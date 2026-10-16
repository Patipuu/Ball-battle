using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>BB-14: the two tribute balls in the View - art, names, guide, tree text, the orbs / UNITY ring / tail / prey overlay.</summary>
    public class BbTribute1Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbTribute1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static readonly string[] Ids = { ChorusRule.WeaponId, SerpentRule.WeaponId };

        [Test]
        public void BothBallsHaveArtNamesAGuideAndTreeText()
        {
            Assert.That(WeaponNames.CoversAll(), Is.True);
            Assert.That(WeaponNames.Of("chorus"), Is.EqualTo("BGLGXLQ"));
            foreach (var id in Ids)
            {
                var a = art.Get(id);
                Assert.That(a.Ball, Is.Not.Null, id);
                Assert.That(a.Blade, Is.Not.Null, id);
                Assert.That(art.SkillIcon(id), Is.Not.Null, id);
                Assert.That(WeaponGuide.For(id), Is.Not.Null, id);
                Assert.That(Palette.Look(id).Body, Is.Not.EqualTo(Palette.Text), id + " has its own colour");
                foreach (var n in new[] { Sim.Meta.TreeNode.Instinct, Sim.Meta.TreeNode.Opener })
                    Assert.That(TreeText.Name(id, n), Is.Not.EqualTo(TreeText.Name("blade", n)), id + " has its own node names");
            }
        }

        static MatchSim Played(string a, string b, uint seed, int ticks)
        {
            var m = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
            for (var i = 0; i < ticks && m.Outcome == MatchOutcome.Ongoing; i++) m.Step();
            return m;
        }

        [Test]
        public void TheOverlayShowsLitOrbsTailAndPrey()
        {
            var v = TributeView.Create(root.transform, art.BallFlash);
            var m = Played("chorus", "serpent", 4, 1500);
            var pos = m.Balls.Select(b => new Vector2(b.Pos.X.ToFloat(), b.Pos.Y.ToFloat())).ToArray();
            v.Render(m, pos, 0f);
            var chorus = (ChorusRule)m.Balls[0].Weapon;
            var snake = (SerpentRule)m.Balls[1].Weapon;
            Assert.That(v.OrbsLit(0), Is.EqualTo(chorus.Lit), "one bright orb per lit colour");
            Assert.That(v.TailShown(1), Is.EqualTo(snake.Segments));
            Assert.That(v.BeadsShown(1), Is.EqualTo(snake.Segments * 3), "beads join the segments into one body");
            Assert.That(v.PreyShown(1), Is.EqualTo(snake.PreyCount));
            Assert.That(v.TailShown(0) + v.BeadsShown(0) + v.PreyShown(0) + v.OrbsLit(1), Is.Zero, "nothing for the wrong ball");
        }

        [Test]
        public void UnityStartsTheRing()
        {
            var v = TributeView.Create(root.transform, art.BallFlash);
            var m = Played("chorus", "blade", 2, 0);
            SimEvent unity = default;
            var found = false;
            while (!found && m.Outcome == MatchOutcome.Ongoing)
            {
                m.Step();
                foreach (var e in m.Events) if (e.Type == SimEventType.ChorusUnity) { unity = e; found = true; }
            }
            Assert.That(found, Is.True);
            v.OnEvent(unity, m);
            Assert.That(v.RingPlaying, Is.True);
            var pos = m.Balls.Select(b => new Vector2(b.Pos.X.ToFloat(), b.Pos.Y.ToFloat())).ToArray();
            v.Render(m, pos, 1f);
            Assert.That(v.RingPlaying, Is.False, "the ring lasts under half a second");
        }

        [Test]
        public void TheCodexCyclesThroughAllTenBalls()
        {
            Assert.That(SkillDefinition.All.Length, Is.EqualTo(WeaponRegistry.All.Count));
            Assert.That(SkillDefinition.All.Select(s => s.WeaponId), Is.EquivalentTo(WeaponRegistry.All.Select(e => e.Id)));
        }

        [Test]
        public void CaptureTributeFights()
        {
            var dir = Environment.GetEnvironmentVariable("BB_TRIBUTE1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_TRIBUTE1_OUT not set");
            Directory.CreateDirectory(dir);
            var camGo = new GameObject("Cam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                void Shoot(string name)
                {
                    foreach (var zoom in new[] { 1, 3 })
                    {
                        var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) { filterMode = FilterMode.Point };
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{name}-{zoom}x.png"), tex.EncodeToPNG());
                        RenderTexture.active = null; cam.targetTexture = null;
                        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                    }
                }
                var go = new GameObject("Arena");
                go.transform.SetParent(root.transform, false);
                var arena = go.AddComponent<ArenaView>();
                arena.Art = art;
                arena.AutoRestart = false;
                foreach (var (a, b, seed, ticks, name) in new[] { ("chorus", "blade", 2u, 700, "1-bglgxlq-ring"), ("serpent", "fang", 3u, 1400, "2-hunter-tail"), ("chorus", "serpent", 4u, 1500, "3-both") })
                {
                    arena.StartMatch(Played(a, b, seed, ticks));
                    Shoot(name);
                }
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
