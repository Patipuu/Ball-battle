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
    public class BbTut2Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Tutorial2Tests");
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void FreshProfileOffersEveryUnlockedBallAndPersistsTheTutorialRun()
        {
            var store = new MemorySaveStore();
            var flow = new RunFlow(store);
            flow.Open();
            flow.NewRun(123, true);
            Assert.That(flow.StartChoices, Is.EqualTo(UnlockRules.Weapons(new ProgressData())));
            flow.ChooseWeapon("blade");
            Assert.That(flow.Run.TutorialRun, Is.True);
            Assert.That(flow.Run.Seed, Is.EqualTo(RunState.TutorialSeed));
            Assert.That(flow.Run.Enemy.WeaponId, Is.EqualTo("brawler"));
            Assert.That(flow.Run.Enemy.MaxHp.ToFloat(), Is.EqualTo(60f));
            Assert.That(flow.Run.ArenaId, Is.EqualTo(BallBattle.Sim.ArenaRegistry.Classic));
            Assert.That(store.Load().Run.TutorialRun, Is.True);
            Assert.That(store.Load().Progress.Onboarding.MenuStage, Is.EqualTo(1));
        }

        [Test]
        public void HubCanEndSavedRunWithoutLosingOtherProgress()
        {
            var store = new MemorySaveStore();
            var flow = new RunFlow(store);
            flow.Open();
            flow.NewRun(42);
            flow.ChooseWeapon(flow.StartChoices[0]);
            flow.Close();
            var updated = store.Load();
            updated.Progress.Gold = 37;
            store.Save(updated);
            flow.AbandonSavedRun();
            Assert.That(store.Load().HasRun, Is.False);
            Assert.That(store.Load().Progress.RunsPlayed, Is.EqualTo(1));
            Assert.That(store.Load().Progress.Gold, Is.EqualTo(37));
            flow.Open();
            flow.NewRun(43);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.PickWeapon));
        }

        [Test]
        public void HubNewRunRequiresConfirmationAndBlocksTheOtherButtons()
        {
            var menu = MenuView.Create(root.transform, art);
            var progress = new ProgressData();
            menu.SetRunInfo(progress, RunState.Start(42, "blade", progress));
            var requested = 0;
            menu.NewRunRequested += () => requested++;
            menu.PressNewRun();
            Assert.That(menu.NewRunDialog.IsOpen, Is.True);
            Assert.That(menu.RunButton.enabled, Is.False);
            menu.NewRunDialog.Press(false);
            Assert.That(requested, Is.Zero);
            Assert.That(menu.RunButton.enabled, Is.True);
            menu.PressNewRun();
            menu.NewRunDialog.Press(true);
            Assert.That(requested, Is.EqualTo(1));
        }

        [Test]
        public void FightGearPausesAndSaveExitKeepsLockedFight()
        {
            var store = new MemorySaveStore();
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var controller = RunController.Create(root.transform, arena, store);
            controller.Open();
            controller.Flow.NewRun(42);
            controller.Flow.ChooseWeapon(controller.Flow.StartChoices[0]);
            controller.Flow.PickStart(0);
            controller.Flow.Fight();
            controller.Tick(0f);
            var overlay = controller.GetComponentInChildren<RunOverlayView>(true);
            overlay.transform.Find("FightGear").GetComponent<PixelButton>().SimulateClick();
            Assert.That(overlay.PauseOpen, Is.True);
            Assert.That(arena.Paused, Is.True);
            overlay.transform.Find("PausePanel/SaveExit").GetComponent<PixelButton>().SimulateClick();
            Assert.That(controller.Flow.Current, Is.EqualTo(RunFlow.State.Closed));
            Assert.That(store.Load().HasRun, Is.True);
            Assert.That(store.Load().Run.FightLocked, Is.True);
        }

        [Test]
        public void HintsDoNotDisableAnyScreenButtons()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            rc.Open();
            rc.Flow.NewRun(123, true);
            rc.Tick(1f / 60f);
            Assert.That(rc.Hints.CurrentId, Is.EqualTo("pick"));
            Assert.That(rc.Pick.GetComponentsInChildren<PixelButton>(true).Any(b => b.enabled), Is.True);
            Assert.That(rc.Hints.GetComponentsInChildren<PixelButton>(true), Is.Empty);
            rc.Flow.ChooseWeapon("blade");
            rc.Tick(1f / 60f);
            Assert.That(rc.Hints.CurrentId, Is.EqualTo("startpick"));
            rc.Flow.MarkHintSeen("startpick");
            Assert.That(rc.Flow.HintSeen("startpick"), Is.True);
        }

        [Test]
        public void FirstSkillPromptStopsSlowingTheFightWhenThePlayerDoesNotTap()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            rc.Open();
            rc.Flow.NewRun(123, true);
            rc.Flow.ChooseWeapon("blade");
            rc.Flow.PickStart(0);
            rc.Flow.Fight();
            rc.Tick(3f);
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.Playing));
            for (var frame = 0; frame < 300 && rc.Flow.Current == RunFlow.State.Playing &&
                 !arena.Sim.Balls[0].Tap.Ready; frame++)
            {
                arena.TimeScale = 2f;
                arena.Advance(1f / 60f);
                rc.Tick(1f / 60f);
            }
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.Playing));
            Assert.That(arena.Sim.Balls[0].Tap.Ready, Is.True);
            rc.Tick(1f / 60f);
            Assert.That(arena.TimeScale, Is.EqualTo(0.35f));
            rc.Tick(RunController.TutorialSlowLimitSeconds + 0.1f);
            Assert.That(arena.TimeScale, Is.EqualTo(0.35f));
            rc.Tick(0f);
            Assert.That(arena.TimeScale, Is.EqualTo(rc.ChosenSpeed), "back to the player's X1/X2 choice");
            Assert.That(rc.Tap.TutorialLesson, Is.True);
        }

        [Test]
        public void FirstWreckerChargeExplainsTheSecondTapUntilThePlayerRams()
        {
            var store = new MemorySaveStore();
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, store);
            rc.Open();
            rc.Flow.NewRun(123, true);
            rc.Flow.ChooseWeapon("brawler");
            rc.Flow.PickStart(0);
            rc.Flow.Fight();
            rc.Tick(3f);
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.Playing));
            for (var frame = 0; frame < 600 && rc.Flow.Current == RunFlow.State.Playing &&
                 !arena.Sim.Balls[0].Tap.Ready; frame++)
            {
                arena.TimeScale = 2f;
                arena.Advance(1f / 60f);
                rc.Tick(1f / 60f);
            }
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.Playing));
            Assert.That(arena.Sim.Balls[0].Tap.Ready, Is.True);
            Assert.That(rc.Tap.Request(), Is.True);
            arena.TimeScale = 1f;
            arena.Advance(1f / 60f);
            rc.Tick(1f / 60f);
            Assert.That(rc.Tap.Prompt.gameObject.activeSelf, Is.True);
            Assert.That(rc.Tap.Prompt.Text, Is.EqualTo(PixelFont.Normalize(UiVietnamese.Translate("WAIT FOR THE RING TO FILL"))));
            Assert.That(rc.Flow.HintSeen("wrecker_release"), Is.False);

            var charge = arena.Sim.Balls[0].Skill.Act as BallBattle.Sim.Skills.WreckerCharge;
            Assert.That(charge, Is.Not.Null);
            for (var frame = 0; frame < 180 && rc.Flow.Current == RunFlow.State.Playing &&
                 charge.Charge.ToFloat() < 0.8f; frame++)
            {
                arena.Advance(1f / 60f);
                rc.Tick(1f / 60f);
            }
            Assert.That(charge.Charging, Is.True);
            Assert.That(charge.Charge.ToFloat(), Is.GreaterThanOrEqualTo(0.8f));
            Assert.That(rc.Tap.Prompt.Text, Is.EqualTo(PixelFont.Normalize(UiVietnamese.Translate("TAP AGAIN TO RAM!"))));
            Assert.That(rc.Tap.Request(), Is.True);
            arena.Advance(1f / 60f);
            rc.Tick(1f / 60f);
            Assert.That(rc.Flow.HintSeen("wrecker_release"), Is.True);
            Assert.That(store.Load().Progress.Onboarding.HasSeen("wrecker_release"), Is.True);
            Assert.That(rc.Tap.Prompt.gameObject.activeSelf, Is.False);
        }

        [Test]
        public void WreckerChargePromptsFitInBothLanguages()
        {
            var before = StringTable.Current;
            try
            {
                foreach (var language in new[] { Lang.En, Lang.Vi })
                {
                    StringTable.Current = language;
                    foreach (var line in new[] { "WAIT FOR THE RING TO FILL", "TAP AGAIN TO RAM!",
                        "TAP NOW TO USE YOUR SKILL!" })
                        Assert.That(PixelFont.MeasureWidth(UiVietnamese.Translate(line)), Is.LessThanOrEqualTo(250), language + " " + line);
                }
            }
            finally { StringTable.Current = before; }
        }

        [Test]
        public void EndOfRunHintNamesAnActionThePlayerCanTake()
        {
            var progress = new ProgressData();
            Assert.That(HintPointer.NextStepLine(progress), Is.EqualTo("NEXT: PLAY AGAIN FOR MORE SHARDS"));

            var ball = BallBattle.Sim.Meta.MetaRules.Ball(progress, "blade");
            ball.Shards = BallBattle.Sim.Meta.MetaRules.ShardsToNext(ball.Level);
            progress.Gold = BallBattle.Sim.Meta.MetaRules.GoldToNext(ball.Level);
            Assert.That(HintPointer.NextStepLine(progress), Is.EqualTo("NEXT: UPGRADE A BALL IN BALLS"));

            progress.PendingChests.Add(BallBattle.Sim.Meta.ChestTier.Wood);
            Assert.That(HintPointer.NextStepLine(progress), Is.EqualTo("NEXT: OPEN YOUR CHEST ON MENU"));
        }

        [Test]
        public void MenuRevealsPagesAsTheFirstRunProgresses()
        {
            var store = new MemorySaveStore();
            var menu = MenuView.Create(root.transform, art);
            menu.Store = store;
            menu.SetRunInfo(store.Load().Progress, null);
            Assert.That(menu.MenuStage, Is.Zero);
            Assert.That(menu.BallsButton.gameObject.activeSelf, Is.False);
            Assert.That(menu.VersusButton.gameObject.activeSelf, Is.False);
            Assert.That(menu.CodexButton.gameObject.activeSelf, Is.False);
            var data = store.Load();
            data.Progress.Onboarding.AdvanceTo(1);
            store.Save(data);
            menu.SetRunInfo(data.Progress, null);
            Assert.That(menu.BallsButton.gameObject.activeSelf, Is.True);
            Assert.That(menu.VersusButton.gameObject.activeSelf, Is.False);
            data.Progress.Onboarding.AdvanceTo(2);
            store.Save(data);
            menu.SetRunInfo(data.Progress, null);
            Assert.That(menu.VersusButton.gameObject.activeSelf, Is.True);
            Assert.That(menu.CodexButton.gameObject.activeSelf, Is.True);
            menu.ShowVersus();
            Assert.That(store.Load().Progress.Onboarding.HasSeen("versus"), Is.True);
        }

        [Test]
        public void SettingsCanPersistentlyTurnHintsOff()
        {
            var store = new MemorySaveStore();
            var menu = MenuView.Create(root.transform, art);
            menu.Store = store;
            menu.ShowSettings();
            var toggle = menu.Settings.GetComponentsInChildren<PixelButton>(true).Single(b => b.name == "Hints");
            toggle.SimulateClick();
            Assert.That(store.Load().Progress.Onboarding.HintsOff, Is.True);
            toggle.SimulateClick();
            Assert.That(store.Load().Progress.Onboarding.HintsOff, Is.False);
        }

        [Test]
        public void VersionFiveSaveMigratesWithoutDroppingItsRun()
        {
            var old = new SaveData { Version = 5, HasRun = true };
            old.Progress.RunsPlayed = 0;
            old.Progress.SeenTips.Add("pick");
            var migrated = SaveMigration.Upgrade(old);
            Assert.That(migrated.Version, Is.EqualTo(6));
            Assert.That(migrated.HasRun, Is.True);
            Assert.That(migrated.Progress.Onboarding.MenuStage, Is.EqualTo(1));
            Assert.That(migrated.Progress.Onboarding.HasSeen("pick"), Is.True);
        }

        [Test]
        public void EveryHintFitsInEnglishAndVietnamese()
        {
            var before = StringTable.Current;
            try
            {
                foreach (var language in new[] { Lang.En, Lang.Vi })
                {
                    StringTable.Current = language;
                    foreach (var hint in HintPointer.Hints)
                    {
                        var rendered = UiVietnamese.Translate(hint.Line);
                        Assert.That(PixelFont.MeasureWidth(rendered), Is.LessThanOrEqualTo(250), hint.Id + " " + language);
                        Assert.That(hint.Label.x, Is.InRange(-125, 125));
                        Assert.That(hint.Label.y, Is.InRange(-230, 220));
                    }
                    foreach (var line in new[] { "NEXT: OPEN YOUR CHEST ON MENU", "NEXT: PLAY AGAIN FOR MORE SHARDS" })
                        Assert.That(PixelFont.MeasureWidth(UiVietnamese.Translate(line)), Is.LessThanOrEqualTo(250), language + " " + line);
                }
            }
            finally { StringTable.Current = before; }
        }

        [Test]
        public void CaptureFirstStepsInBothLanguages()
        {
            var folder = Environment.GetEnvironmentVariable("BB_TUT2_OUT");
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("BB_TUT2_OUT not set");
            Directory.CreateDirectory(folder);
            var previous = StringTable.Current;
            var cameraObject = new GameObject("TutorialCamera");
            try
            {
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 240;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Palette.Background;
                cameraObject.transform.position = new Vector3(0, 0, -10);
                foreach (var lang in new[] { Lang.En, Lang.Vi })
                {
                    StringTable.Current = lang;
                    var scene = new GameObject("TutorialScene");
                    scene.transform.SetParent(root.transform, false);
                    try
                    {
                        var store = new MemorySaveStore();
                        var menu = MenuView.Create(scene.transform, art);
                        menu.Store = store;
                        menu.SetRunInfo(store.Load().Progress, null);
                        menu.Show(true);
                        menu.Animate(Time.unscaledTime + 4f);
                        Capture(camera, folder, lang + "-0-menu");
                        menu.Show(false);
                        var arena = scene.AddComponent<ArenaView>();
                        arena.Art = art;
                        arena.AutoRestart = false;
                        var rc = RunController.Create(scene.transform, arena, store);
                        rc.Open();
                        rc.Flow.NewRun(17, true);
                        rc.Tick(1f / 60f);
                        Capture(camera, folder, lang + "-1-pick");
                        rc.Flow.ChooseWeapon("blade");
                        rc.Tick(1f / 60f);
                        Capture(camera, folder, lang + "-2-card");
                        rc.Flow.PickStart(0);
                        rc.Tick(1f / 60f);
                        Capture(camera, folder, lang + "-3-prepare");
                        rc.Flow.Fight();
                        rc.Tick(3f);
                        for (var frame = 0; frame < 600 && rc.Flow.Current == RunFlow.State.Playing &&
                             !arena.Sim.Balls[0].Tap.Ready; frame++)
                        {
                            arena.TimeScale = 2f;
                            arena.Advance(1f / 60f);
                            rc.Tick(1f / 60f);
                        }
                        Assert.That(arena.Sim.Balls[0].Tap.Ready, Is.True);
                        rc.Tick(1f / 60f);
                        Assert.That(rc.Tap.Prompt.gameObject.activeSelf, Is.True);
                        Capture(camera, folder, lang + "-4-strike");
                        rc.Tap.Request();
                        arena.TimeScale = 4f;
                        var frames = 0;
                        while (rc.Flow.Current != RunFlow.State.FightResult && frames++ < 60 * 120)
                        {
                            rc.Tick(1f / 60f);
                            arena.Advance(1f / 60f);
                        }
                        Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.FightResult));
                        rc.Tick(0.5f);
                        Capture(camera, folder, lang + "-5-result");
                    }
                    finally { Object.DestroyImmediate(scene); }
                }
            }
            finally { StringTable.Current = previous; Object.DestroyImmediate(cameraObject); }
        }

        static void Capture(Camera camera, string folder, string name)
        {
            var rt = new RenderTexture(270, 480, 24) { filterMode = FilterMode.Point };
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            var image = new Texture2D(270, 480, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 270, 480), 0, 0);
            image.Apply();
            File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            RenderTexture.active = null;
            camera.targetTexture = null;
            Object.DestroyImmediate(image);
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
