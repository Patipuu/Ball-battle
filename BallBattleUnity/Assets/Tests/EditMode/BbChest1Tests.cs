using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>BB-12b: CHEST screen (animation, rows, gold, odds), shown after a run / for an abandoned run, welcome chests on the menu.</summary>
    public class BbChest1Tests
    {
        const float Frame = 1f / 60f;
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbChest1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        (ChestView view, ProgressData p, ChestResult r) Open(ChestTier tier, uint seed = 5)
        {
            var p = new ProgressData();
            var r = MetaRules.Open(p, tier, "blade", seed);
            var v = ChestView.Create(root.transform, art);
            v.Show(r, p, "RUN WON");
            return (v, p, r);
        }

        static void Finish(ChestView v)
        {
            for (var i = 0; i < 300 && !v.Revealed; i++) v.Tick(Frame);
        }

        [Test]
        public void TheChestShakesOpensThenRevealsEveryRow()
        {
            var (v, _, r) = Open(ChestTier.Gold);
            Assert.That(v.TitleText, Is.EqualTo("GOLD CHEST"));
            Assert.That((v.LidOpen, v.Revealed, v.RowsVisible), Is.EqualTo((false, false, 0)));
            Assert.That(v.CollectButton.gameObject.activeSelf, Is.False, "COLLECT after the reveal");
            v.Tick(ChestView.ShakeSeconds + 0.01f);
            Assert.That(v.LidOpen, Is.True);
            Finish(v);
            Assert.That(v.RowsVisible, Is.EqualTo(Math.Min(r.Shards.Count, ChestView.MaxRows)));
            Assert.That(v.RowTexts[0], Does.StartWith("EMBER +"));
            Assert.That(v.GoldText, Is.EqualTo("+" + r.TotalGold + " GOLD"));
            Assert.That(v.CollectButton.gameObject.activeSelf, Is.True);
        }

        [Test]
        public void ATapSkipsTheAnimationAndCollectCloses()
        {
            var (v, _, _) = Open(ChestTier.Wood);
            v.SkipButton.SimulateClick();
            Assert.That(v.Revealed, Is.True);
            var collected = 0;
            v.Collected += () => collected++;
            v.CollectButton.SimulateClick();
            Assert.That((v.IsOpen, collected), Is.EqualTo((false, 1)));
        }

        [Test]
        public void RowsShowProgressTowardTheNextLevel()
        {
            var p = new ProgressData();
            MetaRules.Ball(p, "blade").Shards = 3;
            p.Gold = 1000;
            var r = MetaRules.Open(p, ChestTier.Wood, "blade", 2);
            var v = ChestView.Create(root.transform, art);
            v.Show(r, p, "");
            Finish(v);
            Assert.That(v.RowTexts[0], Does.EndWith("UP!"), "3 + 4 shards >= 5 and enough gold: can level now");
            MetaRules.Ball(p, "fang").Level = MetaTuning.MaxLevel;
            var maxed = MetaRules.Open(p, ChestTier.Gold, "fang", 3);
            v.Show(maxed, p, "");
            Finish(v);
            Assert.That(v.RowTexts[0], Does.EndWith("MAX"));
        }

        [Test]
        public void OddsPanelShowsTheRealTableAndFitsTheScreen()
        {
            var lines = ChestView.OddsLines(4);
            Assert.That(lines, Does.Contain($"WOOD: {MetaTuning.PlayedShards[0]} + {MetaTuning.RandomShards[0]} SHARDS  {MetaTuning.ChestGold[0]} GOLD"));
            Assert.That(lines.Any(l => l.Contains("1 IN 4")), Is.True);
            foreach (var l in lines) Assert.That(PixelFont.MeasureWidth(l), Is.LessThanOrEqualTo(240), l);
            var (v, _, _) = Open(ChestTier.Silver);
            v.OddsButton.SimulateClick();
            Assert.That(v.OddsOpen, Is.True);
            Assert.That(v.CollectButton.enabled || v.SkipButton.enabled, Is.False, "buttons under the panel are off");
            Assert.That(v.OddsText.Count, Is.EqualTo(lines.Count(l => l.Length > 0)));
            v.ShowOdds(false);
            Assert.That(v.OddsOpen, Is.False);
        }

        [Test]
        public void EverythingOnTheChestScreenStaysOnScreen()
        {
            foreach (var tier in new[] { ChestTier.Wood, ChestTier.Silver, ChestTier.Gold })
            {
                var (v, _, _) = Open(tier, 11);
                Finish(v);
                foreach (var t in v.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Length > 0))
                {
                    var b = t.Bounds;
                    Assert.That(b.xMin >= -135 && b.xMax <= 135 && b.yMin >= -240 && b.yMax <= 240, Is.True, $"{tier}: '{t.Text}' {b}");
                    Assert.That(b.Overlaps(ChestView.CollectArea) && !t.transform.IsChildOf(v.CollectButton.transform), Is.False, $"'{t.Text}' under COLLECT");
                }
                Object.DestroyImmediate(v.gameObject);
            }
        }

        [Test]
        public void AnAbandonedRunsChestOpensOverTheStartPickAndBlocksIt()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var progress = new ProgressData();
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(progress))[0], progress);
            run.History.Add(new FightRecord { FightIndex = 0, Won = true, EnemyWeapon = "fang", HpAfter = run.Build.Hp });
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = RunSave.From(run) });
            var rc = RunController.Create(root.transform, arena, store);
            rc.Open();
            rc.Flow.NewRun(9);
            rc.Tick(Frame);
            rc.Flow.ChooseWeapon(rc.Flow.StartChoices[0]);
            rc.Tick(Frame);
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.StartPick));
            Assert.That(rc.Chest.IsOpen, Is.True);
            Assert.That(rc.Chest.TitleText, Is.EqualTo("WOOD CHEST"));
            Assert.That(rc.StartPick.GetComponentsInChildren<PixelButton>(true).All(b => !b.enabled), Is.True, "start pick blocked");
            rc.Chest.SkipButton.SimulateClick();
            rc.Chest.CollectButton.SimulateClick();
            rc.Tick(Frame);
            Assert.That(rc.Chest.IsOpen, Is.False);
            Assert.That(rc.Flow.ChestToShow, Is.Null);
            Assert.That(rc.StartPick.GetComponentsInChildren<PixelButton>(true).Any(b => b.enabled), Is.True, "start pick back");
            rc.Tick(Frame);
            Assert.That(rc.Chest.IsOpen, Is.False, "shown once");
        }

        [Test]
        public void MenuShowsTheChestButtonOnlyWhileChestsWait()
        {
            var m = MenuView.Create(root.transform, art);
            m.SetRunInfo(new ProgressData(), null);
            Assert.That(m.ChestButton.gameObject.activeSelf, Is.False);
            var p = new ProgressData { RunsPlayed = 2, RunsWon = 2 };
            MetaRules.GrantWelcomeChests(p);
            m.SetRunInfo(p, null);
            Assert.That(m.ChestButton.gameObject.activeSelf, Is.True);
            Assert.That(m.ChestButton.LabelText, Is.EqualTo("CHESTS X2"));
            Assert.That(MenuView.ChestLabel(1), Is.EqualTo("OPEN CHEST"));
            foreach (var b in m.Hub.GetComponentsInChildren<PixelButton>(true).Where(b => b != m.ChestButton && b.gameObject.activeSelf))
                Assert.That(b.Area.Overlaps(MenuView.ChestArea), Is.False, b.name);
            foreach (var t in m.Hub.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Length > 0 && !t.transform.IsChildOf(m.ChestButton.transform)))
                Assert.That(t.Bounds.Overlaps(MenuView.ChestArea), Is.False, t.Text);
        }

        [Test]
        public void CaptureChestScreens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_CHEST1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_CHEST1_OUT not set");
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
                var (v, _, _) = Open(ChestTier.Wood, 3);
                v.Tick(0.2f);
                Shoot("1-wood-closed");
                Finish(v);
                Shoot("2-wood-open");
                Object.DestroyImmediate(v.gameObject);
                (v, _, _) = Open(ChestTier.Silver, 4);
                Finish(v);
                Shoot("3-silver");
                Object.DestroyImmediate(v.gameObject);
                (v, _, _) = Open(ChestTier.Gold, 6);
                Finish(v);
                Shoot("4-gold");
                v.OddsButton.SimulateClick();
                Shoot("5-odds");
                Object.DestroyImmediate(v.gameObject);
                var m = MenuView.Create(root.transform, art);
                var p = new ProgressData { RunsWon = 3, RunsPlayed = 4 };
                MetaRules.GrantWelcomeChests(p);
                m.SetRunInfo(p, null);
                Shoot("6-menu-chest-button");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
