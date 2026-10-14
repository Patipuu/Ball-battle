using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>
    /// 1: the match ArenaView really steps is the one prepared for the countdown (driven through RunController +
    /// ArenaView frames, the tester's sequence: boss win → Prepare fight 4 → FIGHT → YES skip → countdown → fight).
    /// 2: Second Wind feedback. 3: result-screen damage capped to the HP really removed.
    /// </summary>
    public class RunArenaChainTests
    {
        const float Frame = 1f / 60f;
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("ChainRoot");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static readonly FieldInfo UsedField = typeof(SecondWindTrait).GetField("used", BindingFlags.NonPublic | BindingFlags.Instance);
        static SecondWindTrait SecondWindOf(BallState b) => b.Traits.OfType<SecondWindTrait>().FirstOrDefault();

        // ---- 1. Countdown match == running match ----

        sealed class Rig
        {
            public ArenaView Arena;
            public RunController Run;
            public bool ControllerFirst;
            public readonly List<SimEvent> Events = new List<SimEvent>();

            public void Step(int frames = 1)
            {
                for (var i = 0; i < frames; i++)
                {
                    if (ControllerFirst) { Run.Tick(Frame); Arena.Advance(Frame); }
                    else { Arena.Advance(Frame); Run.Tick(Frame); }
                }
            }

            public RunFlow Flow => Run.Flow;
        }

        Rig Build(bool controllerFirst, string forceTrait)
        {
            var arena = root.AddComponent<ArenaView>();
            arena.Art = art;
            arena.AutoRestart = false;   // as GameController sets it
            var rig = new Rig { Arena = arena, Run = RunController.Create(root.transform, arena, new MemorySaveStore()), ControllerFirst = controllerFirst };
            rig.Flow.ForceTraitId = forceTrait;
            arena.SimEventRaised += rig.Events.Add;
            rig.Run.Open();
            rig.Step();
            return rig;
        }

        /// <summary>FIGHT (and YES when it asks), then frames until the result screen; NEXT FIGHT.</summary>
        static void PlayOneFight(Rig rig)
        {
            if (rig.Flow.Current == RunFlow.State.StartPick) rig.Flow.PickStart(0); // one free retry card, then the retry fight
            else
            {
                rig.Run.Prepare.PressFight();
                if (rig.Run.Prepare.ConfirmOpen) rig.Run.Prepare.PressConfirm(true);
            }
            var guard = 0;
            while (rig.Flow.Current != RunFlow.State.FightResult && guard++ < 60 * 400) rig.Step();
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.FightResult));
            rig.Run.Result.PressContinue();
            rig.Step();
        }

        [TestCase(true, true, false)]
        [TestCase(false, true, false)]
        [TestCase(true, false, false)]
        [TestCase(true, true, true)]    // the bug report's build: Brawler + Thorns + Second Wind
        [TestCase(false, true, true)]
        public void TheMatchShownInTheCountdownIsTheOneThatRuns(bool controllerFirst, bool forced, bool brawlerThorns)
        {
            uint seed = 0;
            if (forced) seed = 31;
            else
                for (uint s = 1; s < 2000 && seed == 0; s++)
                {
                    var w = RunState.StartChoices(s, UnlockRules.Weapons(new ProgressData()))[0];
                    var probe = RunState.Start(s, w, new ProgressData());
                    if (Enumerable.Range(0, probe.OfferCount).Any(i => probe.OfferCard(i).Equals(new Card(CardKind.Trait, SecondWindTrait.TraitId, 1)))) seed = s;
                }
            var rig = Build(controllerFirst, forced ? SecondWindTrait.TraitId : null);
            rig.Flow.NewRun(seed);
            rig.Step();
            rig.Flow.ChooseWeapon(rig.Flow.StartChoices[0]);
            var startSw = Enumerable.Range(0, rig.Flow.Run.OfferCount).First(i => rig.Flow.Run.OfferCard(i).Id == SecondWindTrait.TraitId);
            rig.Flow.PickStart(startSw);
            rig.Step();
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.Prepare));
            if (brawlerThorns)
            {
                rig.Flow.Run.Build.WeaponId = BrawlerRule.WeaponId;
                rig.Flow.Run.Build.Traits.Add(new TraitSlot(ThornsTrait.TraitId, 1));
                rig.Run.Prepare.Show(rig.Flow.Run);
            }
            rig.Step();
            Assert.That(rig.Flow.Run.Build.TraitLevel(SecondWindTrait.TraitId), Is.EqualTo(1));

            rig.Arena.TimeScale = 4f;
            rig.Flow.Run.Build.DamagePct = TFix.X(10f); // Reach the boss deterministically; restore for the fight under test.
            while (rig.Flow.Run.FightIndex < 4) PlayOneFight(rig);   // Shell King is fight 4
            rig.Flow.Run.Build.DamagePct = TFix.X(0f);
            rig.Arena.TimeScale = 1f;
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.Prepare));
            Assert.That(rig.Flow.Run.History.Any(h => RunTuning.IsBoss(h.FightIndex)), Is.True, "the boss was fought");

            // Prepare of fight 4 at 41% HP, like the video; the screen redraws as on any change.
            rig.Flow.Run.Build.SetHp(TFix.X(0.41f) * rig.Flow.Run.Build.MaxHp);
            rig.Flow.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.DamageUp));   // a card can be taken, so FIGHT asks "SKIP FREE PICK?"
            rig.Run.Prepare.Show(rig.Flow.Run);
            var shownHp = rig.Run.Prepare.YouStats.Split(' ')[1].Split('/')[0];

            // FIGHT -> YES (skip the free pick) -> countdown.
            rig.Run.Prepare.PressFight();
            Assert.That(rig.Run.Prepare.ConfirmOpen, Is.True, "free pick unused: asks first");
            rig.Run.Prepare.PressConfirm(true);
            rig.Step();
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.Countdown));
            var countdownSim = rig.Arena.Sim;
            var you = countdownSim.Balls[0];
            Assert.That(SecondWindOf(you), Is.Not.Null, "ball 0 of the countdown match has Second Wind");
            if (brawlerThorns)
            {
                Assert.That(you.Weapon.Id, Is.EqualTo(BrawlerRule.WeaponId));
                Assert.That(you.Traits.OfType<ThornsTrait>().Any(), Is.True);
            }
            Assert.That(HpText.Of(you.Hp.ToFloat()).ToString(), Is.EqualTo(shownHp), "countdown HP = Prepare HP");
            Assert.That(rig.Arena.Hud.HpText(0), Is.EqualTo(shownHp), "HUD during the countdown");
            var hp0 = you.Hp;

            // Countdown frames: same instance, frozen.
            while (rig.Flow.Current == RunFlow.State.Countdown)
            {
                rig.Step();
                Assert.That(rig.Arena.Sim, Is.SameAs(countdownSim), "no other match during the countdown");
            }
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.Playing));

            // First running tick.
            rig.Events.Clear();
            var guard = 0;
            while (countdownSim.Tick < 1 && guard++ < 10) rig.Step();
            Assert.That(rig.Arena.Sim, Is.SameAs(countdownSim), "the running match is the countdown match");
            var heals = rig.Events.Where(e => e.Type == SimEventType.Heal && e.A == 0).Sum(e => e.Value.ToFloat());
            Assert.That(you.Hp.ToFloat(), Is.LessThanOrEqualTo(hp0.ToFloat() + heals + 0.001f), "no unexplained HP jump on the first tick");
            Assert.That(SecondWindOf(rig.Arena.Sim.Balls[0]), Is.SameAs(SecondWindOf(you)));

            // The whole fight: same match; Second Wind fires at <= 30% (one tick late at most) and never dies unused.
            var sw2 = SecondWindOf(you);
            var lowFrames = 0;
            guard = 0;
            while (countdownSim.Outcome == MatchOutcome.Ongoing && guard++ < 60 * 200)
            {
                var wasAlive = you.Alive;
                rig.Step();
                Assert.That(rig.Arena.Sim, Is.SameAs(countdownSim));
                var used = (bool)UsedField.GetValue(sw2);
                if (lowFrames > 0 && you.Alive) Assert.That(used, Is.True, $"tick {countdownSim.Tick}: {you.Hp:0.#}/{you.MaxHp:0.#} and Second Wind not fired");
                lowFrames = you.Alive && !used && you.HpFraction <= TraitTuning.SecondWindThreshold ? 1 : 0;
                if (wasAlive && !you.Alive) Assert.That(used, Is.True, "died with Second Wind unused");
            }
        }

        [Test]
        public void IfAnotherMatchEverReplacesTheFightItIsLogged()
        {
            var rig = Build(true, SecondWindTrait.TraitId);
            rig.Flow.NewRun(31);
            rig.Step();
            rig.Flow.ChooseWeapon(rig.Flow.StartChoices[0]);
            rig.Flow.PickStart(0);
            rig.Step();
            rig.Run.Prepare.PressFight();
            if (rig.Run.Prepare.ConfirmOpen) rig.Run.Prepare.PressConfirm(true);
            rig.Step();
            Assert.That(rig.Flow.Current, Is.EqualTo(RunFlow.State.Countdown));
            Assert.That(rig.Arena.Sim, Is.SameAs(rig.Flow.CurrentMatch));

            // Something swaps the arena's match (the suspected bug): the run notices and says so in the log.
            var other = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 99,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            rig.Arena.StartMatch(other);
            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("arena is not showing the Run fight"));
            rig.Step();
            rig.Step();   // logged once, not every frame
        }

                // ---- 3 / 4. Result numbers and small fixes ----

        [Test]
        public void ResultUsesTheEnemysWholeLossAndTheNextFightsRealMaxHp()
        {
            var run = RunState.Start(52, BladeRule.WeaponId, new ProgressData());
            run.Build.Traits.Add(new TraitSlot(HeavyTrait.TraitId, 2));
            run.EnemyWeapons.Clear();
            run.EnemyWeapons.Add(VenomRule.WeaponId);
            var match = run.CreateMatch();
            var stats = new FightStats();
            stats.Reset(match);
            while (match.Outcome == MatchOutcome.Ongoing) { match.Step(); stats.AddAll(match.Events); }
            var before = FightReport.Snapshot.Of(run);
            run.ApplyResult(match);
            var report = FightReport.Build(match, stats, before, run);
            Assert.That(report.DamageDealt, Is.EqualTo(stats.Taken(1)), "DMG DEALT = all the enemy lost (hits, poison, reflect, arena)");
            if (match.WinnerIndex == 0 && match.EndReason == MatchEndReason.Knockout)
                Assert.That(report.DamageDealt, Is.GreaterThanOrEqualTo(match.Balls[1].MaxHp.ToFloat() * 0.99f), "a KO shows about the enemy's whole HP");
            var next = run.CreateMatch().Balls[0];
            Assert.That(report.MaxHpNext, Is.EqualTo(next.MaxHp.ToFloat()), "HP NEXT FIGHT uses the real max HP");
            Assert.That(report.MaxHpNext, Is.EqualTo((run.Build.MaxHp * (Fix.One + TraitTuning.HeavyMaxHpPct[1])).ToFloat()).Within(0.01f), "Heavy 2: not /100");
            Assert.That(report.CarryLine, Does.Contain("/" + HpText.Of(next.MaxHp.ToFloat())));
        }

        [Test]
        public void TraitLevelsHaveASpace()
        {
            var b = new RunBuild();
            b.Traits.Add(new TraitSlot(SecondWindTrait.TraitId, 2));
            b.Traits.Add(new TraitSlot(HeavyTrait.TraitId, 2));
            b.Traits.Add(new TraitSlot(ThornsTrait.TraitId, 1));
            Assert.That(CardText.Traits(b), Is.EqualTo("SECOND WIND 2 HEAVY 2 THORNS"));
        }

        [Test]
        public void TheRedLostSegmentStartsAtTheCurrentHp()
        {
            var a = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = TFix.X(100f), Hp = TFix.X(41f) };
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 3, new[] { a, new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim, "YOU", "FOE");
            Assert.That(view.Hud.LostFraction(0), Is.EqualTo(0.41f).Within(0.001f), "no red drain from full at the start");
            Assert.That(view.Hud.LostFraction(1), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void RerollCanBeRepeatedAndSaysItCostsAgain()
        {
            var run = RunState.Start(53, BladeRule.WeaponId, new ProgressData());
            run.Pick(0);   // CHOOSE YOUR START is separate from this Prepare's first pick.
            run.Coins = 3;
            var v = PrepareView.Create(root.transform, art);
            v.RerollRequested += () => { run.Reroll(); v.Show(run); };
            v.Show(run);
            Assert.That(v.RerollReason, Is.Empty, "before the first reroll: just REROLL 1");
            v.PressReroll();
            Assert.That(run.Coins, Is.EqualTo(2), "paid");
            Assert.That(v.RerollReason, Is.EqualTo("AGAIN 2  COINS 2"), "says the next one costs again");
            v.PressReroll();
            Assert.That(run.Coins, Is.Zero, "paid again");
            Assert.That(v.RerollReason, Is.EqualTo("NOT ENOUGH COINS"));
            v.PressReroll();
            Assert.That(run.Coins, Is.Zero, "locked: no third reroll");
            Assert.That(run.RerollCount, Is.EqualTo(2));
        }

                // ---- 2. Second Wind feedback ----

        (ArenaView view, FxView fx, MatchSim sim) Fight(float youHp)
        {
            var a = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = TFix.X(100f), Hp = TFix.X(youHp) }.With(TraitRegistry.Create(SecondWindTrait.TraitId, 1));
            var b = new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)).With(TraitRegistry.Create(VampireTrait.TraitId, 1));
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 6, new[] { a, b });
            sim.ApplySpawnHooks();
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            view.StartMatch(sim, "YOU", "FOE");
            var fx = root.AddComponent<FxView>();
            typeof(FxView).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(fx, null);
            var on = typeof(FxView).GetMethod("OnSimEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            view.SimEventRaised += e => on.Invoke(fx, new object[] { e });
            return (view, fx, sim);
        }

        [Test]
        public void SecondWindShowsATurquoisePlusNumberCaptionRingAndSpentBadge()
        {
            var (view, fx, sim) = Fight(25f);   // 25% HP: fires on the first tick
            Assert.That(view.Badges.IsSpent(0, SecondWindTrait.TraitId), Is.False);
            var normal = view.Badges.BoxColor(0, SecondWindTrait.TraitId).Value;
            view.Advance(Frame);
            view.Advance(Frame);
            var label = "+" + fx.Popups.Format(TraitTuning.SecondWindHeal[0].ToFloat());
            var number = fx.Popups.ShowingNow().FirstOrDefault(p => p.value == label);
            Assert.That(number.value, Is.EqualTo(label), "the heal number has a + sign");
            Assert.That(number.scale, Is.EqualTo(2));
            Assert.That(fx.Popups.ColorOf(label), Is.EqualTo(FxView.SecondWindColor));
            Assert.That(FxView.SecondWindColor, Is.Not.EqualTo(new Color32(96, 255, 64, 255)), "not the poison green");
            var caption = fx.Popups.LabelOf(label);
            Assert.That(caption, Is.Not.Null);
            Assert.That(caption.Text, Is.EqualTo("2ND WIND"));
            Assert.That(caption.TextColor.r == 60 && caption.TextColor.g == 150 && caption.TextColor.b == 170, Is.True);
            Assert.That(caption.transform.localPosition.y, Is.LessThan(number.y), "caption under the number");
            Assert.That(fx.ActiveRingPulses, Is.EqualTo(1), "turquoise ring grows around the ball");

            Assert.That(view.Badges.IsSpent(0, SecondWindTrait.TraitId), Is.True);
            var spent = view.Badges.BoxColor(0, SecondWindTrait.TraitId).Value;
            Assert.That(spent.r, Is.EqualTo((byte)(normal.r * TraitBadgeView.SpentBrightness)));
            Assert.That(spent.g, Is.EqualTo((byte)(normal.g * TraitBadgeView.SpentBrightness)));

            // The ring lasts 0.3 s.
            var tick = typeof(FxView).GetMethod("TickRings", BindingFlags.NonPublic | BindingFlags.Instance);
            tick.Invoke(fx, new object[] { 0.2f });
            Assert.That(fx.ActiveRingPulses, Is.EqualTo(1));
            tick.Invoke(fx, new object[] { 0.11f });
            Assert.That(fx.ActiveRingPulses, Is.Zero);

            // Next fight: badge back to normal.
            var next = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 7,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)).With(TraitRegistry.Create(SecondWindTrait.TraitId, 1)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            view.StartMatch(next, "YOU", "FOE");
            Assert.That(view.Badges.IsSpent(0, SecondWindTrait.TraitId), Is.False);
            Assert.That(view.Badges.BoxColor(0, SecondWindTrait.TraitId).Value, Is.EqualTo(normal));
        }

        [Test]
        public void OtherHealsKeepTheirOwnLookAndSecondWindShowsOnce()
        {
            var (view, fx, sim) = Fight(80f);
            var on = typeof(FxView).GetMethod("OnSimEvent", BindingFlags.NonPublic | BindingFlags.Instance);
            on.Invoke(fx, new object[] { new SimEvent { Type = SimEventType.Heal, Tick = 1, A = 0, Value = TFix.X(3.5f), Point = sim.Balls[0].Pos } });
            Assert.That(fx.ActiveRingPulses, Is.Zero, "a 3.5 heal (vampire, regen) is not Second Wind");
            Assert.That(fx.Popups.ShowingNow().Any(p => p.value == "3.5"), Is.True);
            Assert.That(view.Badges.IsSpent(0, SecondWindTrait.TraitId), Is.False);
            on.Invoke(fx, new object[] { new SimEvent { Type = SimEventType.TraitTriggered, TraitId = SecondWindTrait.TraitId, Tick = 2, A = 0, Value = TFix.X(20f), Point = sim.Balls[0].Pos } });
            on.Invoke(fx, new object[] { new SimEvent { Type = SimEventType.TraitTriggered, TraitId = SecondWindTrait.TraitId, Tick = 3, A = 0, Value = TFix.X(20f), Point = sim.Balls[0].Pos } });
            Assert.That(fx.ActiveRingPulses, Is.EqualTo(1), "once per fight");
            on.Invoke(fx, new object[] { new SimEvent { Type = SimEventType.Heal, Tick = 4, A = 1, Value = TFix.X(20f), Point = sim.Balls[1].Pos } });
            Assert.That(fx.ActiveRingPulses, Is.EqualTo(1), "the enemy has no Second Wind");
        }

        [Test]
        public void ALethalBlowSavedBySecondWindShowsTheSameFeedback()
        {
            var (view, fx, sim) = Fight(35f);   // just above 30%: it either drops under 30% or takes a lethal blow
            var guard = 0;
            var you = sim.Balls[0];
            while (sim.Outcome == MatchOutcome.Ongoing && !(bool)UsedField.GetValue(SecondWindOf(you)) && guard++ < 60 * 200) view.Advance(Frame);
            Assert.That((bool)UsedField.GetValue(SecondWindOf(you)), Is.True, "Second Wind fired in this fight");
            Assert.That(view.Badges.IsSpent(0, SecondWindTrait.TraitId), Is.True);
            Assert.That(fx.Popups.LabelOf("+" + fx.Popups.Format(TraitTuning.SecondWindHeal[0].ToFloat())), Is.Not.Null);
        }

        // ---- 3. Damage capped to the HP removed ----

        [Test]
        public void ResultDamageNeverCountsTheOverkill()
        {
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 2,
                new[] { new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)), new BallLoadout(WeaponRegistry.Create(FangRule.WeaponId)) });
            sim.ApplySpawnHooks();
            var stats = new FightStats();
            stats.Reset(sim);
            stats.Add(new SimEvent { Type = SimEventType.Hit, A = 1, B = 0, Value = TFix.X(60f) });
            stats.Add(new SimEvent { Type = SimEventType.Heal, A = 0, Value = TFix.X(10f) });
            stats.Add(new SimEvent { Type = SimEventType.Hit, A = 1, B = 0, Value = TFix.X(70f) });   // only 50 left: 20 overkill
            Assert.That(stats.Taken(0), Is.EqualTo(110f));
            Assert.That(stats.DealtBySource(1), Is.EqualTo(110f));
            stats.Add(new SimEvent { Type = SimEventType.StatusTick, A = 0, B = 1, Value = TFix.X(5f) });   // already at 0
            Assert.That(stats.Taken(0), Is.EqualTo(110f));
            stats.Add(new SimEvent { Type = SimEventType.Heal, A = 0, Value = TFix.X(20f) });   // Second Wind save: back to 20
            stats.Add(new SimEvent { Type = SimEventType.WallBounce, A = 0, Value = TFix.X(25f) });
            Assert.That(stats.Taken(0), Is.EqualTo(130f), "after a save, counts again up to the HP it got back");
            Assert.That(stats.DealtBySource(1), Is.EqualTo(110f), "arena damage is nobody's");
            Assert.That(stats.Dealt(1), Is.EqualTo(stats.Taken(0)), "DMG DEALT = everything the opponent lost");
        }
    }
}
