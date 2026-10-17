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
    /// <summary>UI-TUT1: first-run tips, one at a time, in order, saved, skippable, never after the first run.</summary>
    [Ignore("TUT-2 replaced the blocking TutorialCoach; coverage is in BbTut2Tests.")]
    public class BbTut1Tests
    {
        const float Frame = 1f / 60f;
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbTut1Root");
            TutorialCoach.Enabled = true;
        }

        [TearDown]
        public void TearDown()
        {
            TutorialCoach.Enabled = false;   // TipsOffForTests: off for every other test
            Object.DestroyImmediate(root);
        }

        (ArenaView arena, RunController rc, MemorySaveStore store) Rig(string json = null)
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var store = new MemorySaveStore { Json = json };
            var rc = RunController.Create(root.transform, arena, store);
            rc.Open();
            return (arena, rc, store);
        }

        static void Step(ArenaView arena, RunController rc) { rc.Tick(Frame); arena.Advance(Frame); }

        static string Choice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        [Test]
        public void FirstRunShowsEveryTipInOrderAndBlocksTheScreenUntilOk()
        {
            var (arena, rc, store) = Rig();
            var flow = rc.Flow;
            flow.NewRun(7);
            Step(arena, rc);
            Assert.That(rc.Coach.Current?.Id, Is.EqualTo("pick"));
            Assert.That(rc.Pick.GetComponentsInChildren<PixelButton>(true).All(b => !b.enabled), Is.True, "ball tiles and PICK are off under the tip");
            rc.Coach.OkButton.SimulateClick();
            Assert.That(rc.Coach.Showing, Is.False);
            Assert.That(rc.Pick.GetComponentsInChildren<PixelButton>(true).Any(b => b.enabled), Is.True, "buttons back");
            Assert.That(flow.TipSeen("pick"), Is.True);

            flow.ChooseWeapon(Choice(7));
            Step(arena, rc);
            Assert.That(rc.Coach.Current?.Id, Is.EqualTo("startpick"));
            rc.Coach.OkButton.SimulateClick();
            flow.PickStart(0);
            Step(arena, rc);
            foreach (var id in new[] { "enemy", "cards", "fight" })
            {
                Assert.That(rc.Coach.Current?.Id, Is.EqualTo(id), "Prepare tips one after the other");
                rc.Coach.OkButton.SimulateClick();
                Step(arena, rc);
            }
            Assert.That(rc.Coach.Showing, Is.False);

            rc.Prepare.PressFight();
            if (rc.Prepare.ConfirmOpen) rc.Prepare.PressConfirm(true);
            arena.TimeScale = 4f;
            var guard = 0;
            while (flow.Current != RunFlow.State.FightResult && guard++ < 60 * 300) Step(arena, rc);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.FightResult));
            Assert.That(rc.Coach.Current?.Id, Is.EqualTo("result"));
            rc.Coach.OkButton.SimulateClick();
            Assert.That(TutorialCoach.Tips.All(t => flow.TipSeen(t.Id)), Is.True);
            Assert.That(store.Json, Does.Contain("\"SeenTips\""), "saved");
        }

        [Test]
        public void SkipTipsEndsTheTutorialForGood()
        {
            var (arena, rc, store) = Rig();
            rc.Flow.NewRun(7);
            Step(arena, rc);
            rc.Coach.SkipButton.SimulateClick();
            Assert.That(rc.Coach.Showing, Is.False);
            Assert.That(TutorialCoach.Tips.All(t => rc.Flow.TipSeen(t.Id)), Is.True);
            rc.Flow.ChooseWeapon(Choice(7));
            Step(arena, rc);
            Assert.That(rc.Coach.Showing, Is.False, "no tip after SKIP TIPS");
            var reloaded = store.Load();
            Assert.That(TutorialCoach.Tips.All(t => reloaded.Progress.SeenTips.Contains(t.Id)), Is.True, "kept in the save");
        }

        [Test]
        public void NoTipsOnceARunWasPlayedOrWhenOff()
        {
            var (arena, rc, _) = Rig("{\"Version\":2,\"Progress\":{\"Achievements\":[],\"RunsPlayed\":1,\"RunsWon\":0,\"BestFight\":3},\"HasRun\":false}");
            rc.Flow.NewRun(7);
            Step(arena, rc);
            Assert.That(rc.Coach.Showing, Is.False, "old player");

            Object.DestroyImmediate(root); root = new GameObject("BbTut1Root2");
            TutorialCoach.Enabled = false;
            var (arena2, rc2, _) = Rig();
            rc2.Flow.NewRun(7);
            Step(arena2, rc2);
            Assert.That(rc2.Coach.Showing, Is.False, "-bbNoTips");
        }

        [Test]
        public void OldSaveWithoutSeenTipsLoads()
        {
            var store = new MemorySaveStore { Json = "{\"Version\":2,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":0,\"RunsWon\":0,\"BestFight\":2},\"HasRun\":false}" };
            var data = store.Load();
            Assert.That(data.Progress.SeenTips, Is.Not.Null.And.Empty);
            Assert.That(data.Progress.Has("boss1"), Is.True);
        }

        [Test]
        public void EveryTipBoxFitsTheScreenAndKeepsItsTargetVisible()
        {
            var (_, rc, _) = Rig();
            foreach (var tip in TutorialCoach.Tips)
            {
                var box = TutorialCoach.BoxFor(tip);
                Assert.That(box.xMin >= -135 && box.xMax <= 135 && box.yMin >= -240 && box.yMax <= 240, Is.True, $"{tip.Id}: on screen");
                Assert.That(box.Overlaps(tip.Target), Is.False, $"{tip.Id}: box clear of what it points at");
                var layout = rc.Coach.transform.Find("Tip_" + tip.Id);
                foreach (var t in layout.GetComponentsInChildren<PixelText>(true).Where(t => t.Text.Length > 0))
                {
                    var b = t.Bounds;
                    Assert.That(b.xMin >= box.xMin + 2 && b.xMax <= box.xMax - 2 && b.yMin >= box.yMin && b.yMax <= box.yMax, Is.True, $"{tip.Id}: '{t.Text}' {b} in {box}");
                }
                var texts = layout.GetComponentsInChildren<PixelText>(true).Where(t => t.Text.Length > 0).ToList();
                foreach (var btn in layout.GetComponentsInChildren<PixelButton>(true))
                    foreach (var t in texts.Where(t => !t.transform.IsChildOf(btn.transform)))
                        Assert.That(btn.Area.Overlaps(t.Bounds), Is.False, $"{tip.Id}: '{t.Text}' under {btn.name}");
            }
        }

        [Test]
        public void CaptureTut1Screens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_TUT1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_TUT1_OUT not set");
            Directory.CreateDirectory(dir);
            var (arena, rc, _) = Rig();
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
                var flow = rc.Flow;
                flow.NewRun(7);
                Step(arena, rc);
                Shoot("1-tip-pick");
                rc.Coach.OkButton.SimulateClick();
                flow.ChooseWeapon(Choice(7));
                Step(arena, rc);
                Shoot("2-tip-startpick");
                rc.Coach.OkButton.SimulateClick();
                flow.PickStart(0);
                Step(arena, rc);
                Shoot("3-tip-enemy"); rc.Coach.OkButton.SimulateClick(); Step(arena, rc);
                Shoot("4-tip-cards"); rc.Coach.OkButton.SimulateClick(); Step(arena, rc);
                Shoot("5-tip-fight"); rc.Coach.OkButton.SimulateClick(); Step(arena, rc);
                rc.Prepare.PressFight();
                if (rc.Prepare.ConfirmOpen) rc.Prepare.PressConfirm(true);
                arena.TimeScale = 4f;
                var guard = 0;
                while (flow.Current != RunFlow.State.FightResult && guard++ < 60 * 300) Step(arena, rc);
                Step(arena, rc);
                Shoot("6-tip-result");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
