using System.Linq;
using BallBattle.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Builds Assets/Scenes/Arena.unity from code (camera with URP PixelPerfectCamera 270x480, ArenaView).
    /// Menu: BallBattle/Build Scenes.
    /// Batch: Unity.exe -batchmode -projectPath BallBattleUnity -executeMethod BallBattle.EditorTools.SceneBuilder.BuildAll -quit
    /// </summary>
    public static class SceneBuilder
    {
        public const string ArenaScenePath = "Assets/Scenes/Arena.unity";
        public const int NativeWidth = 270;
        public const int NativeHeight = 480;

        [MenuItem("BallBattle/Build Scenes")]
        public static void BuildAll()
        {
            PlaceholderArtGenerator.Run();
            BuildArena();
            RegisterScenes(ArenaScenePath);
            Debug.Log("[BallBattle] Scenes built");
        }

        static void BuildArena()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = NativeHeight / 2f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.Background;
            cam.transform.position = new Vector3(0f, 0f, -10f);
            camGo.AddComponent<UniversalAdditionalCameraData>();

            var ppc = camGo.AddComponent<PixelPerfectCamera>();
            ppc.assetsPPU = 1;
            ppc.refResolutionX = NativeWidth;
            ppc.refResolutionY = NativeHeight;
            ppc.gridSnapping = PixelPerfectCamera.GridSnapping.UpscaleRenderTexture;
            ppc.cropFrame = PixelPerfectCamera.CropFrame.Windowbox;

            // Global unlit-equivalent light: sprites use the lit 2D material, so a future local Light2D must not darken the scene.
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
            light.color = Color.white;

            var arena = new GameObject("Arena").AddComponent<ArenaView>();
            arena.Art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);

            EditorSceneManager.SaveScene(scene, ArenaScenePath);
        }

        static void RegisterScenes(params string[] paths)
        {
            var list = EditorBuildSettings.scenes.ToList();
            foreach (var p in paths)
                if (list.All(s => s.path != p)) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
