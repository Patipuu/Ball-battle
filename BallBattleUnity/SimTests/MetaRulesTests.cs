using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-12a: chest tiers, seeded chest draws, ball levels and upgrades (Sim/Meta).</summary>
    public class MetaRulesTests
    {
        static ProgressData Fresh() => new ProgressData();

        [Test]
        public void CostTableMatchesTheDesign()
        {
            Assert.That(MetaTuning.ShardCost.Sum(), Is.EqualTo(400));
            Assert.That(MetaTuning.GoldCost.Sum(), Is.EqualTo(735));
            Assert.That(MetaTuning.ShardCost.Length, Is.EqualTo(MetaTuning.MaxLevel - 1));
            Assert.That(MetaRules.ShardsToNext(MetaTuning.MaxLevel), Is.Zero);
            Assert.That(MetaRules.TreePoints(1), Is.EqualTo(1));
            Assert.That(MetaRules.TreePoints(10), Is.EqualTo(10));
            Assert.That(MetaRules.TreePoints(99), Is.EqualTo(10));
        }

        [Test]
        public void TierFollowsHowFarTheRunWent()
        {
            Assert.That(MetaRules.TierFor(false, -1, 0), Is.Null, "no fight played: no chest");
            Assert.That(MetaRules.TierFor(false, -1, 1), Is.EqualTo(ChestTier.Wood), "lost the first fight");
            Assert.That(MetaRules.TierFor(false, RunTuning.BossFights[0] - 1, 4), Is.EqualTo(ChestTier.Wood), "lost to boss 1");
            Assert.That(MetaRules.TierFor(false, RunTuning.BossFights[0], 5), Is.EqualTo(ChestTier.Silver), "beat boss 1");
            Assert.That(MetaRules.TierFor(true, RunTuning.Fights - 1, RunTuning.Fights), Is.EqualTo(ChestTier.Gold));
        }

        [Test]
        public void TierFromARunState()
        {
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(Fresh()))[0], Fresh());
            Assert.That(MetaRules.TierFor(run), Is.Null);
            for (var i = 0; i <= RunTuning.BossFights[0]; i++) run.History.Add(new FightRecord { FightIndex = i, Won = true });
            run.History.Add(new FightRecord { FightIndex = RunTuning.BossFights[0] + 1, Won = false });
            run.Over = true;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Silver));
            run.Won = true;
            Assert.That(MetaRules.TierFor(run), Is.EqualTo(ChestTier.Gold));
        }

        [TestCase(ChestTier.Wood)]
        [TestCase(ChestTier.Silver)]
        [TestCase(ChestTier.Gold)]
        public void AChestGivesItsTableAndIsApplied(ChestTier tier)
        {
            var p = Fresh();
            var r = MetaRules.Open(p, tier, "blade", 77);
            var t = (int)tier;
            Assert.That(r.Shards.Sum(s => s.Count), Is.EqualTo(MetaTuning.PlayedShards[t] + MetaTuning.RandomShards[t]));
            Assert.That(r.Shards[0].Ball, Is.EqualTo("blade"), "played ball first");
            Assert.That(r.Shards[0].Count, Is.GreaterThanOrEqualTo(MetaTuning.PlayedShards[t]));
            Assert.That(r.Gold, Is.EqualTo(MetaTuning.ChestGold[t]));
            Assert.That(p.Gold, Is.EqualTo(r.TotalGold));
            Assert.That(p.ChestsOpened, Is.EqualTo(1));
            foreach (var s in r.Shards) Assert.That(MetaRules.Ball(p, s.Ball).Shards, Is.EqualTo(s.Count));
        }

        [Test]
        public void SameSeedAndCountSameChestNextChestDiffers()
        {
            string Draw(ProgressData p) => string.Join(",", MetaRules.Open(p, ChestTier.Gold, "fang", 123).Shards.Select(s => s.Ball + s.Count));
            var a = Fresh();
            var b = Fresh();
            Assert.That(Draw(a), Is.EqualTo(Draw(b)), "deterministic");
            Assert.That(MetaRules.ChestSeed(123, 0), Is.Not.EqualTo(MetaRules.ChestSeed(123, 1)), "the next chest uses another seed");
        }

        [Test]
        public void RandomShardsOnlyGoToUnlockedBallsWithEvenOdds()
        {
            var p = Fresh();
            var unlocked = UnlockRules.Weapons(p);
            var counts = new Dictionary<string, int>();
            for (var i = 0; i < 2000; i++)
                foreach (var s in MetaRules.Open(p, ChestTier.Gold, "", (uint)i).Shards)
                    counts[s.Ball] = (counts.TryGetValue(s.Ball, out var c) ? c : 0) + s.Count;
            Assert.That(counts.Keys, Is.SubsetOf(unlocked), "locked balls get no shards");
            var mean = counts.Values.Sum() / (double)unlocked.Count;
            foreach (var id in unlocked)
                Assert.That(counts[id], Is.InRange(mean * 0.9, mean * 1.1), id);
        }

        [Test]
        public void MaxedBallShardsBecomeGold()
        {
            var p = Fresh();
            MetaRules.Ball(p, "blade").Level = MetaTuning.MaxLevel;
            var r = MetaRules.Open(p, ChestTier.Wood, "blade", 9);
            var bladeShards = r.Shards.Where(s => s.Ball == "blade").Sum(s => s.Count);
            Assert.That(r.ConvertedGold, Is.EqualTo(bladeShards * MetaTuning.MaxedShardGold));
            Assert.That(MetaRules.Ball(p, "blade").Shards, Is.Zero);
            Assert.That(p.Gold, Is.EqualTo(MetaTuning.ChestGold[0] + r.ConvertedGold));
        }

        [Test]
        public void UpgradeSpendsExactlyAndStopsAtTen()
        {
            var p = Fresh();
            Assert.That(MetaRules.Upgrade(p, "pike"), Is.False, "nothing to spend");
            Assert.That(MetaRules.LevelOf(p, "pike"), Is.EqualTo(1));
            MetaRules.Ball(p, "pike").Shards = 400 + 7;
            p.Gold = 735 + 3;
            for (var i = 0; i < 20; i++) MetaRules.Upgrade(p, "pike");
            Assert.That(MetaRules.LevelOf(p, "pike"), Is.EqualTo(MetaTuning.MaxLevel));
            Assert.That((MetaRules.Ball(p, "pike").Shards, p.Gold), Is.EqualTo((7, 3)));
            Assert.That(MetaRules.CanUpgrade(p, "pike"), Is.False);
        }

        [Test]
        public void UpgradeNeedsBothShardsAndGold()
        {
            var p = Fresh();
            MetaRules.Ball(p, "fang").Shards = 5;
            p.Gold = 9;
            Assert.That(MetaRules.Upgrade(p, "fang"), Is.False);
            p.Gold = 10;
            Assert.That(MetaRules.Upgrade(p, "fang"), Is.True);
            Assert.That((MetaRules.LevelOf(p, "fang"), MetaRules.Ball(p, "fang").Shards, p.Gold), Is.EqualTo((2, 0, 0)));
        }

        [Test]
        public void WelcomeChestsOnePerWinAtMostFive()
        {
            var p = new ProgressData { RunsWon = 3 };
            MetaRules.GrantWelcomeChests(p);
            Assert.That(p.PendingChests, Is.EqualTo(new[] { ChestTier.Silver, ChestTier.Silver, ChestTier.Silver }));
            var q = new ProgressData { RunsWon = 12 };
            MetaRules.GrantWelcomeChests(q);
            Assert.That(q.PendingChests.Count, Is.EqualTo(MetaTuning.MaxWelcomeChests));
            var r = MetaRules.OpenPending(q, 4);
            Assert.That(r.Tier, Is.EqualTo(ChestTier.Silver));
            Assert.That(r.PlayedBall, Is.Empty);
            Assert.That(r.Shards.Sum(s => s.Count), Is.EqualTo(MetaTuning.PlayedShards[1] + MetaTuning.RandomShards[1]));
            Assert.That(q.PendingChests.Count, Is.EqualTo(MetaTuning.MaxWelcomeChests - 1));
            Assert.That(MetaRules.OpenPending(new ProgressData(), 1), Is.Null);
        }

        [Test]
        public void AwardRunUsesThePlayedBallAndRunSeed()
        {
            var p = Fresh();
            var ball = RunState.StartChoices(42, UnlockRules.Weapons(p))[0];
            var run = RunState.Start(42, ball, p);
            run.History.Add(new FightRecord { FightIndex = 0, Won = false });
            run.Over = true;
            var r = MetaRules.AwardRun(p, run);
            Assert.That((r.Tier, r.PlayedBall), Is.EqualTo((ChestTier.Wood, ball)));
            Assert.That(MetaRules.Ball(p, ball).Shards, Is.GreaterThanOrEqualTo(MetaTuning.PlayedShards[0]));
            var empty = RunState.Start(43, RunState.StartChoices(43, UnlockRules.Weapons(p))[0], p);
            empty.Over = true;
            Assert.That(MetaRules.AwardRun(p, empty), Is.Null, "no fight, no chest");
        }
    }
}
