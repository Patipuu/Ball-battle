using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace BallBattle.Tests
{
    /// <summary>
    /// Prompt 3e: fast clicks (same-frame press/release, polling → IMGUI switch, lock windows), HEAL/SWAP icons and the
    /// SWAP title wrap, FOE HEALED line, whole-number damage, end-of-fight log, -bbSeed, stat card stripes, new weapon
    /// names in card text.
    /// </summary>
    public class Prompt3eTests
    {
        const float Frame = 1f / 60f;
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            root = new GameObject("P3eRoot");
            PixelInput.SetTestClock(0f);
            PixelInput.ResetCapture();
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.ResetCapture();
            PixelInput.SetTestClock(null);
            Object.DestroyImmediate(root);
        }

        static string FirstChoice(uint seed) => RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0];

        // ---- 10. Fast clicks ----

        static readonly Rect Area = new Rect(-20, -20, 40, 40);
        static readonly Vector2 On = Vector2.zero;
        static readonly Vector2 Off = new Vector2(100, 100);

        int clicks;

        PixelButton Button()
        {
            var b = PixelButton.Create(root.transform, "B", art, Area, "B", 1, Palette.Floor, Palette.Wall, Palette.Text, 0);
            clicks = 0;
            b.Clicked += () => clicks++;
            return b;
        }

        void Dispatch() => PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));

        [Test]
        public void PollingPressAndReleaseInOneFrameIsOneClick()
        {
            Button();
            PixelInput.PollFrame(1, true, true, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1));
        }

        [Test]
        public void PollingReleaseWithoutTheMissedPressStillClicks()
        {
            Button();
            PixelInput.PollFrame(1, false, true, On);   // a slow frame collapsed the press: only the release is reported
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1));
        }

        [Test]
        public void PressAndReleaseInConsecutiveFramesIsOneClick()
        {
            Button();
            PixelInput.PollFrame(1, true, false, On);
            Dispatch();
            PixelInput.PollFrame(2, false, true, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1));

            PixelInput.ResetCapture();
            PixelInput.GuiEvent(1, true, On);
            Dispatch();
            PixelInput.GuiEvent(2, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(2), "IMGUI too");
        }

        [Test]
        public void ImguiPressAndReleaseInOneFrameIsOneClick()
        {
            Button();
            PixelInput.GuiEvent(5, true, On);
            PixelInput.GuiEvent(5, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(PixelInput.UsesGuiEvents, Is.True);

            PixelInput.GuiEvent(6, false, On);   // only the release arrived
            Dispatch();
            Assert.That(clicks, Is.EqualTo(2));
        }

        [Test]
        public void FirstClickWhenSwitchingToImguiIsNeverLost()
        {
            // The click that focuses the window: polling saw nothing, IMGUI sees the press and the release.
            Button();
            PixelInput.PollFrame(3, false, false, On);
            PixelInput.GuiEvent(3, true, On);
            PixelInput.GuiEvent(3, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1), "old code dropped the first IMGUI event and lost this click");
        }

        [Test]
        public void NoDoubleClickWhenPollingAndImguiSeeTheSameClick()
        {
            Button();
            PixelInput.PollFrame(3, true, true, On);   // polled ...
            Dispatch();
            PixelInput.GuiEvent(3, true, On);          // ... and the same press / release from IMGUI in the same frame
            PixelInput.GuiEvent(3, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1));

            // Press polled, release later from IMGUI: still one click.
            PixelInput.ResetCapture();
            PixelInput.PollFrame(7, true, false, On);
            Dispatch();
            PixelInput.GuiEvent(7, true, On);
            PixelInput.GuiEvent(8, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(2));

            // Once IMGUI is the source, polling adds nothing.
            PixelInput.PollFrame(9, true, true, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(2));
        }

        [Test]
        public void SlidingOffStillCancelsAndAPressFromAnotherButtonNeverClicks()
        {
            Button();
            PixelInput.GuiEvent(1, true, On);
            PixelInput.GuiEvent(2, false, Off);
            Dispatch();
            PixelInput.GuiEvent(3, true, Off);
            PixelInput.GuiEvent(4, false, On);
            Dispatch();
            Assert.That(clicks, Is.Zero);
        }

        [Test]
        public void APressBeforeAnUnlockDoesNotEatTheNextClick()
        {
            Button();
            PixelInput.BlockFor(0.3f);
            PixelInput.AdvanceTestClock(0.1f);
            PixelInput.GuiEvent(1, true, On);   // pressed while locked
            Dispatch();
            PixelInput.AdvanceTestClock(0.3f);
            PixelInput.GuiEvent(2, false, On);  // released after the unlock
            Dispatch();
            Assert.That(clicks, Is.Zero, "a click started while locked does not count");
            PixelInput.GuiEvent(3, true, On);
            PixelInput.GuiEvent(3, false, On);
            Dispatch();
            Assert.That(clicks, Is.EqualTo(1), "the next quick click works");
        }

        // ---- 1-3. HEAL / SWAP icons, SWAP title ----

        [Test]
        public void HealAndSwapCardsShowTheirIcons()
        {
            Assert.That(art.CardHeal, Is.Not.Null);
            Assert.That(art.CardSwap, Is.Not.Null);
            Assert.That(art.CardHeal.rect.size, Is.EqualTo(new Vector2(14, 14)));
            var run = RunState.Start(70, FirstChoice(70), new ProgressData());
            run.Coins = 10;
            run.Build.SetHp(TFix.X(50f));
            run.OfferCards.Clear();
            var other = WeaponRegistry.All.First(w => w.Id != run.Build.WeaponId).Id;
            foreach (var c in new[] { new Card(CardKind.Heal), new Card(CardKind.SwapWeapon, other), new Card(CardKind.DamageUp) }) run.OfferCards.Add(OfferSlot.From(c));
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.CardIcon(0).sprite, Is.SameAs(art.CardHeal));
            Assert.That(v.CardIcon(1).sprite, Is.SameAs(art.CardSwap));
            Assert.That(v.CardIcon(1).enabled, Is.True);
            Assert.That(v.TitleWrapped(1), Is.False, "no reason tag: the title stays on one 2x line");
        }

        [Test]
        public void TheLongestSwapTitleWrapsInsteadOfTouchingTheReasonTag()
        {
            var run = RunState.Start(71, FirstChoice(71), new ProgressData());
            run.Coins = 10;
            run.Build.WeaponId = VolleyRule.WeaponId;   // SPARKLER: the longest display name
            run.OfferCards.Clear();
            foreach (var c in new[] { new Card(CardKind.SwapWeapon, VolleyRule.WeaponId), new Card(CardKind.DamageUp), new Card(CardKind.MaxHp) }) run.OfferCards.Add(OfferSlot.From(c));
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.CardTag(0).Text, Is.EqualTo("ALREADY YOUR WEAPON"));
            Assert.That(v.CardIcon(0).enabled, Is.True, "the icon stays");
            Assert.That(v.TitleWrapped(0), Is.True);
            Assert.That(v.CardTitleLine(0, 1).Text, Is.EqualTo("SWAP TO"));
            Assert.That(v.CardTitleLine(0, 2).Text, Is.EqualTo(WeaponNames.Of(VolleyRule.WeaponId)));

            var area = PrepareView.CardArea(0);
            var tag = v.CardTag(0).WorldBounds;
            var l1 = v.CardTitleLine(0, 1).WorldBounds;
            var l2 = v.CardTitleLine(0, 2).WorldBounds;
            var d1 = v.CardDetail(0, 1).WorldBounds;
            var icon = v.CardIcon(0).bounds;
            Assert.That(l1.xMax + 2f, Is.LessThanOrEqualTo(tag.xMin), "line 1 clear of the tag");
            Assert.That(l2.yMax, Is.LessThanOrEqualTo(tag.yMin), "line 2 under the tag row");
            Assert.That(l2.yMin, Is.GreaterThan(d1.yMax), "above the description");
            Assert.That(l1.xMin, Is.GreaterThan(icon.max.x), "right of the icon");
            foreach (var b in new[] { l1, l2, tag })
                Assert.That(b.xMin >= area.xMin + 2 && b.xMax <= area.xMax - 2 && b.yMin >= area.yMin + 2 && b.yMax <= area.yMax - 2, Is.True, "inside the card, inside 270 px");
        }

        // ---- 4-5. FOE HEALED and whole-number damage ----

        (ArenaView arena, RunController rc, List<SimEvent> events) Rig()
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;
            var rc = RunController.Create(root.transform, arena, new MemorySaveStore());
            var events = new List<SimEvent>();
            arena.SimEventRaised += events.Add;
            rc.Open();
            return (arena, rc, events);
        }

        static void PlayFightOne((ArenaView arena, RunController rc, List<SimEvent> events) rig, uint seed, string weapon, int chapter = 0)
        {
            var (arena, rc, _) = rig;
            rc.Flow.NewRun(seed, false, chapter);
            rc.Tick(Frame);
            rc.Flow.ChooseWeapon(weapon);
            rc.Flow.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.DamageUp));
            rc.Flow.PickStart(0);
            rc.Tick(Frame);
            rc.Prepare.PressFight();
            if (rc.Prepare.ConfirmOpen) rc.Prepare.PressConfirm(true);
            arena.TimeScale = 4f;
            var guard = 0;
            while (rc.Flow.Current != RunFlow.State.FightResult && guard++ < 60 * 300) { rc.Tick(Frame); arena.Advance(Frame); }
            Assert.That(rc.Flow.Current, Is.EqualTo(RunFlow.State.FightResult));
            rc.Tick(Frame);   // the result screen shows
        }

        [Test]
        public void AHealingEnemyGetsAFoeHealedLineAndTheEndOfFightLog()
        {
            var rig = Rig();
            // Chapters (21/10): MIRROR (aegis), which heals, is in chapter 2's pool. Find a seed whose fight 1 is MIRROR for a BRAWLER start.
            uint seed = 0;
            for (uint s = 1; s < 2000 && seed == 0; s++)
            {
                var p = new ProgressData { RunsPlayed = 1, ChaptersCleared = 1 };
                if (!RunState.StartChoices(s, UnlockRules.Weapons(p)).Contains(BrawlerRule.WeaponId)) continue;
                if (RunState.Start(s, BrawlerRule.WeaponId, p, chapter: 1).Enemy.WeaponId == AegisRule.WeaponId) seed = s;
            }
            Assert.That(seed, Is.Not.Zero);
            rig.rc.Flow.Progress.ChaptersCleared = 1;
            LogAssert.Expect(LogType.Log, new Regex(@"^\[BallBattle\] Fight 1 ended: seed " + seed + @" \(match \d+\), outcome (Win|Lost|Draw), tick \d+, you brawler HP start \d+\.\d\d heal \d+\.\d\d end \d+\.\d\d, enemy aegis HP start \d+\.\d\d heal \d+\.\d\d end \d+\.\d\d, DMG DEALT \d+\.\d\d TAKEN \d+\.\d\d$"));
            PlayFightOne(rig, seed, BrawlerRule.WeaponId, 1);
            var healed = rig.events.Where(e => e.Type == SimEventType.Heal && e.A == 1).Sum(e => e.Value.ToFloat());
            Assert.That(healed, Is.GreaterThan(0f));
            var r = rig.rc.Flow.LastReport;
            Assert.That(r.EnemyHealed, Is.EqualTo(healed).Within(0.01f));
            var view = rig.rc.Result;
            Assert.That(view.FoeHealedLine.Text, Is.EqualTo("FOE HEALED +" + HpText.Floor(healed)));
            Assert.That((Color32)view.FoeHealedLine.TextColor, Is.EqualTo(new Color32(140, 240, 220, 255)));
            AssertNoTextOverlaps(view.transform);
        }

        [Test]
        public void NoFoeHealedLineWhenTheEnemyNeverHealed()
        {
            var rig = Rig();
            PlayFightOne(rig, 7u, FirstChoice(7));   // fight 1: ANGLER (pike), no heals
            Assert.That(rig.events.Any(e => e.Type == SimEventType.Heal && e.A == 1), Is.False);
            Assert.That(rig.rc.Flow.LastReport.EnemyHealed, Is.Zero);
            Assert.That(rig.rc.Result.FoeHealedLine.Text, Is.Empty);
        }

        [Test]
        public void ResultDamageIsAWholeNumberRoundedDown()
        {
            Assert.That(HpText.Floor(155.25f), Is.EqualTo(155));
            Assert.That(HpText.Floor(87.4f), Is.EqualTo(87));
            Assert.That(HpText.Floor(0.4f), Is.Zero);
            var r = new FightReport { EnemyWeaponId = FangRule.WeaponId, FightNumber = 2, HpLeft = 40f, MaxHpInFight = 100f, DamageDealt = 155.25f, DamageTaken = 87.4f, EnemyHealed = 9.5f, Outcome = FightOutcome.Win, NextArenaId = ArenaRegistry.Classic, NextWeaponId = BladeRule.WeaponId, NextEnemyHp = 120f, MaxHpNext = 100f, HpNext = 75f };
            var view = RunResultView.Create(root.transform, art);
            view.Show(r);
            Assert.That(view.Value(1), Is.EqualTo("155"));
            Assert.That(view.Value(2), Is.EqualTo("87"));
            Assert.That(view.FoeHealedLine.Text, Is.EqualTo("FOE HEALED +9"));
            AssertNoTextOverlaps(view.transform);
        }

        static void AssertNoTextOverlaps(Transform screen)
        {
            var texts = screen.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Trim().Length > 0).ToList();
            foreach (var t in texts)
            {
                var b = t.WorldBounds;
                Assert.That(b.xMin >= -135f && b.xMax <= 135f, Is.True, $"'{t.Text}' inside 270 px");
            }
            for (var i = 0; i < texts.Count; i++)
                for (var j = i + 1; j < texts.Count; j++)
                    Assert.That(texts[i].WorldBounds.Overlaps(texts[j].WorldBounds), Is.False, $"'{texts[i].Text}' overlaps '{texts[j].Text}'");
        }

        // ---- 6. End-of-fight log line ----

        [Test]
        public void EndOfFightLogUsesRealValues()
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 9,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(AegisRule.WeaponId)) });
            sim.ApplySpawnHooks();
            var stats = new FightStats();
            stats.Reset(sim);
            stats.Add(new SimEvent { Type = SimEventType.Hit, A = 0, B = 1, Value = TFix.X(12.345f) });
            stats.Add(new SimEvent { Type = SimEventType.Heal, A = 1, Value = TFix.X(1.5f) });
            var line = FightLog.Ended("Fight 3", sim, stats);
            Assert.That(line, Does.StartWith("[BallBattle] Fight 3 ended: seed 9, outcome "), "Versus: the match seed");
            Assert.That(FightLog.Ended("Fight 3", sim, stats, 1791713962u), Does.StartWith("[BallBattle] Fight 3 ended: seed 1791713962 (match 9), outcome "), "Run: the run seed (-bbSeed) and the match seed");
            Assert.That(line, Does.Contain("you blade HP start 100.00 heal 0.00 end 100.00"));
            Assert.That(line, Does.Contain("enemy aegis HP start 100.00 heal 1.50"));
            Assert.That(line, Does.EndWith("DMG DEALT 12.35 TAKEN 0.00"), "2 decimals, not the rounded display");
        }

        // ---- 7. -bbSeed ----

        [Test]
        public void SeedArgumentIsParsedAndForcesNewRuns()
        {
            Assert.That(DebugOptions.ParseSeed(new[] { "game.exe" }), Is.Null);
            Assert.That(DebugOptions.ParseSeed(new[] { "game.exe", "-bbSeed", "1791713962" }), Is.EqualTo(1791713962u));
            Assert.That(DebugOptions.ParseSeed(new[] { "-bbseed", "0" }), Is.EqualTo(0u));
            Assert.That(DebugOptions.ParseSeed(new[] { "-bbForceTrait", "heavy", "-bbSeed", "42" }), Is.EqualTo(42u), "next to -bbForceTrait");
            LogAssert.Expect(LogType.Warning, new Regex("-bbSeed: expected a number"));
            Assert.That(DebugOptions.ParseSeed(new[] { "-bbSeed", "abc" }), Is.Null);
            LogAssert.Expect(LogType.Warning, new Regex("-bbSeed: expected a number"));
            Assert.That(DebugOptions.ParseSeed(new[] { "-bbSeed" }), Is.Null);

            LogAssert.Expect(LogType.Log, "[BallBattle] Run seed forced: 777");
            Assert.That(RunController.NewSeed(777u), Is.EqualTo(777u));
            Assert.That(RunController.NewSeed(null), Is.GreaterThan(0u), "normal play: random");
        }

        // ---- 8. Stat card stripes ----

        [Test]
        public void StatCardStripesMatchTheirIconSymbol()
        {
            Assert.That(CardFace.For(new Card(CardKind.SpeedUp), art).Stripe, Is.EqualTo(new Color32(255, 190, 90, 255)));
            Assert.That(CardFace.For(new Card(CardKind.MaxHp), art).Stripe, Is.EqualTo(new Color32(140, 240, 170, 255)));
            Assert.That(CardFace.For(new Card(CardKind.DamageUp), art).Stripe, Is.EqualTo(new Color32(255, 120, 110, 255)));
            Assert.That(CardFace.For(new Card(CardKind.Heal), art).Stripe, Is.EqualTo(new Color32(140, 240, 220, 255)));
            Assert.That(CardFace.For(new Card(CardKind.SwapWeapon, BladeRule.WeaponId), art).Stripe, Is.EqualTo(new Color32(190, 210, 255, 255)));
            Assert.That(CardFace.For(new Card(CardKind.Trait, VampireTrait.TraitId, 1), art).Stripe, Is.EqualTo(TraitBadgeView.ColorOf(VampireTrait.TraitId)), "traits keep theirs");
        }

        // ---- 9. No old weapon ids on screen ----

        static readonly string[] OldIds = WeaponRegistry.All.Select(w => w.Id.ToUpperInvariant()).ToArray();
        static readonly Regex NamePrefix = new Regex(@"^([A-Z]+):");

        /// <summary>
        /// Weapon names only where a weapon name goes: a "<NAME>:" prefix on a card description line, SWAP titles, the
        /// weapon name cells. Words that merely share an old id elsewhere ("TWIN BLADE", "2ND BLADE BEHIND") are fine.
        /// </summary>
        [Test]
        public void CardTextNamesWeaponsWithTheirNewNames()
        {
            var names = WeaponRegistry.All.Select(w => WeaponNames.Of(w.Id)).ToArray();
            var cards = new List<Card> { new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.MaxHp), new Card(CardKind.Heal) };
            foreach (var t in TraitRegistry.All) { cards.Add(new Card(CardKind.Trait, t.Id, 1)); cards.Add(new Card(CardKind.Trait, t.Id, 2)); }
            foreach (var w in WeaponRegistry.All) cards.Add(new Card(CardKind.SwapWeapon, w.Id));
            foreach (var c in cards)
            {
                foreach (var text in new[] { CardText.Detail(c), CardText.Detail2(c) })
                {
                    var m = NamePrefix.Match(text);
                    if (!m.Success) continue;
                    Assert.That(OldIds, Does.Not.Contain(m.Groups[1].Value), $"{c}: '{text}' names a weapon by its old id");   // "UPGRADE:" etc. are not weapons
                }
                if (c.Kind == CardKind.SwapWeapon)
                    Assert.That(CardText.Title(c), Is.EqualTo("SWAP TO " + WeaponNames.Of(c.Id)));
            }
            Assert.That(CardText.Detail2(new Card(CardKind.SpeedUp)), Is.Empty, "The speed card no longer shows a Wrecker-specific line");
            Assert.That(CardText.Detail(new Card(CardKind.Trait, TwinBladeTrait.TraitId, 1)), Does.StartWith("2ND BLADE BEHIND"), "blade = the weapon's edge here");
            Assert.That(CardText.Detail2(new Card(CardKind.Trait, ParryMasterTrait.TraitId, 1)), Is.EqualTo("NOT FOR " + WeaponNames.Of(BrawlerRule.WeaponId)));
        }

        [Test]
        public void WeaponNameCellsShowTheNewNames()
        {
            var run = RunState.Start(72, FirstChoice(72), new ProgressData());
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.YouNameText, Is.EqualTo(WeaponNames.Of(run.Build.WeaponId)), "your weapon cell");
            Assert.That(v.EnemyNameText, Is.EqualTo(WeaponNames.Of(run.Enemy.WeaponId)), "enemy weapon cell");

            var r = new FightReport { EnemyWeaponId = VenomRule.WeaponId, FightNumber = 1, HpLeft = 40f, MaxHpInFight = 100f, Outcome = FightOutcome.Win, NextArenaId = ArenaRegistry.Classic, NextWeaponId = RigRule.WeaponId, NextEnemyHp = 120f, MaxHpNext = 100f, HpNext = 75f };
            var rv = RunResultView.Create(root.transform, art);
            rv.Show(r);
            Assert.That(rv.FightLineText, Does.EndWith("VS " + WeaponNames.Of(VenomRule.WeaponId)), "enemy just fought");
            Assert.That(rv.NextNameText, Is.EqualTo(WeaponNames.Of(RigRule.WeaponId)), "next enemy");
        }
    }
}
