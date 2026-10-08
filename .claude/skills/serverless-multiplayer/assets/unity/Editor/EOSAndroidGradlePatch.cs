// EOSAndroidGradlePatch — Unity 6 (AGP 8.x / Gradle 9) build fix for PlayEveryWare EOS 6.1.0 on Android.
//
// Install: drop into any Editor-only folder of the game project (e.g. Assets/Editor/). No configuration.
// Runs as IPostGenerateGradleAndroidProject (callbackOrder 10000, after EDM4U) on every Android export,
// patches the GENERATED Gradle project (never the plugin's Assets copy, which PEW re-copies each build).
// Idempotent; a no-op once the plugin ships a Unity-6-clean androidlib.
//
// What each step fixes:
//   A. FixSpacesInGradleFileUris  — Gradle 9 cannot convert a file:// Maven repository URI containing a
//      literal space ("Cannot convert URI ... to a file") when the project path has a space.
//      Percent-encodes spaces in file: URIs of every *.gradle file (also helps Firebase/EDM repos).
//   B. EnableCoreLibraryDesugaring — eos-sdk.aar AAR metadata requires core library desugaring on the
//      app module; without it ":launcher:checkDebugAarMetadata" fails. Adds coreLibraryDesugaringEnabled
//      + desugar_jdk_libs to the module that applies com.android.application (not the root script).
//   C. eos_dependencies.androidlib AndroidManifest.xml — AGP 8 forbids the manifest package attribute;
//      it is stripped.
//   D. eos_dependencies.androidlib build.gradle:
//      D1. remove the legacy buildscript { } block (calls jcenter(), removed in Gradle 9; pins AGP 3.6.0);
//      D2. strip any remaining jcenter() line;
//      D3. add namespace 'com.pew.eos_dependencies' inside android { } (AGP 8 requirement);
//      D4. replace unresolved placeholders compileSdkVersion -1 / targetSdkVersion -1 /
//          buildToolsVersion 'NOBUILDTOOLS' (rejected by AGP 9) with Unity's own
//          unity.compileSdkVersion / unity.targetSdkVersion root Gradle properties.
//
// NOTE: intentionally NOT guarded by #if UNITY_ANDROID — the UnityEditor.Android
// interface is available whenever the Android module is installed (regardless of
// the active build target), so the post-processor always compiles and registers.
// OnPostGenerateGradleAndroidProject only fires during an actual Android build.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

namespace TeamNet.Multiplayer.EditorTools
{
    /// <summary>
    /// Unity 6 (AGP 8) compatibility patch for the PlayEveryWare EOS plugin's Android
    /// dependency library (<c>eos_dependencies.androidlib</c>).
    ///
    /// The plugin ships a legacy-style Android library that declares its package via the
    /// manifest <c>package="com.pew.eos_dependencies"</c> attribute and has no
    /// <c>namespace</c> in its <c>build.gradle</c>. Android Gradle Plugin 8.x (used by
    /// Unity 6) rejects both: it requires a <c>namespace</c> in the module's build script
    /// and no longer allows the manifest <c>package</c> attribute. Its generated library
    /// also leaves the old Unity placeholders <c>compileSdkVersion -1</c>,
    /// <c>targetSdkVersion -1</c>, and <c>buildToolsVersion 'NOBUILDTOOLS'</c> unresolved.
    /// AGP 9 rejects those values, so this postprocessor replaces them with the SDK values
    /// emitted by Unity's root Gradle project.
    ///
    /// The plugin's own <c>AndroidBuilder.PreBuild</c> re-copies the library on every build
    /// (overwriting any manual edit to the Assets copy), so this fix runs after Unity has
    /// generated the Gradle project and patches the generated files directly. It is
    /// idempotent and a no-op once the plugin ships a Unity-6-clean library.
    /// </summary>
    public sealed class EOSAndroidGradlePatch : IPostGenerateGradleAndroidProject
    {
        private const string EosPackage = "com.pew.eos_dependencies";

        // Run very late so EDM4U has already written its repositories into the project
        // (we need to patch its generated settings.gradle) and the EOS library is present.
        public int callbackOrder => 10000;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            // The generated Gradle project root is `path` (the unityLibrary module dir);
            // search its parent so we also catch sibling modules and the root settings.gradle.
            string searchRoot = Directory.GetParent(path)?.FullName ?? path;
            int patched = 0;

