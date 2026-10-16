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
    ///        (or BuildWebGL / BuildAll). Exit code 0 = success, 1 = failure.
    /// Needs the Linux Build Support (Mono) and WebGL Build Support modules for this editor version.
    /// </summary>
    public static class BuildScript
    {
        const string ProductFile = "BallBattle";

        [MenuItem("BallBattle/Build/Linux Standalone")]
        public static void BuildLinuxMenu() => Build(BuildTarget.StandaloneLinux64, false);

        [MenuItem("BallBattle/Build/WebGL")]
        public static void BuildWebGLMenu() => Build(BuildTarget.WebGL, false);

        public static void BuildLinux() => Build(BuildTarget.StandaloneLinux64, true);
        public static void BuildWebGL() => Build(BuildTarget.WebGL, true);

        public static void BuildAll()
        {
            var ok = Build(BuildTarget.StandaloneLinux64, false) && Build(BuildTarget.WebGL, false);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>&lt;repo&gt;/Builds/&lt;folder&gt;; the repo root is the parent of the Unity project.
        /// Batch override: -bbBuildOut &lt;dir&gt; builds into that folder instead (e.g. a fresh folder, so an older build is kept).</summary>
        public static string OutputDir(BuildTarget target)
        {
            var args = Environment.GetCommandLineArgs();
            var i = Array.IndexOf(args, "-bbBuildOut");
            if (i >= 0 && i + 1 < args.Length) return Path.GetFullPath(args[i + 1]);
            var folder = target == BuildTarget.WebGL ? "WebGL" : "Linux";
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "Builds", folder));
        }

        static string[] Scenes()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            return scenes.Length > 0 ? scenes : new[] { SceneBuilder.ArenaScenePath };
        }

        static bool Build(BuildTarget target, bool exitWhenBatch)
        {
            var group = BuildPipeline.GetBuildTargetGroup(target);
            var ok = false;
            if (!BuildPipeline.IsBuildTargetSupported(group, target))
            {
                Debug.LogError($"[BuildScript] {target} is not installed. Add its Build Support module to Unity {Application.unityVersion} in Unity Hub.");
            }
            else
            {
                var dir = OutputDir(target);
                Directory.CreateDirectory(dir);
                var location = target == BuildTarget.WebGL ? dir : Path.Combine(dir, ProductFile + ".x86_64");
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