using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Player builds into &lt;repo&gt;/Builds/&lt;target&gt; (outside the Unity project, git-ignored).
    /// Menu: BallBattle/Build/...
    /// Batch: Unity.exe -batchmode -quit -projectPath BallBattleUnity -executeMethod BallBattle.EditorTools.BuildScript.BuildLinux
    ///        (or BuildWindows / BuildWebGL / BuildWebGLItch / BuildAndroid / BuildAll). Exit code 0 = success, 1 = failure.
    /// BuildWebGLItch: WebGL with decompression fallback (itch.io serves .gz files without Content-Encoding), zipped for upload.
    /// BuildAndroid: arm64 APK. Signed with the keystore in BB_KEYSTORE / BB_KEYSTORE_PASS (alias "ballbattle") when set,
    /// otherwise with Unity's debug key (installs fine, but a later build signed differently cannot update it).
    /// Both clients read the PvP server URL from Assets/Resources/pvp-server.txt (PvpServer/deploy/deploy.ps1 -WriteClientUrl).
    /// Needs the matching Build Support module for this editor version.
    /// </summary>
    public static class BuildScript
    {
        const string ProductFile = "BallBattle";

        [MenuItem("BallBattle/Build/Linux Standalone")]
        public static void BuildLinuxMenu() => Build(BuildTarget.StandaloneLinux64, false);

        [MenuItem("BallBattle/Build/Windows Standalone")]
        public static void BuildWindowsMenu() => Build(BuildTarget.StandaloneWindows64, false);

        [MenuItem("BallBattle/Build/WebGL")]
        public static void BuildWebGLMenu() => Build(BuildTarget.WebGL, false);

        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, true);
        public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, true);
        public static void BuildWebGL() => Build(BuildTarget.WebGL, true);

        [MenuItem("BallBattle/Build/WebGL for itch.io (zip)")]
        public static void BuildWebGLItchMenu() => BuildWebGLItchCore(false);
        public static void BuildWebGLItch() => BuildWebGLItchCore(true);

        [MenuItem("BallBattle/Build/Android APK")]
        public static void BuildAndroidMenu() => BuildAndroidCore(false);
        public static void BuildAndroid() => BuildAndroidCore(true);

        static void BuildWebGLItchCore(bool exitWhenBatch)
        {
            var fallback = PlayerSettings.WebGL.decompressionFallback;
            PlayerSettings.WebGL.decompressionFallback = true;
            var ok = false;
            try
            {
                ok = Build(BuildTarget.WebGL, false, "WebGL-itch");
                if (ok)
                {
                    var dir = OutputDir(BuildTarget.WebGL, "WebGL-itch");
                    var zip = Path.Combine(Path.GetDirectoryName(dir), "BallBattle-webgl-itch.zip");
                    if (File.Exists(zip)) File.Delete(zip);
                    System.IO.Compression.ZipFile.CreateFromDirectory(dir, zip);
                    Debug.Log("[BuildScript] itch.io upload: " + zip);
                }
            }
            finally { PlayerSettings.WebGL.decompressionFallback = fallback; }
            if (exitWhenBatch && Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        static void BuildAndroidCore(bool exitWhenBatch)
        {
            EditorUserBuildSettings.buildAppBundle = false;
            PlayerSettings.Android.forceInternetPermission = true;   // PvP talks to the HTTPS server
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            var keystore = Environment.GetEnvironmentVariable("BB_KEYSTORE");
            var pass = Environment.GetEnvironmentVariable("BB_KEYSTORE_PASS");
            var custom = !string.IsNullOrEmpty(keystore) && File.Exists(keystore) && !string.IsNullOrEmpty(pass);
            PlayerSettings.Android.useCustomKeystore = custom;
            if (custom)
            {
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = pass;
                PlayerSettings.Android.keyaliasName = "ballbattle";
                PlayerSettings.Android.keyaliasPass = pass;
            }
            else Debug.LogWarning("[BuildScript] BB_KEYSTORE not set: APK signed with the debug key.");
            var ok = Build(BuildTarget.Android, false);
            if (custom)
            {
                // Never leave the password in ProjectSettings.asset (Unity keeps it in memory only, but be explicit).
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
                PlayerSettings.Android.useCustomKeystore = false;
            }
            if (exitWhenBatch && Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void BuildAll()
        {
            var ok = Build(BuildTarget.StandaloneLinux64, false) && Build(BuildTarget.WebGL, false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>&lt;repo&gt;/Builds/&lt;folder&gt;; the repo root is the parent of the Unity project.
        /// Batch override: -bbBuildOut &lt;dir&gt; builds into that folder instead (e.g. a fresh folder, so an older build is kept).</summary>
        public static string OutputDir(BuildTarget target) => OutputDir(target, null);

        static string OutputDir(BuildTarget target, string folderOverride)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-bbBuildOut");
            if (i >= 0 && i + 1 < args.Length) return Path.GetFullPath(args[i + 1]);
            var folder = folderOverride ?? (target == BuildTarget.WebGL ? "WebGL" : target == BuildTarget.StandaloneWindows64 ? "Windows" :
                target == BuildTarget.Android ? "Android" : "Linux");
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", folder));
        }

        static string[] Scenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return scenes.Length > 0 ? scenes : new[] { SceneBuilder.ArenaScenePath };
        }

        static bool Build(BuildTarget target, bool exitWhenBatch) => Build(target, exitWhenBatch, null);

        static bool Build(BuildTarget target, bool exitWhenBatch, string folder)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var ok = false;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[BuildScript] {target} is not installed. Add its Build Support module to Unity {Application.unityVersion} in Unity Hub.");
            }
            else
            {
                var dir = OutputDir(target, folder);
                Directory.CreateDirectory(dir);
                var location = target == BuildTarget.WebGL ? dir : Path.Combine(dir, ProductFile +
                    (target == BuildTarget.StandaloneWindows64 ? ".exe" : target == BuildTarget.Android ? ".apk" : ".x86_64"));
                var options = new BuildPlayerOptions
                {
                    scenes = Scenes(),
                    locationPathName = location,
                    target = target,
                    targetGroup = group,
                    options = BuildOptions.None
                };
                try
                {
                    var summary = BuildPipeline.BuildPlayer(options).summary;
                    ok = summary.result == BuildResult.Succeeded;
                    Debug.Log($"[BuildScript] {target}: {summary.result}, {summary.totalSize / (1024f * 1024f):0.0} MB, " +
                              $"{summary.totalErrors} errors, {summary.totalTime.TotalSeconds:0} s -> {location}");
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
            if (exitWhenBatch && Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            return ok;
        }
    }
}
