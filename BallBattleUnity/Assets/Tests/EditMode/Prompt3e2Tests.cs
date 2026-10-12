using System.Collections.Generic;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// Prompt 3e.2: click-through across input locks (both input sources), Prepare lock 0.6 s, SWAP and ABANDON
    /// confirmations, and the rules every confirm dialog follows (0.3 s lock, YES/NO off the area that opened it, spam-proof).
    /// </summary>
    public class Prompt3e2Tests
    {
        GameObject root;
        ArtLibrary art;
        int frame;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("P3e2Root");
            PixelInput.SetTestClock(0f);
            PixelInput.ResetCapture();
            frame = 100;
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.ResetCapture();
            PixelInput.SetTestClock(null);
            Object.DestroyImmediate(root);
        }

        public enum Source { Polling, Imgui }

        static void Wait(float s) => PixelInput.AdvanceTestClock(s);
        void Dispatch() => PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));

        void Press(Source src, Vector2 at)
        {
            frame++;
            if (src == Source.Polling) PixelInput.PollFrame(frame, true, false, at);
            else PixelInput.GuiEvent(frame, true, at);
            Dispatch();
        }

        void Release(Source src, Vector2 at)
        {
            frame++;
            if (src == Source.Polling) PixelInput.PollFrame(frame, false, true, at);
            else PixelInput.GuiEvent(frame, false, at);
            Dispatch();
        }

        void Click(Source src, Vector2 at)
        {
            Press(src, at);
            Release(src, at);
        }

        static string FirstChoice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        // ---- 3. Pressed while locked, released after: no click; the next click works ----

        [TestCase(Source.Polling)]
        [TestCase(Source.Imgui)]
        public void APressDuringALockNeverClicksAndTheNextClickWorks(Source src)
        {
            var clicks = 0;
            PixelButton.Create(root.transform, "B", art, new Rect(-20, -20, 40, 40), "B", 1, Palette.Floor, Palette.Wall, Palette.Text, 0).Clicked += () => clicks++;
            PixelInput.BlockFor(0.3f);   // a screen just opened
            Wait(0.1f);
            Press(src, Vector2.zero);
            Wait(0.3f);
            Release(src, Vector2.zero);
            Assert.That(clicks, Is.Zero, "pressed while locked, released after: not a click");
            Click(src, Vector2.zero);
            Assert.That(clicks, Is.EqualTo(1), "the next normal click: exactly one");
        }

        [TestCase(Source.Polling)]
        [TestCase(Source.Imgui)]
        public void PrepareLocksForSixHundredMillisecondsAndAPressInsideNeverFights(Source src)
        {
            Assert.That(PrepareView.EntryInputBlockSeconds, Is.EqualTo(0.6f));
            var run = RunState.Start(80, FirstChoice(80), new ProgressData());
            run.Pick(0);   // CHOOSE YOUR START does not consume Prepare's free card.
            run.Coins = 10;
            run.OfferCards.Clear();
            foreach (var c in new[] { new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp) }) run.OfferCards.Add(OfferSlot.From(c));
            run.Pick(0);   // free pick used: FIGHT starts at once
            var v = PrepareView.Create(root.transform, art);
            var fights = 0;
            v.FightRequested += () => fights++;
            v.Show(run);
            Wait(0.3f);
            Press(src, PrepareView.FightArea.center);   // inside the 0.6 s lock
            Wait(0.4f);
            Release(src, PrepareView.FightArea.center);   // 0.7 s: after the lock
            Assert.That(fights, Is.Zero);
            Click(src, PrepareView.FightArea.center);
            Assert.That(fights, Is.EqualTo(1));
        }

        [TestCase(Source.Polling)]
        [TestCase(Source.Imgui)]
        public void ResultScreenLockOfEightHundredMillisecondsWorksTheSameWay(Source src)
        {
            var view = RunResultView.Create(root.transform, art);
            var pressed = 0;
            view.ContinueRequested += () => pressed++;
            view.Hide();
            view.Show(new FightReport { EnemyWeaponId = FangRule.WeaponId, FightNumber = 1, HpLeft = 40f, MaxHpInFight = 100f, Outcome = FightOutcome.Win, NextArenaId = "classic", NextWeaponId = BladeRule.WeaponId, NextEnemyHp = 100f, HpNext = 70f, MaxHpNext = 100f });
            Assert.That(RunResultView.EntryInputBlockSeconds, Is.EqualTo(0.8f));
            Wait(0.5f);
            Press(src, RunResultView.ContinueArea.center);
            Wait(0.4f);
            Release(src, RunResultView.ContinueArea.center);
            Assert.That(pressed, Is.Zero);
            Click(src, RunResultView.ContinueArea.center);
            Assert.That(pressed, Is.EqualTo(1));
        }

        // ---- 5. SWAP asks first ----

        (PrepareView view, RunState run, List<int> picks) SwapAt(int slot)
        {
            var run = RunState.Start(81, FirstChoice(81), new ProgressData());
            run.Coins = 10;
            var other = WeaponRegistry.All.First(w => w.Id != run.Build.WeaponId).Id;
            run.OfferCards.Clear();
            for (var i = 0; i < 3; i++)
                run.OfferCards.Add(OfferSlot.From(i == slot ? new Card(CardKind.SwapWeapon, other) : new Card(i == 0 || (slot == 0 && i == 1) ? CardKind.DamageUp : CardKind.MaxHp)));
            var v = PrepareView.Create(root.transform, art);
            var picks = new List<int>();
            v.CardPicked += picks.Add;
            v.Show(run);
            Wait(0.65f);
            return (v, run, picks);
        }

        [Test]
        public void SwapAsksSwapToTheNewNameAndNoCostsNothing()
        {
            var (v, run, picks) = SwapAt(1);
            var to = run.OfferCard(1).Id;
            var coins = run.Coins;
            Click(Source.Imgui, PrepareView.CardArea(1).center);
            Assert.That(v.ConfirmOpen, Is.True, "a SWAP card asks first");
            Assert.That(v.Dialog.Question, Is.EqualTo("SWAP TO " + WeaponNames.Of(to) + "?"));
            Assert.That(picks, Is.Empty);
            Wait(0.35f);
            Click(Source.Imgui, v.Dialog.NoArea.center);
            Assert.That(v.ConfirmOpen, Is.False);
            Assert.That(picks, Is.Empty, "NO: not picked");
            Assert.That((run.Coins, run.PicksTaken), Is.EqualTo((coins, 0)), "NO costs nothing");

            Click(Source.Imgui, PrepareView.CardArea(1).center);
            Wait(0.35f);
            Click(Source.Imgui, v.Dialog.YesArea.center);
            Assert.That(picks, Is.EqualTo(new[] { 1 }), "YES picks it");

            Click(Source.Imgui, PrepareView.CardArea(0).center);
            Assert.That(v.ConfirmOpen, Is.False, "other cards: one click as before");
            Assert.That(picks, Is.EqualTo(new[] { 1, 0 }));
        }

        // ---- 6. NEW RUN over a saved run asks first ----

        static RunFlow FlowWithSavedRun(MemorySaveStore store)
        {
            var f = new RunFlow(store);
            f.Open();
            f.NewRun(82);
            f.ChooseWeapon(f.StartChoices[0]);
            f.Close();
            var back = new RunFlow(store);
            back.Open();
            Assert.That(back.HasSavedRun, Is.True);
            return back;
        }

        [Test]
        public void NewRunOverASavedRunAsksAbandonAndOnlyYesStartsOver()
        {
            var store = new MemorySaveStore();
            var flow = FlowWithSavedRun(store);
            var title = RunTitleView.Create(root.transform, art);
            var starts = 0;
            title.NewRunRequested += () => starts++;
            title.Show(flow);
            title.PressNewRun();
            Assert.That(title.DialogOpen, Is.True);
            Assert.That(title.Dialog.Question, Is.EqualTo("ABANDON CURRENT RUN?"));
            title.Dialog.Press(false);
            Assert.That(starts, Is.Zero, "NO keeps the saved run");
            Assert.That(flow.HasSavedRun, Is.True);
            title.PressNewRun();
            title.Dialog.Press(true);
            Assert.That(starts, Is.EqualTo(1), "YES starts a new run (RunController then overwrites the save)");

            var fresh = new RunFlow(new MemorySaveStore());
            fresh.Open();
            title.Show(fresh);
            title.PressNewRun();
            Assert.That(title.DialogOpen, Is.False, "no saved run: no question");
            Assert.That(starts, Is.EqualTo(2));
        }

        // ---- 7. Every dialog: 0.3 s lock, YES/NO off the area that opened it, spam-proof ----

        static readonly Rect ScreenRect = new Rect(-135, -240, 270, 480);

        [Test]
        public void YesAndNoNeverOverlapTheAreaThatOpenedTheDialog()
        {
            var openers = new List<(string name, Rect area)> { ("FIGHT", PrepareView.FightArea), ("NEW RUN", RunTitleView.NewRunArea) };
            for (var i = 0; i < 3; i++) openers.Add(("SWAP card " + (i + 1), PrepareView.CardArea(i)));
            foreach (var (name, area) in openers)
            {
                var l = ConfirmDialog.LayoutFor(area);
                Assert.That(ConfirmDialog.YesAt(l).Overlaps(area), Is.False, $"{name}: YES");
                Assert.That(ConfirmDialog.NoAt(l).Overlaps(area), Is.False, $"{name}: NO");
                foreach (var r in new[] { ConfirmDialog.PanelAt(l), ConfirmDialog.YesAt(l), ConfirmDialog.NoAt(l) })
                    Assert.That(r.xMin >= ScreenRect.xMin && r.xMax <= ScreenRect.xMax && r.yMin >= ScreenRect.yMin && r.yMax <= ScreenRect.yMax, Is.True, $"{name}: on screen");
            }

            // The dialogs really open there.
            for (var i = 0; i < 3; i++)
            {
                var (v, _, _) = SwapAt(i);
                Click(Source.Imgui, PrepareView.CardArea(i).center);
                Assert.That(v.ConfirmOpen, Is.True);
                Assert.That(v.Dialog.YesArea.Overlaps(PrepareView.CardArea(i)), Is.False, $"SWAP card {i + 1}: YES");
                Assert.That(v.Dialog.NoArea.Overlaps(PrepareView.CardArea(i)), Is.False, $"SWAP card {i + 1}: NO");
                Object.DestroyImmediate(v.gameObject);
            }
        }

        [Test]
        public void ADialogLocksInputForThreeHundredMillisecondsWhenItOpens()
        {
            var (v, _, picks) = SwapAt(0);
            Click(Source.Imgui, PrepareView.CardArea(0).center);
            Assert.That(v.ConfirmOpen, Is.True);
            Wait(0.2f);
            Click(Source.Imgui, v.Dialog.YesArea.center);
            Assert.That(picks, Is.Empty, "YES inside the 0.3 s lock: ignored");
            Wait(0.15f);
            Click(Source.Imgui, v.Dialog.YesArea.center);
            Assert.That(picks, Is.EqualTo(new[] { 0 }));
        }

        /// <summary>~5 taps a second for 2 s on the spot that opened the dialog: it stays open and nothing is chosen.</summary>
        void Spam(Vector2 at, Source src)
        {
            for (var k = 0; k < 10; k++)
            {
                Click(src, at);
                Wait(0.2f);
            }
        }

        [TestCase(0, Source.Imgui)]
        [TestCase(1, Source.Imgui)]
        [TestCase(2, Source.Imgui)]
        [TestCase(1, Source.Polling)]
        public void SpammingTheSwapCardNeverAnswersTheDialog(int slot, Source src)
        {
            var (v, run, picks) = SwapAt(slot);
            var weapon = run.Build.WeaponId;
            Click(src, PrepareView.CardArea(slot).center);
            Assert.That(v.ConfirmOpen, Is.True);
            Spam(PrepareView.CardArea(slot).center, src);
            Assert.That(v.ConfirmOpen, Is.True, "still open");
            Assert.That(picks, Is.Empty, "no YES chosen");
            Assert.That(run.Build.WeaponId, Is.EqualTo(weapon), "weapon unchanged");
        }

        [TestCase(Source.Imgui)]
        [TestCase(Source.Polling)]
        public void SpammingFightNeverAnswersSkipFreePick(Source src)
        {
            var run = RunState.Start(83, FirstChoice(83), new ProgressData());
            run.OfferCards.Clear();
            foreach (var c in new[] { new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp) }) run.OfferCards.Add(OfferSlot.From(c));
            var v = PrepareView.Create(root.transform, art);
            var fights = 0;
            v.FightRequested += () => fights++;
            v.Show(run);
            Wait(0.65f);
            Click(src, PrepareView.FightArea.center);
            Assert.That(v.ConfirmOpen, Is.True);
            Spam(PrepareView.FightArea.center, src);
            Assert.That(v.ConfirmOpen, Is.True);
            Assert.That(fights, Is.Zero);
        }

        [TestCase(Source.Imgui)]
        [TestCase(Source.Polling)]
        public void SpammingNewRunNeverAnswersAbandon(Source src)
        {
            var store = new MemorySaveStore();
            var flow = FlowWithSavedRun(store);
            var savedSeed = store.Load().Run.Seed;
            var title = RunTitleView.Create(root.transform, art);
            var starts = 0;
            title.NewRunRequested += () => starts++;
            title.Show(flow);
            Click(src, RunTitleView.NewRunArea.center);
            Assert.That(title.DialogOpen, Is.True);
            Spam(RunTitleView.NewRunArea.center, src);
            Assert.That(title.DialogOpen, Is.True);
            Assert.That(starts, Is.Zero, "the saved run is kept");
            Assert.That(store.Load().Run.Seed, Is.EqualTo(savedSeed));
        }
    }
}
