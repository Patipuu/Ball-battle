using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using NUnit.Framework;
using static BallBattle.SimTests.RunFixtures;

namespace BallBattle.SimTests
{
    /// <summary>Run lifecycle: start, results, lives, bosses, enemies.</summary>
    public class RunTests
    {
        [Test]
        public void StartOffersEveryUnlockedWeaponRegardlessOfSeed()
        {
            var weapons = UnlockRules.Weapons(Fresh);
            var choices = RunState.StartChoices(5, weapons);
            Assert.That(choices, Is.EqualTo(weapons));
            Assert.That(RunState.StartChoices(99, weapons), Is.EqualTo(choices));
            Assert.That(RunState.Start(5, weapons.Last(), Fresh).Build.WeaponId, Is.EqualTo(weapons.Last()));
            Assert.Throws<ArgumentException>(() => RunState.Start(5, "unknown", Fresh));
        }

        [Test]
        public void SameSeedAndChoicesReplayTheWholeRun()
        {
            List<string> Play()
            {
                var run = NewRun(1236);   // BB-6c: Rules 7 ends seed 1234 after 2 fights; 1236 plays 8
                run.Build.MaxHp = run.Build.Hp = 2000; // replay fixture must cover several fights independently of balance changes
                var log = new List<string>();
                while (!run.Over)
                {
                    if (run.CanReroll && run.FightIndex % 2 == 1) run.Reroll();
                    for (var i = 0; i < run.OfferCount; i++) if (run.CanPick(i)) { run.Pick(i); break; }
                    var m = Finish(run.CreateMatch());
                    log.Add($"{run.FightIndex}:{m.ComputeHash():X16}");
                    run.ApplyResult(m);
                    log.Add($"hp {run.Build.Hp} coins {run.Coins} retries {run.RetryCount}");
                }
                return log;
            }

            var a = Play();
            Assert.That(a.Count, Is.GreaterThanOrEqualTo(6));
            Assert.That(Play(), Is.EqualTo(a));
        }

        [Test]
        public void WinKeepsHpAndHealsAShareCapped()
        {
            var run = NewRun();
            run.ApplyResult(Forced(run, true, 0.5f));
            Assert.That(run.Build.Hp, Is.EqualTo(Fix.FromInt(FixMath.RoundToEven((RunTuning.WinHealPct + Fix.Ratio(1, 2)) * run.Build.MaxHp))), "HP kept in whole points (round half to even)");
            Assert.That(run.Coins, Is.EqualTo(RunTuning.StartCoins + RunTuning.WinCoins));
            Assert.That(run.RetryCount, Is.Zero);

            run.ApplyResult(Forced(run, true, 0.95f));
            Assert.That(run.Build.Hp, Is.EqualTo(run.Build.MaxHp), "heal never exceeds max HP");
        }

        [Test]
        public void LossUsesRetryAtSixtyPercentHpOnSameFight()
        {
            var run = NewRun();
            run.ApplyResult(Forced(run, true, 0.3f));
            var arena = run.ArenaId;
            var enemy = run.Enemy.WeaponId;
            var firstSeed = run.FightSeed;
            run.ApplyResult(Forced(run, false));
            Assert.That(run.RetryCount, Is.EqualTo(1));
            Assert.That(run.Build.Hp.F(), Is.EqualTo((run.Build.MaxHp * RunTuning.RetryHealPct).F()).Within(0.001f));
            Assert.That(run.FightIndex, Is.EqualTo(1));
            Assert.That(run.IsRetry, Is.True);
            Assert.That(run.ArenaId, Is.EqualTo(arena));
            Assert.That(run.Enemy.WeaponId, Is.EqualTo(enemy));
            Assert.That(run.FightSeed, Is.Not.EqualTo(firstSeed));
            Assert.That(run.History.Last().Retry, Is.False);
            run.ApplyResult(Forced(run, true));
            Assert.That(run.History.Last().Retry, Is.True);
            Assert.That(run.IsRetry, Is.False);
        }