            // Project-wide fix (needed by Firebase/EDM too, not just EOS): the project path
            // contains a space (e.g. "My Game Project"), and Gradle 9 (Unity 6) refuses to
            // convert a file:// Maven URL that contains a literal space
            // ("Cannot convert URI ... to a file"). Percent-encode spaces in file: URIs.
            FixSpacesInGradleFileUris(searchRoot);

            // EOS's eos-sdk.aar is built against Java 8+ APIs and its AAR metadata demands
            // core library desugaring on the consuming app module, otherwise the build fails at
            // ":launcher:checkDebugAarMetadata" with "requires core library desugaring".
            EnableCoreLibraryDesugaring(searchRoot);

            foreach (string manifest in SourceFiles(searchRoot, "AndroidManifest.xml"))
            {
                string manifestText = File.ReadAllText(manifest);
                if (!manifestText.Contains(EosPackage))
                {
                    continue;
                }

                // 1) Remove the now-unsupported `package="com.pew.eos_dependencies"` attribute.
                string strippedManifest = Regex.Replace(
                    manifestText,
                    "\\s+package\\s*=\\s*\"" + Regex.Escape(EosPackage) + "\"",
                    string.Empty);

                if (strippedManifest != manifestText)
                {
                    File.WriteAllText(manifest, strippedManifest);
                }

                // 2) Ensure the sibling build.gradle declares the namespace inside `android { }`.
                string moduleDir = Path.GetDirectoryName(manifest);
                if (moduleDir == null)
                {
                    continue;
                }

                PatchBuildGradle(moduleDir);
                patched++;
            }

            if (patched > 0)
            {
                Debug.Log($"[EOSAndroidGradlePatch] Patched {patched} EOS Android library module(s) for AGP 8 / Gradle 9 (namespace, SDK placeholders, manifest, and obsolete buildscript/jcenter).");
            }
            else
            {
                Debug.LogWarning("[EOSAndroidGradlePatch] No EOS Android library module found to patch — verify the plugin still ships eos_dependencies.androidlib.");
            }
        }

        private static void PatchBuildGradle(string moduleDir)
        {
            string buildGradle = Path.Combine(moduleDir, "build.gradle");
            if (!File.Exists(buildGradle))
            {
                return;
            }

            string text = File.ReadAllText(buildGradle);
            string original = text;

            // 1) Remove the obsolete `buildscript { }` block. It calls jcenter() (removed in
            //    Gradle 9, which Unity 6 uses -> "Could not find method jcenter()") and pins an
            //    ancient AGP classpath (3.6.0) that conflicts with the root project's AGP 8.x.
            text = RemoveBlock(text, "buildscript");

            // 2) Belt-and-suspenders: strip any stray jcenter() lines left anywhere.
            text = Regex.Replace(text, "^[ \\t]*jcenter\\(\\)[ \\t]*\\r?\\n", string.Empty, RegexOptions.Multiline);

            // 3) Ensure the android { } block declares a namespace (AGP 8 requirement).
            if (!Regex.IsMatch(text, "namespace\\s+['\"]"))
            {
                text = Regex.Replace(text, "android\\s*\\{", "android {\n    namespace '" + EosPackage + "'");
            }

            // 4) The plugin's legacy Android library hard-codes Unity's old unresolved
            // placeholders (-1 / NOBUILDTOOLS). AGP 9 requires real SDK values. Read the
            // root Gradle properties generated by Unity so this remains correct when Unity's
            // target SDK changes instead of baking a specific API level into the plugin patch.
            text = Regex.Replace(
                text,
                @"(?m)^[ \t]*compileSdkVersion\s+-1[ \t]*\r?\n",
                "    compileSdk project.property('unity.compileSdkVersion').toInteger()\n");
            text = Regex.Replace(
                text,
                @"(?m)^[ \t]*targetSdkVersion\s+-1[ \t]*\r?\n",
                "        targetSdk project.property('unity.targetSdkVersion').toInteger()\n");
            text = Regex.Replace(
                text,
                @"(?m)^[ \t]*buildToolsVersion\s+['""]NOBUILDTOOLS['""][ \t]*\r?\n",
                string.Empty);

            if (text != original)
            {
                File.WriteAllText(buildGradle, text);
            }
        }

