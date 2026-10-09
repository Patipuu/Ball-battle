using System.Reflection;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>Drives ArenaView's draw path (projectiles, turrets, status marks, trait badges, obstacles) without errors.</summary>
    public class ViewSmokeTests
    {
        [TestCase("rig", "venom", ArenaRegistry.Bumpers)]
        [TestCase("volley", "aegis", ArenaRegistry.Pillar)]
        [TestCase("blade", "fang", ArenaRegistry.SpikeWalls)]
        public void DrawsEveryEffectWithoutErrors(string a, string b, string arenaId)
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(lib, Is.Not.Null);
            var go = new GameObject("ArenaViewSmoke");
            try
            {
                var view = go.AddComponent<ArenaView>();
                view.Art = lib;
                var la = new BallLoadout(WeaponRegistry.Create(a)).With(TraitRegistry.Create("bubble", 2), TraitRegistry.Create("poison-tip", 1));
                var lb = new BallLoadout(WeaponRegistry.Create(b)).With(TraitRegistry.Create("thorns", 1));
                var sim = new MatchSim(ArenaRegistry.Create(arenaId), 5, new[] { la, lb });
                sim.ApplySpawnHooks();
                view.StartMatch(sim, "YOU", "FOE");
                var draw = typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance);
                for (var tick = 0; tick < 1200 && sim.Outcome == MatchOutcome.Ongoing; tick++)
                {
                    sim.Step();
                    if (tick % 7 == 0) draw.Invoke(view, new object[] { 0.5f, 0.016f });
                }
                Assert.Pass();
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
/// <summary>Writes PNGs of a busy fight and the Versus menu to the folder in BB_CAPTURE (skipped when unset).</summary>
        [Test]
        public void CaptureScreens()
        {
            var dir = System.Environment.GetEnvironmentVariable("BB_CAPTURE");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_CAPTURE not set");
            System.IO.Directory.CreateDirectory(dir);
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            var root = new GameObject("CaptureRoot");
            var camGo = new GameObject("CaptureCam");
            GameObject menuRoot = null;
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 240f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                var view = root.AddComponent<ArenaView>();
                view.Art = lib;
                var la = new BallLoadout(WeaponRegistry.Create("rig")).With(TraitRegistry.Create("bubble", 2), TraitRegistry.Create("poison-tip", 1));
                var lb = new BallLoadout(WeaponRegistry.Create("volley")).With(TraitRegistry.Create("thorns", 1), TraitRegistry.Create("vampire", 2));
                var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Bumpers), 3, new[] { la, lb });
                sim.ApplySpawnHooks();
                view.StartMatch(sim, "YOU", "FOE");
                var draw = typeof(ArenaView).GetMethod("Draw", BindingFlags.NonPublic | BindingFlags.Instance);
                for (var tick = 0; tick < 1500 && sim.Outcome == MatchOutcome.Ongoing; tick++) sim.Step();
                draw.Invoke(view, new object[] { 0f, 0.016f });
                Shoot(cam, System.IO.Path.Combine(dir, "fight.png"));

                root.SetActive(false);
                menuRoot = new GameObject("MenuRoot");
                MenuView.Create(menuRoot.transform, lib);
                Shoot(cam, System.IO.Path.Combine(dir, "menu.png"));
            }
            finally
            {
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(root);
            }
        }

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(270, 480, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(270, 480, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 270, 480), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}