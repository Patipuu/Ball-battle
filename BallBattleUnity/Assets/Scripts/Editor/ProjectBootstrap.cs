using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.Rendering;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Applies the project-wide settings Phase 1 requires (URP 2D, portrait, Android ARM64/IL2CPP).
    /// Idempotent: safe to run again after a Unity upgrade or a settings reset.
    /// Batch: Unity.exe -batchmode -projectPath BallBattleUnity -executeMethod BallBattle.EditorTools.ProjectBootstrap.Run -quit
    /// </summary>
    public static class ProjectBootstrap
    {
        const string UrpAssetPath = "Assets/Settings/UniversalRP.asset";
        const string AndroidAppId = "com.patipuu.ballbattle";

        [MenuItem("BallBattle/Apply Project Settings")]
        public static void Run()
        {
            AssignRenderPipeline();
            ApplyPlayerSettings();
            AssetDatabase.SaveAssets();
            Debug.Log("[BallBattle] Project settings applied");
        }

        static void AssignRenderPipeline()
        {
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            if (urp == null)
                throw new System.InvalidOperationException($"URP asset missing at {UrpAssetPath}");

            GraphicsSettings.defaultRenderPipeline = urp;

            // A quality level with its own pipeline would override the default one; clear every override.
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);
        }

        static void ApplyPlayerSettings()
        {
            PlayerSettings.companyName = "Patipuu";
            PlayerSettings.productName = "Ball Battle";

            // Portrait 9:16 only.
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.allowedAutorotateToPortrait = true;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = false;
            PlayerSettings.allowedAutorotateToLandscapeRight = false;

            // Windows playtest window: 270x480 native art at x2 fits any 1080p monitor; resizable for x3/x4.
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            // Keep simulating when unfocused (screen recorders, Editor previews driven over MCP).
            PlayerSettings.runInBackground = true;

            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidAppId);
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }
    }
}
