using System.Collections.Generic;
using System.Linq;
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
    /// <summary>Post-fight result screen in Run mode: numbers straight from the sim, wording from boss-and-loss-rules, fits 270x480.</summary>
    public class RunResultScreenTests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("ResultTestRoot");
        }

        [TearDown]
        public void TearDown()
        {
            PixelInput.SetTestClock(null);
            Object.DestroyImmediate(root);
        }

        // ---- Damage totals = the HP the sim removed ----

        [TestCase("blade", "thorns", "venom", ArenaRegistry.SpikeWalls, 3u)]
        [TestCase("volley", "vampire", "aegis", ArenaRegistry.Bumpers, 5u)]
        [TestCase("brawler", "spiky", "rig", ArenaRegistry.Classic, 8u)]
        public void DamageTotalsMatchTheHpTheSimRemoved(string a, string trait, string b, string arenaId, uint seed)
        {
            var la = new BallLoadout(WeaponRegistry.Create(a)).With(TraitRegistry.Create(trait, 2));
            var lb = new BallLoadout(WeaponRegistry.Create(b)).With(TraitRegistry.Create(ThornsTrait.TraitId, 1));
            var sim = new MatchSim(ArenaRegistry.Create(arenaId), seed, new[] { la, lb });
            sim.ApplySpawnHooks();
            var startHp = sim.Balls.Select(x => x.Hp).ToArray();
            var stats = new FightStats();
            stats.Reset(sim);
            var healed = new float[2];
            for (var t = 0; t < 180 * 60 && sim.Outcome == MatchOutcome.Ongoing; t++)
            {
                sim.Step();
                stats.AddAll(sim.Events);
                foreach (var e in sim.Events) if (e.Type == SimEventType.Heal && e.A >= 0 && e.A < 2) healed[e.A] += e.Value;
            }
            Assert.That(sim.Outcome, Is.Not.EqualTo(MatchOutcome.Ongoing));
            Assert.That(sim.Balls.Count(x => x.Alive), Is.LessThanOrEqualTo(1));
            Assert.That(sim.Balls.Any(x => x.Alive), Is.True, "these seeds end by KO with a survivor, so one exact check always runs");
            for (var i = 0; i < 2; i++)
            {
                // Only the HP really removed counts (no overkill): matches the HP bar, knocked out or not.
                var removed = startHp[i] + healed[i] - Mathf.Max(0f, sim.Balls[i].Hp);
                Assert.That(stats.Taken(i), Is.EqualTo(removed).Within(0.01f), $"ball {i}: taken = HP the sim removed");
                if (!sim.Balls[i].Alive) Assert.That(stats.Taken(i), Is.LessThanOrEqualTo(startHp[i] + healed[i] + 0.01f), $"ball {i}: never more than it had");
            }
            Assert.That(stats.Dealt(0), Is.GreaterThan(0f));
            Assert.That(stats.DealtBySource(0), Is.LessThanOrEqualTo(stats.Taken(1) + 0.01f), "what you caused is part of what the enemy lost");
            Assert.That(stats.Dealt(0), Is.EqualTo(stats.Taken(1)), "DMG DEALT = everything the enemy lost");
            Assert.That(stats.DealtBySource(1), Is.LessThanOrEqualTo(stats.Taken(0) + 0.01f));
        }

        // ---- Flow: FightOver → FightResult → Prepare, report read from the finished fight ----

        static MatchSim lastMatch;

        [Test]
        public void EveryFightShowsAResultBeforeTheNextPrepare()
        {
            var f = new RunFlow(new MemorySaveStore(), 2f, 2f);
            f.MatchPrepared += m => lastMatch = m;
            f.Open();
            f.NewRun(21);
            f.ChooseWeapon(f.StartChoices[0]);
            f.Pick(0);
            var coinsBefore = f.Run.Coins;
            var livesBefore = f.Run.Lives;

            f.Fight();
            f.Tick(2.1f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Playing));
            var m = lastMatch;
            while (m.Outcome == MatchOutcome.Ongoing) { m.Step(); f.Stats.AddAll(m.Events); }
            f.OnMatchEnded(m);
            f.Tick(2.1f);

            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightResult));
            var r = f.LastReport;
            Assert.That(r.FightNumber, Is.EqualTo(1));
            Assert.That(r.Outcome, Is.EqualTo(m.WinnerIndex == 0 ? FightOutcome.Win : m.WinnerIndex < 0 ? FightOutcome.Draw : FightOutcome.Lost));
            Assert.That(r.HpLeft, Is.EqualTo(Mathf.Max(0f, m.Balls[0].Hp)));
            Assert.That(r.MaxHpInFight, Is.EqualTo(m.Balls[0].MaxHp));
            Assert.That(r.DamageDealt, Is.EqualTo(f.Stats.Taken(1)), "DMG DEALT = everything the enemy lost");
            Assert.That(r.DamageTaken, Is.EqualTo(f.Stats.Taken(0)));
            Assert.That(r.CoinsGained, Is.EqualTo(f.Run.Coins - coinsBefore));
            Assert.That(r.CoinsGained, Is.EqualTo(m.WinnerIndex == 0 ? RunTuning.WinCoins : RunTuning.LossCoins));
            var nextStart = f.Run.CreateMatch().Balls[0];
            Assert.That(r.HpNext, Is.EqualTo(nextStart.Hp), "HP NEXT FIGHT = the next fight's real start HP");
            Assert.That(r.MaxHpNext, Is.EqualTo(nextStart.MaxHp));
            Assert.That(r.LivesBefore, Is.EqualTo(livesBefore));
            Assert.That(r.LivesAfter, Is.EqualTo(f.Run.Lives));

            // Next fight: the exact enemy, HP and arena the next Prepare / fight will use.
            Assert.That(r.NextFightNumber, Is.EqualTo(2));
            Assert.That(r.NextWeaponId, Is.EqualTo(f.Run.Enemy.WeaponId));
            Assert.That(r.NextArenaId, Is.EqualTo(f.Run.ArenaId));
            Assert.That(r.NextEnemyHp, Is.EqualTo(f.Run.CreateMatch().Balls[1].MaxHp));

            f.NextAfterResult();
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Prepare));
        }

        [Test]
        public void QuittingOnTheResultScreenResumesAtPrepare()
        {
            var store = new MemorySaveStore();
            var f = new RunFlow(store, 2f, 2f);
            f.MatchPrepared += m => lastMatch = m;
            f.Open();
            f.NewRun(22);
            f.ChooseWeapon(f.StartChoices[0]);
            f.Fight();
            f.Tick(2.1f);
            while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
            f.OnMatchEnded(lastMatch);
            f.Tick(2.1f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightResult));
            var history = f.Run.History.Count;
            f.Close();

            var back = new RunFlow(store, 2f, 2f);
            back.Open();
            back.Continue();
            Assert.That(back.Current, Is.EqualTo(RunFlow.State.Prepare), "the result was already applied and saved");
            Assert.That(back.Run.History.Count, Is.EqualTo(history));
        }

        // ---- Wording (docs/design/boss-and-loss-rules.md) ----

        static FightReport Report(FightOutcome o, bool retry = false, bool over = false, bool won = false) => new FightReport
        {
            FightNumber = 3, WasBoss = true, EnemyWeaponId = BrawlerRule.WeaponId, Outcome = o, EndReason = MatchEndReason.Knockout,
            Seconds = 41.6f, HpLeft = o == FightOutcome.Win ? 12.4f : 0f, MaxHpInFight = 135f, DamageDealt = 87.25f, DamageTaken = 133.4f,
            CoinsGained = o == FightOutcome.Win ? RunTuning.WinCoins : RunTuning.LossCoins, CoinsTotal = 12,
            HpNext = retry ? 60f : 100f, MaxHpNext = 100f, LivesBefore = 3, LivesAfter = o == FightOutcome.Win ? 3 : 2, IsRetry = retry,
            RunOver = over, RunWon = won,
            NextFightNumber = retry ? 3 : 4, NextIsBoss = retry, NextWeaponId = AegisRule.WeaponId, NextTraits = "GLASS CANNON PARRY MASTER",
            NextEnemyHp = 297f, NextArenaId = ArenaRegistry.LowGravity,
        };

        [Test]
        public void WordsFollowTheLossRulesDoc()
        {
            Assert.That(Report(FightOutcome.Win).Title, Is.EqualTo("WIN"));
            Assert.That(Report(FightOutcome.Win).ConsequenceLine, Is.EqualTo($"WIN  +{RunTuning.WinCoins} COINS"));
            Assert.That(Report(FightOutcome.Lost).Title, Is.EqualTo("LOST"));
            Assert.That(Report(FightOutcome.Lost).ConsequenceLine, Is.EqualTo("LOST  -1 LIFE"), "current rule (SimVersion 3): lives");
            Assert.That(Report(FightOutcome.Lost).CarryLine, Does.EndWith("LIVES 2"));
            Assert.That(Report(FightOutcome.Draw).Title, Is.EqualTo("DRAW"));
            Assert.That(Report(FightOutcome.Draw).ConsequenceLine, Does.StartWith("DRAW COUNTS AS LOST"), "a draw counts as lost");
            Assert.That(Report(FightOutcome.Lost, retry: true).ConsequenceLine, Is.EqualTo("LOST - RETRY AT 60% HP"), "v4 retry wording");
            Assert.That(Report(FightOutcome.Lost, retry: true).NextHeader, Does.Contain("RETRY"));
            Assert.That(Report(FightOutcome.Win, over: true, won: true).RunLine, Is.EqualTo("RUN WON!"));
            Assert.That(Report(FightOutcome.Lost, over: true).RunLine, Is.EqualTo("RUN OVER"));
            Assert.That(FightReport.Number(87.25f), Is.EqualTo("87.3"));
            Assert.That(FightReport.Number(42f), Is.EqualTo("42"));
        }

        // ---- Screen ----

        [Test]
        public void ResultScreenShowsTheReportAndFitsTheScreen()
        {
            var v = RunResultView.Create(root.transform, art);
            foreach (var r in new[]
                     {
                         Report(FightOutcome.Win), Report(FightOutcome.Lost), Report(FightOutcome.Draw),
                         Report(FightOutcome.Draw, retry: true), Report(FightOutcome.Lost, over: true), Report(FightOutcome.Win, over: true, won: true),
                     })
            {
                v.Show(r);
                Assert.That(v.TitleText, Is.EqualTo(r.Title));
                Assert.That(v.Value(0), Is.EqualTo($"{HpText.Of(r.HpLeft)}/{HpText.Of(r.MaxHpInFight)}"));
                Assert.That(v.Value(1), Is.EqualTo(HpText.Floor(r.DamageDealt).ToString()), "whole number, rounded down");
                Assert.That(v.Value(2), Is.EqualTo(HpText.Floor(r.DamageTaken).ToString()));
                Assert.That(v.Value(3), Does.StartWith("+" + r.CoinsGained));
                Assert.That(v.ConsequenceText, Is.EqualTo(r.ConsequenceLine));
                if (r.RunOver) Assert.That(v.NextHeaderText, Is.Empty, "no next fight after the last one");
                else
                {
                    Assert.That(v.NextNameText, Is.EqualTo(WeaponNames.Of(r.NextWeaponId)));
                    Assert.That(v.NextStatsText, Does.Contain("ARENA " + ArenaRegistry.Get(r.NextArenaId).DisplayName));
                }
                Assert.That(v.ContinueButton.LabelText, Is.EqualTo(r.RunOver ? "SUMMARY" : "NEXT FIGHT"));
                AssertAllTextOnScreen(v.transform);
            }
        }

        [Test]
        public void ContinueIgnoresTheBannerTapAndThenWorks()
        {
            PixelInput.SetTestClock(0f);
            var v = RunResultView.Create(root.transform, art);
            var pressed = 0;
            v.ContinueRequested += () => pressed++;
            v.Hide();
            v.Show(Report(FightOutcome.Win));
            PixelInput.InjectTap(RunResultView.ContinueArea.center);
            PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));
            Assert.That(pressed, Is.Zero, "tap right away (closing the fight banner) is dropped");
            PixelInput.AdvanceTestClock(0.5f);
            PixelInput.InjectTap(RunResultView.ContinueArea.center);
            PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));
            Assert.That(pressed, Is.Zero, "still locked at 0.5 s (0.8 s lock)");
            PixelInput.AdvanceTestClock(0.4f);
            PixelInput.InjectTap(RunResultView.ContinueArea.center);
            PixelInput.Dispatch(() => root.GetComponentsInChildren<PixelButton>(false));
            Assert.That(pressed, Is.EqualTo(1));
            var area = RunResultView.ContinueArea;
            Assert.That(area.xMin >= -135f && area.xMax <= 135f && area.yMin >= -240f && area.yMax <= 240f, Is.True);
        }

        static void AssertAllTextOnScreen(Transform screen)
        {
            var count = 0;
            foreach (var t in screen.GetComponentsInChildren<PixelText>(false))
            {
                if (string.IsNullOrEmpty(t.Text.Trim())) continue;
                count++;
                var b = t.WorldBounds;
                Assert.That(b.xMin, Is.GreaterThanOrEqualTo(-135f), $"'{t.Text}' cut on the left");
                Assert.That(b.xMax, Is.LessThanOrEqualTo(135f), $"'{t.Text}' cut on the right");
                Assert.That(b.yMin, Is.GreaterThanOrEqualTo(-240f), $"'{t.Text}' cut at the bottom");
                Assert.That(b.yMax, Is.LessThanOrEqualTo(240f), $"'{t.Text}' cut at the top");
            }
            Assert.That(count, Is.GreaterThan(5));
        }
    }
}
