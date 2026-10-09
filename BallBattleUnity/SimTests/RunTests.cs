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
        public void StartOffersThreeDistinctUnlockedWeapons()
        {
            var weapons = UnlockRules.Weapons(Fresh);
            var choices = RunState.StartChoices(5, weapons);
            Assert.That(choices.Count, Is.EqualTo(3));
            Assert.That(choices.Distinct().Count(), Is.EqualTo(3));
            Assert.That(choices.All(weapons.Contains));
            var other = weapons.First(w => !choices.Contains(w));
            Assert.Throws<ArgumentException>(() => RunState.Start(5, other, Fresh));
        }

        [Test]
        public void SameSeedAndChoicesReplayTheWholeRun()
        {
            List<string> Play()
            {
                var run = NewRun(1234);
                var log = new List<string>();
                while (!run.Over)
                {
                    if (run.CanReroll && run.FightIndex % 2 == 1) run.Reroll();
                    for (var i = 0; i < run.OfferCount; i++) if (run.CanPick(i)) { run.Pick(i); break; }
                    var m = Finish(run.CreateMatch());
                    log.Add($"{run.FightIndex}:{m.ComputeHash():X16}");
                    run.ApplyResult(m);
                    log.Add($"hp {run.Build.Hp} coins {run.Coins} lives {run.Lives}");
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
            Assert.That(run.Build.Hp, Is.EqualTo(50f + 100f * RunTuning.WinHealPct).Within(1e-3f));
            Assert.That(run.Coins, Is.EqualTo(RunTuning.StartCoins + RunTuning.WinCoins));
            Assert.That(run.Lives, Is.EqualTo(RunTuning.Lives));

            run.ApplyResult(Forced(run, true, 0.95f));
            Assert.That(run.Build.Hp, Is.EqualTo(run.Build.MaxHp), "heal never exceeds max HP");
        }

        [Test]
        public void LossCostsALifeAndRefillsHp()
        {
            var run = NewRun();
            run.ApplyResult(Forced(run, true, 0.3f));
            run.ApplyResult(Forced(run, false));
            Assert.That(run.Lives, Is.EqualTo(RunTuning.Lives - 1));
            Assert.That(run.Build.Hp, Is.EqualTo(run.Build.MaxHp));
            Assert.That(run.FightIndex, Is.EqualTo(2), "the run moves on after a loss");
        }

        [Test]
        public void ThreeLossesEndTheRun()
        {
            var run = NewRun();
            for (var i = 0; i < RunTuning.Lives; i++) run.ApplyResult(Forced(run, false));
            Assert.That(run.Over, Is.True);
            Assert.That(run.Won, Is.False);
            Assert.That(run.OfferCount, Is.EqualTo(0));
            Assert.Throws<InvalidOperationException>(() => run.CreateMatch());
        }

        [Test]
        public void RunIsWonOnlyByWinningTheLastFight()
        {
            var won = NewRun();
            for (var i = 0; i < RunTuning.Fights; i++) won.ApplyResult(Forced(won, i != 0));
            Assert.That(won.Over && won.Won, Is.True);
            Assert.That(won.BossesDefeated, Is.EqualTo(3));

            var lost = NewRun();
            for (var i = 0; i < RunTuning.Fights; i++) lost.ApplyResult(Forced(lost, i != RunTuning.Fights - 1));
            Assert.That(lost.Over, Is.True);
            Assert.That(lost.Won, Is.False, "losing the final boss loses the run even with lives left");
        }

        [Test]
        public void BossesAreGiantWithMoreHpAndTraits()
        {
            for (var stage = 0; stage < 3; stage++)
            {
                var e = EnemyGenerator.Generate(9, RunTuning.BossFights[stage]);
                Assert.That(e.Radius, Is.EqualTo(RunTuning.BossRadius));
                Assert.That(e.MaxHp, Is.EqualTo(RunTuning.BossHp[stage]));
                Assert.That(e.Traits.Count, Is.EqualTo(Math.Min(RunTuning.BossTraits[stage], BallBattle.Sim.Traits.TraitRegistry.All.Count)));
                Assert.That(e.Traits.Select(t => t.Id).Distinct().Count(), Is.EqualTo(e.Traits.Count));
            }
            var normal = EnemyGenerator.Generate(9, 0);
            Assert.That(normal.Radius, Is.LessThanOrEqualTo(0f));
            Assert.That(normal.MaxHp, Is.EqualTo(RunTuning.PlayerStartHp));
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