        /// <summary>
        /// Removes every top-level <c>{blockKeyword} { ... }</c> block from the text using
        /// brace matching (handles nested braces inside the block).
        /// </summary>
        private static string RemoveBlock(string text, string blockKeyword)
        {
            int start = text.IndexOf(blockKeyword, System.StringComparison.Ordinal);
            while (start >= 0)
            {
                int open = text.IndexOf('{', start);
                if (open < 0)
                {
                    break;
                }

                int depth = 0;
                int end = open;
                for (; end < text.Length; end++)
                {
                    if (text[end] == '{')
                    {
                        depth++;
                    }
                    else if (text[end] == '}')
                    {
                        depth--;
                        if (depth == 0)
                        {
                            end++;
                            break;
                        }
                    }
                }

                text = text.Remove(start, end - start);
                start = text.IndexOf(blockKeyword, start, System.StringComparison.Ordinal);
            }

            return text;
        }

        /// <summary>
        /// Percent-encodes spaces inside <c>file:</c> URIs in every .gradle file under the
        /// project root, so Gradle 9 can resolve local Maven repositories when the project
        /// path contains a space (e.g. "My Game Project").
        /// </summary>
        private static void FixSpacesInGradleFileUris(string gradleRoot)
        {
            foreach (string gradle in SourceFiles(gradleRoot, "*.gradle"))
            {
                string text = File.ReadAllText(gradle);

                // Groovy dollar-slashy strings:  $/file:.../$
                string patched = Regex.Replace(text, @"\$/(file:.*?)/\$",
                    m => "$/" + m.Groups[1].Value.Replace(" ", "%20") + "/$", RegexOptions.Singleline);

                // Quoted strings holding a file: URI:  "file:..."
                patched = Regex.Replace(patched, "\"(file:[^\"]*)\"",
                    m => "\"" + m.Groups[1].Value.Replace(" ", "%20") + "\"");

                if (patched != text)
                {
                    File.WriteAllText(gradle, patched);
                    Debug.Log($"[EOSAndroidGradlePatch] Percent-encoded spaces in file: URIs -> {Path.GetFileName(gradle)}");
                }
            }
        }

        /// <summary>
        /// Enables Java 8+ core library desugaring on the Android application (launcher) module,
        /// required by EOS's <c>eos-sdk.aar</c> under AGP 8 / Gradle 9.
        /// </summary>
        private static void EnableCoreLibraryDesugaring(string gradleRoot)
        {
            const string DesugarLib = "com.android.tools:desugar_jdk_libs:2.1.4";

            foreach (string gradle in SourceFiles(gradleRoot, "build.gradle"))
            {
                string text = File.ReadAllText(gradle);
                // Only the module that APPLIES the application plugin (launcher) — NOT the root
                // build.gradle, which merely declares it in a plugins { ... apply false } block
                // (Gradle 9 forbids any statement before that plugins { } block).
                if (!Regex.IsMatch(text, @"apply\s+plugin:\s*['""]com\.android\.application['""]"))
                {
                    continue;
                }

                string original = text;

                // 1) coreLibraryDesugaringEnabled true — inside compileOptions { }.
                if (!Regex.IsMatch(text, @"coreLibraryDesugaringEnabled\s+true"))
                {
                    if (Regex.IsMatch(text, @"compileOptions\s*\{"))
                    {
                        text = new Regex(@"compileOptions\s*\{").Replace(
                            text, "compileOptions {\n        coreLibraryDesugaringEnabled true", 1);
                    }
                    else
                    {
                        text = new Regex(@"android\s*\{").Replace(
                            text, "android {\n    compileOptions {\n        coreLibraryDesugaringEnabled true\n    }", 1);
                    }
                }

                // 2) coreLibraryDesugaring runtime — inside dependencies { }.
                if (!text.Contains("desugar_jdk_libs"))
                {
                    if (Regex.IsMatch(text, @"dependencies\s*\{"))
                    {
                        text = new Regex(@"dependencies\s*\{").Replace(
                            text, "dependencies {\n    coreLibraryDesugaring '" + DesugarLib + "'", 1);
                    }
                    else
                    {
                        text = "dependencies {\n    coreLibraryDesugaring '" + DesugarLib + "'\n}\n\n" + text;
                    }
                }

                if (text != original)
                {
                    File.WriteAllText(gradle, text);
                    Debug.Log("[EOSAndroidGradlePatch] Enabled core library desugaring on the launcher module.");
                }
            }
        }

        /// <summary>
        /// Enumerates matching files under <paramref name="root"/>, skipping Gradle's own
        /// <c>build/</c> output directories so we only ever patch source files.
        /// </summary>
        private static IEnumerable<string> SourceFiles(string root, string pattern)
        {
            return Directory.GetFiles(root, pattern, SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/build/"));
        }
    }
}