        [Test]
        public void TwoLossesEndTheRun()
        {
            var run = NewRun();
            for (var i = 0; i <= RunTuning.Retries; i++) run.ApplyResult(Forced(run, false));
            Assert.That(run.Over, Is.True);
            Assert.That(run.Won, Is.False);
            Assert.That(run.OfferCount, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => run.CreateMatch());
        }

        [Test]
        public void RunIsWonOnlyByWinningTheLastFight()
        {
            var won = NewRun();
            won.ApplyResult(Forced(won, false));
            for (var i = 0; i < RunTuning.Fights; i++) won.ApplyResult(Forced(won, true));
            Assert.That(won.Over && won.Won, Is.True);
            Assert.That(won.BossesDefeated, Is.EqualTo(2));

            var lost = NewRun();
            for (var i = 0; i < RunTuning.Fights - 1; i++) lost.ApplyResult(Forced(lost, true));
            lost.ApplyResult(Forced(lost, false));
            lost.ApplyResult(Forced(lost, false));
            Assert.That(lost.Over, Is.True);
            Assert.That(lost.Won, Is.False, "losing the final boss loses the run even with lives left");
        }

        [Test]
        public void BossesAreGiantWithMoreHpAndTraits()
        {
            // BB-04: HP and trait count come from the boss (BossTuning); the slot's default boss here (SHELL KING / MIMIC).
            for (var boss = 0; boss < 2; boss++)
            {
                var id = EnemyGenerator.DefaultBoss(RunTuning.BossFights[boss]);
                var e = EnemyGenerator.Generate(9, RunTuning.BossFights[boss]);
                Assert.That(e.BossId, Is.EqualTo(id));
                Assert.That(e.Radius, Is.EqualTo(RunTuning.BossRadius));
                Assert.That(e.MaxHp, Is.EqualTo(EnemyGenerator.BossHp(id)));
                Assert.That(e.MaxHp, Is.GreaterThan(RunTuning.PlayerStartHp));
                Assert.That(e.Traits.Count, Is.EqualTo(Math.Min(EnemyGenerator.BossTraitCount(id), BallBattle.Sim.Traits.TraitRegistry.All.Count)));
                Assert.That(e.Traits.Select(t => t.Id).Distinct().Count(), Is.EqualTo(e.Traits.Count));
            }
            var warden = EnemyGenerator.Generate(9, RunTuning.BossFights[1], BallBattle.Sim.Boss.BossIds.Warden);
            Assert.That(warden.Traits.Count, Is.EqualTo(BallBattle.Sim.Boss.BossTuning.WardenTraits), "WARDEN keeps the old fight-8 traits");
            var normal = EnemyGenerator.Generate(9, 0);
            Assert.That(normal.Radius.F(), Is.LessThanOrEqualTo(0f));
            Assert.That(normal.MaxHp, Is.EqualTo(RunTuning.PlayerStartHp));
        }

        [Test]
        public void StartPickIsFreeSingleAndUsesASeparateOffer()
        {
            var run = RunState.Start(77, RunState.StartChoices(77, UnlockRules.Weapons(Fresh))[0], Fresh);
            Assert.That(run.StartPickPending, Is.True);
            Assert.That(run.CanReroll, Is.False);
            Assert.That(Enumerable.Range(0, run.OfferCount).Select(run.OfferCard).All(c => c.Kind != CardKind.Heal), Is.True);
            var coins = run.Coins;
            run.Pick(0);
            Assert.That(run.StartPickPending, Is.False);
            Assert.That(run.Coins, Is.EqualTo(coins));
            Assert.That(run.PicksTaken, Is.Zero);
            Assert.That(run.CanReroll, Is.True);
        }

        [Test]
        public void EnemiesComeFromThePoolsSnapshottedAtStart()
        {
            var run = NewRun();
            var preview = run.Enemy;
            run.EnemyWeapons.Clear();
            run.EnemyWeapons.Add("pike");
            run.EnemyTraits.Clear();
            run.EnemyTraits.Add("spiky");
            var e = run.Enemy;
            Assert.That(e.WeaponId, Is.EqualTo("pike"), "the run's own pool decides, not the live registry");
            Assert.That(e.Traits.All(t => t.Id == "spiky"), Is.True);
            Assert.That(preview.WeaponId, Is.Not.Null);
        }
    }
}
