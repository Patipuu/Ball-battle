using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>UI-UX2: readable arena preview and thumbnail, secondary text contrast, even menu buttons, skill word clear of other balls.</summary>
    public class BbUx2Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbUx2Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static double Lum(Color32 c)
        {
            double Ch(byte v) { var s = v / 255.0; return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
            return 0.2126 * Ch(c.r) + 0.7152 * Ch(c.g) + 0.0722 * Ch(c.b);
        }
        static double Contrast(Color32 a, Color32 b) { var x = Lum(a); var y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

        [Test]
        public void SecondaryTextReadsOnEveryPanel()
        {
            foreach (var bg in new[] { Palette.Background, Palette.Floor })
                Assert.That(Contrast(Palette.TextDim, bg), Is.GreaterThanOrEqualTo(6.5), $"TextDim on {bg}");
            Assert.That(Contrast(Palette.TextDim, Palette.HpBack), Is.GreaterThanOrEqualTo(6.0), "TextDim on the HP bar back");
            Assert.That(Lum(Palette.TextDim), Is.LessThan(Lum(Palette.Text) * 0.7), "still clearly dimmer than main text");
        }

        [TestCase(ArenaRegistry.SpikeWalls)]
        [TestCase(ArenaRegistry.LowGravity)]
        [TestCase(ArenaRegistry.Classic)]
        public void PreviewIsVeiledAndWallsLifted(string arenaId)
        {
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            var v = PrepareView.Create(root.transform, art);
            v.ArenaOverride = arenaId;
            v.Show(run);
            var rs = v.ArenaPreviewRoot.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(rs.Count(r => ((Color32)r.color).Equals(PrepareView.PreviewVeil)), Is.EqualTo(1), "one veil over the floor");
            var veil = rs.Single(r => ((Color32)r.color).Equals(PrepareView.PreviewVeil));
            Assert.That(v.PreviewFloor == null || v.PreviewFloor.sortingOrder < veil.sortingOrder, Is.True, "veil above the floor tile");
            Assert.That(art.TryArena(arenaId, out var look), Is.True);
            Assert.That(rs.Any(r => ((Color32)r.color).Equals(PrepareView.PreviewWall(look.WallColor))), Is.True, "lifted wall frame");
            Assert.That(Lum(PrepareView.PreviewWall(look.WallColor)), Is.GreaterThan(Lum(look.WallColor)));
        }

        [Test]
        public void SpikeWallsThumbnailIsTheBrighterV4()
        {
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes("Assets/Art/Sprites/arena_thumbs/thumb_spike-walls_v3.png"));
            Assert.That((tex.width, tex.height), Is.EqualTo((48, 48)));
            var px = tex.GetPixels32();
            Assert.That(px.Count(c => c.r == 58 && c.g == 54 && c.b == 68), Is.GreaterThan(200), "v4 floor 58,54,68 (v3 was 32,30,34)");
            Assert.That(px.Count(c => c.r == 32 && c.g == 30 && c.b == 34), Is.EqualTo(0), "no v3 floor left");
        }

        [Test]
        public void MenuButtonsShareOneSizeAndColumn()
        {
            var m = MenuView.Create(root.transform, art);
            Assert.That(MenuView.CodexArea.size, Is.EqualTo(MenuView.VersusArea.size));
            Assert.That(MenuView.CodexArea.center.x, Is.EqualTo(MenuView.VersusArea.center.x));
            Assert.That(MenuView.CodexArea.yMax, Is.LessThanOrEqualTo(MenuView.VersusArea.yMin - 8f), "gap between the buttons");
            var codex = m.GetComponentsInChildren<PixelButton>(true).Single(b => b.name == "CodexButton");
            Assert.That(codex.LabelText, Is.EqualTo("CODEX"));
            Assert.That(codex.LabelScale, Is.EqualTo(m.VersusButton.LabelScale), "same label size as VERSUS");
            Bb0102Tests.AssertTextFitsAndNoOverlap(m.transform.Find("Hub"));
        }

        [Test]
        public void SkillWordAvoidsTheBossAbove()
        {
            // SPORE (r 16) under WARDEN (r 24) touching: the word goes below the caster, clear of the boss for its whole rise.
            var caster = new Vector2(0, 0);
            var boss = new Vector2(0, 40);
            var at = DamagePopups.SkillWordStart(caster, 16, "DRAIN", new List<(Vector2, float)> { (boss, 24) });
            var r = DamagePopups.StartRect(at, "DRAIN", 1);
            var swept = new Rect(r.xMin, r.yMin, r.width, r.height + DamagePopups.Rise);
            Assert.That(swept.Overlaps(new Rect(-24, 16, 48, 48)), Is.False, "clear of the boss");
            Assert.That(swept.yMax, Is.LessThanOrEqualTo(-16f), "below the caster");
        }

        [Test]
        public void SkillWordGoesAboveWhenNothingIsThere()
        {
            var at = DamagePopups.SkillWordStart(new Vector2(10, 20), 16, "LUNGE", new List<(Vector2, float)> { (new Vector2(100, -50), 16) });
            var r = DamagePopups.StartRect(at, "LUNGE", 1);
            Assert.That(r.yMin, Is.EqualTo(20 + 16 + 2).Within(0.5f), "ink starts 2 px above the ball");
            Assert.That(r.center.x, Is.EqualTo(10).Within(1f));
        }

        [Test]
        public void SkillWordFallsBackAboveWhenBoxedIn()
        {
            var others = new List<(Vector2, float)> { (new Vector2(0, 60), 40), (new Vector2(0, -60), 40), (new Vector2(-60, 0), 40), (new Vector2(60, 0), 40) };
            var at = DamagePopups.SkillWordStart(Vector2.zero, 16, "FRENZY", others);
            Assert.That(DamagePopups.StartRect(at, "FRENZY", 1).yMin, Is.EqualTo(18).Within(0.5f));
        }

        [Test]
        public void CaptureUx2Screens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_UX2_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_UX2_OUT not set");
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
                var m = MenuView.Create(root.transform, art);
                Shoot("1-menu");
                m.ShowVersus();
                Shoot("2-versus");
                Object.DestroyImmediate(m.gameObject);
                var p = new ProgressData();
                foreach (var arena in new[] { ArenaRegistry.SpikeWalls, ArenaRegistry.LowGravity, ArenaRegistry.Bumpers, ArenaRegistry.Pillar })
                {
                    var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
                    run.Pick(0);
                    var v = PrepareView.Create(root.transform, art);
                    v.ArenaOverride = arena;
                    v.Show(run);
                    Shoot("3-prepare-" + arena);
                    Object.DestroyImmediate(v.gameObject);
                }
                var br = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
                br.Pick(0);
                br.FightIndex = RunTuning.BossFights[BossIds.SlotOf(BossIds.Warden)];
                br.BossIds[BossIds.SlotOf(BossIds.Warden)] = BossIds.Warden;
                var bp = PrepareView.Create(root.transform, art);
                bp.Show(br);
                Shoot("4-prepare-boss-warden");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
