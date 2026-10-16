using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>UI-MENU1: the hub tells a new player where to start and what each button does, and it moves.</summary>
    public class BbMenu1Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbMenu1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static string[] HubTexts(MenuView m) =>
            m.transform.Find("Hub").GetComponentsInChildren<PixelText>(false).Select(t => t.Text).Where(s => s.Length > 0).ToArray();

        /// <summary>UI-CLEAN1 (16/10): no tagline, "NEW? START HERE", rules line or captions under the buttons; the buttons speak for themselves.</summary>
        static readonly string[] Removed =
        {
            "WEAPON BALL AUTO BATTLER", "NEW? START HERE", "LEVEL UP WITH CHEST SHARDS", "WATCH ANY TWO BALLS FIGHT", "WHAT EVERY BALL DOES",
            "8 FIGHTS  2 BOSSES  1 RETRY"
        };

        [Test]
        public void NewPlayerSeesACleanMenuWithEveryButton()
        {
            var m = MenuView.Create(root.transform, art);
            m.SetRunInfo(new ProgressData(), null);
            var texts = HubTexts(m);
            foreach (var s in Removed) Assert.That(texts, Does.Not.Contain(s));
            foreach (var label in new[] { "BALLS", "CODEX", "HOW TO PLAY" })
                Assert.That(m.GetComponentsInChildren<PixelButton>(true).Any(b => b.LabelText == label), Is.True, label);
            for (var t = 0f; t < 4f; t += 0.37f)
            {
                m.Animate(t);
                Bb0102Tests.AssertTextFitsAndNoOverlap(m.transform.Find("Hub"));
            }
        }

        [Test]
        public void ReturningPlayerMenuHasNoCaptionsEither()
        {
            var m = MenuView.Create(root.transform, art);
            var p = new ProgressData { RunsPlayed = 1 };
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            m.SetRunInfo(p, run);
            var texts = HubTexts(m);
            foreach (var s in Removed) Assert.That(texts, Does.Not.Contain(s));
            Bb0102Tests.AssertTextFitsAndNoOverlap(m.transform.Find("Hub"));
        }

        [Test]
        public void TitleIsColouredWithShadowAndWaves()
        {
            var m = MenuView.Create(root.transform, art);
            Assert.That(m.Title.Text, Is.EqualTo("BALL BATTLE"));
            Assert.That(m.Title.HasShadow, Is.True);
            Assert.That(m.Title.GlyphColor(0), Is.EqualTo(Palette.Look("blade").Body));
            Assert.That(m.Title.GlyphColor(1), Is.EqualTo(Palette.Look("volley").Body));
            var colours = Enumerable.Range(0, m.Title.Text.Length).Where(i => m.Title.Text[i] != ' ').Select(i => m.Title.GlyphColor(i)).Distinct().Count();
            Assert.That(colours, Is.GreaterThanOrEqualTo(7));
            m.Animate(0.5f);
            var offsets = Enumerable.Range(0, m.Title.Text.Length).Select(m.Title.GlyphOffsetY).ToArray();
            Assert.That(offsets.All(o => Mathf.Abs(o) <= 2f && o == Mathf.Round(o)), Is.True, "whole pixels, at most 2 px");
            Assert.That(offsets.Distinct().Count(), Is.GreaterThan(1), "a wave, not a block");
        }

        [Test]
        public void RunShineSweepsInsideTheButton()
        {
            var m = MenuView.Create(root.transform, art);
            var seen = 0;
            for (var t = 0f; t < MenuView.ShinePeriod; t += 0.05f)
            {
                m.Animate(t);
                if (!m.RunShine.enabled) continue;
                seen++;
                var b = m.RunShine.bounds;
                Assert.That(b.min.x >= MenuView.RunArea.xMin && b.max.x <= MenuView.RunArea.xMax, Is.True, $"t={t}: {b}");
            }
            Assert.That(seen, Is.GreaterThan(5));
        }

        [Test]
        public void BackdropBallsStayOnScreenAndBounce()
        {
            var m = MenuView.Create(root.transform, art);
            var b = m.Backdrop;
            Assert.That(b.BallCount, Is.EqualTo(MenuBackdrop.Count));
            var start = Enumerable.Range(0, b.BallCount).Select(b.PositionOf).ToArray();
            for (var f = 0; f < 60 * 60; f++)   // one minute at 60 fps
            {
                b.Step(1f / 60f);
                for (var i = 0; i < b.BallCount; i++)
                {
                    var p = b.PositionOf(i);
                    Assert.That(Mathf.Abs(p.x) <= MenuBackdrop.HalfW - MenuBackdrop.Radius + 0.01f && Mathf.Abs(p.y) <= MenuBackdrop.HalfH - MenuBackdrop.Radius + 0.01f, Is.True, $"ball {i} at {p}");
                }
            }
            Assert.That(Enumerable.Range(0, b.BallCount).Count(i => (b.PositionOf(i) - start[i]).magnitude > 10f), Is.GreaterThan(3), "they move");
            Assert.That(b.SpriteOf(0).color.a, Is.EqualTo(MenuBackdrop.Alpha).Within(0.001f), "dimmed");
            Assert.That(b.SpriteOf(0).sortingOrder, Is.LessThan(MenuView.Order + 2), "under buttons and boxes");
        }

        [Test]
        public void HowToPlayOpensFitsAndStartsARun()
        {
            var m = MenuView.Create(root.transform, art);
            var started = 0;
            m.RunRequested += () => started++;
            m.SetRunInfo(new ProgressData(), null);
            m.GetComponentsInChildren<PixelButton>(true).Single(b => b.LabelText == "HOW TO PLAY").SimulateClick();
            Assert.That(m.HowTo.IsOpen, Is.True);
            Assert.That(m.transform.Find("Hub").gameObject.activeSelf, Is.False);
            Bb0102Tests.AssertTextFitsAndNoOverlap(m.HowTo.transform);
            foreach (var t in m.HowTo.GetComponentsInChildren<PixelText>().Where(t => HowToPlayView.Steps.Any(s => s.detail == t.Text || s.title == t.Text)))
                Assert.That(t.Bounds.xMax, Is.LessThanOrEqualTo(122f), $"'{t.Text}' inside its box");
            foreach (var t in HowToPlayView.Steps)
                Assert.That(t.detail.All(c => c != '='), Is.True, "the pixel font has no '='");
            foreach (var icon in m.HowTo.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "StepIcon"))
                Assert.That(icon.bounds.min.x, Is.GreaterThanOrEqualTo(-124f), $"{icon.sprite.name} inside its box");
            m.HowTo.StartButton.SimulateClick();
            Assert.That(started, Is.EqualTo(1), "START A RUN = RUN");
            m.HowTo.BackButton.SimulateClick();
            Assert.That(m.HowTo.IsOpen, Is.False);
            Assert.That(m.transform.Find("Hub").gameObject.activeSelf, Is.True);
        }

        [Test]
        public void CaptureMenu1Screens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_MENU1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_MENU1_OUT not set");
            Directory.CreateDirectory(dir);
            Directory.CreateDirectory(Path.Combine(dir, "anim"));
            var camGo = new GameObject("Cam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                void Shoot(string name, int[] zooms = null)
                {
                    foreach (var zoom in zooms ?? new[] { 1, 3 })
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
                m.SetRunInfo(new ProgressData(), null);
                m.Animate(0.35f);
                Shoot("1-hub-new-player");
                for (var f = 0; f < 24; f++)   // a short strip of frames (8 fps) to show the motion
                {
                    for (var k = 0; k < 8; k++) m.Backdrop.Step(1f / 64f);
                    m.Animate(1f + f / 8f);
                    Shoot($"anim/frame{f:00}", new[] { 1 });
                }
                var p = new ProgressData { RunsPlayed = 3 };
                var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
                run.Pick(0);
                m.SetRunInfo(p, run);
                m.Animate(2f);
                Shoot("2-hub-continue");
                m.HowTo.Show();
                m.transform.Find("Hub").gameObject.SetActive(false);
                Shoot("3-how-to-play");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
