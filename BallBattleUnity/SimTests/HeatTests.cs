using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using NUnit.Framework;
using static BallBattle.SimTests.RunFixtures;

namespace BallBattle.SimTests
{
    /// <summary>HEAT (21/10): three difficulty steps inside every chapter.</summary>
    public class HeatTests
    {
        static RunState Start(uint seed, int chapter, int heat)
        {
            var p = new ProgressData { RunsPlayed = 1 };
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(p));
            return RunState.Start(seed, choices[0], p, chapter: chapter, heat: heat);
        }

        [Test]
        public void HeatOneIsTheChapterAsBalanced()
        {
            for (uint seed = 1; seed <= 10; seed++)
            for (var f = 0; f < RunTuning.Fights; f++)
            {
                var a = Start(seed, 1, 0);
                a.FightIndex = f;
                var e = a.Enemy;
                var plain = EnemyGenerator.Generate(a.Seed, f, a.EnemyWeapons, a.EnemyTraits, a.CurrentBossId, Chapters.Get(1));
                Assert.That(e.MaxHp, Is.EqualTo(plain.MaxHp));
                Assert.That(e.Traits.Count, Is.EqualTo(plain.Traits.Count));
                Assert.That(e.DamagePct, Is.EqualTo(plain.DamagePct));
            }
            Assert.That(Start(1, 0, 0).MaxRetries, Is.EqualTo(RunTuning.Retries));
        }

        [Test]
        public void HigherHeatAddsTraitsHpAndBossPower()
        {
            for (uint seed = 1; seed <= 10; seed++)
            for (var f = 0; f < RunTuning.Fights; f++)
            {
                RunBuild[] e = new RunBuild[3];
                for (var h = 0; h < 3; h++) { var r = Start(seed, 2, h); r.FightIndex = f; e[h] = r.Enemy; }
                Assert.That(e[1].MaxHp, Is.GreaterThan(e[0].MaxHp), $"seed {seed} fight {f}");
                if (string.IsNullOrEmpty(e[0].BossId))
                {
                    Assert.That(e[1].WeaponId, Is.EqualTo(e[0].WeaponId), "heat never changes which ball a fight meets");
                    Assert.That(e[1].Traits.Count, Is.GreaterThanOrEqualTo(e[0].Traits.Count));
                    Assert.That(e[1].Traits.Count, Is.LessThanOrEqualTo(e[0].Traits.Count + 1));
                    Assert.That(e[2].MaxHp, Is.EqualTo(e[1].MaxHp), "heat 3 adds nothing to normal foes");
                }
                else
                {
                    Assert.That(e[2].MaxHp, Is.GreaterThan(e[1].MaxHp));
                    Assert.That(e[2].DamagePct, Is.GreaterThan(e[1].DamagePct));
                }
            }
        }

        [Test]
        public void HeatThreeHasNoRetry()
        {
            var run = Start(5, 0, 2);
            Assert.That(run.MaxRetries, Is.Zero);
            run.ApplyResult(Forced(run, false));
            Assert.That(run.Over, Is.True);
            Assert.That(run.RetryCount, Is.Zero);
            var easy = Start(5, 0, 1);
            easy.ApplyResult(Forced(easy, false));
            Assert.That(easy.Over, Is.False, "heat 2 keeps the retry");
        }

        [Test]
        public void WinningAHeatOpensTheNextHeatOfThatChapter()
        {
            var p = new ProgressData { RunsPlayed = 1 };
            Assert.That(HeatRules.Unlocked(p, 0), Is.Zero);
            var run = Start(1, 0, 0);
            run.Over = run.Won = true;
            UnlockRules.Record(p, run);
            Assert.That(HeatRules.Unlocked(p, 0), Is.EqualTo(1));
            Assert.That(run.Achievements, Does.Contain(HeatRules.Mark + "0:1"));
            Assert.That(Chapters.Unlocked(p), Is.EqualTo(1), "heat 1 also opens the next chapter");
            Assert.That(HeatRules.Unlocked(p, 1), Is.Zero);

            var h2 = Start(2, 0, 1);
            h2.Over = h2.Won = true;
            UnlockRules.Record(p, h2);
            Assert.That(HeatRules.Unlocked(p, 0), Is.EqualTo(2));
            var h3 = Start(3, 0, 2);
            h3.Over = h3.Won = true;
            UnlockRules.Record(p, h3);
            Assert.That(HeatRules.Cleared(p, 0), Is.EqualTo(3));
            Assert.That(HeatRules.Unlocked(p, 0), Is.EqualTo(2));
            Assert.That(h3.Achievements.Any(a => a.StartsWith(HeatRules.Mark)), Is.False, "no heat 4");

            var old = new ProgressData { RunsWon = 2, ChaptersCleared = 2 };   // a save from before heat
            Assert.That(HeatRules.Unlocked(old, 0), Is.EqualTo(1));
            Assert.That(HeatRules.Unlocked(old, 1), Is.EqualTo(1));
            Assert.That(HeatRules.Unlocked(old, 2), Is.Zero);
        }

        [Test]
        public void HeatRaisesTheChest()
        {
            var run = Start(1, 0, 0);
            run.History.Add(new FightRecord { FightIndex = 0, Won = true });
            run.Over = true;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Wood));
            run.Heat = 1;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Silver));
            run.Heat = 2;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Gold));
            run.Won = true;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Gold), "capped at GOLD");

            var a = new ProgressData(); var b = new ProgressData();
            var plain = MetaRules.Open(a, ChestTier.Wood, "", 9);
            var hot = MetaRules.Open(b, ChestTier.Wood, "", 9, HeatRules.ChestGoldBonusPct(2));
            Assert.That(hot.Gold, Is.EqualTo(plain.Gold * 2));
        }

        [Test]
        public void HeatIsValidatedAndClamped()
        {
            Assert.That(Start(1, 0, 7).Heat, Is.EqualTo(HeatRules.Count - 1));
            var run = Start(1, 0, 0);
            Assert.That(RunValidation.IsValid(run), Is.True);
            run.Heat = 3;
            Assert.That(RunValidation.IsValid(run), Is.False);
            run.Heat = 2; run.RetryCount = 1;
            Assert.That(RunValidation.IsValid(run), Is.False, "heat 3 runs never hold a used retry");
        }
    }
}
