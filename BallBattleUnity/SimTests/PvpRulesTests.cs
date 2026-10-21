using System;
using System.Linq;
using BallBattle.Sim.Weapons;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Pvp;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    [TestFixture]
    public sealed class PvpRulesTests
    {
        static PvpBallSnapshot Ball(string id, int level = 1) =>
            new PvpBallSnapshot(new BallProgress { Id = id, Level = level });

        [Test]
        public void BlindBansRemoveTheUnionAndSelectTheFirstLegalDefenseBall()
        {
            var attack = new[] { Ball("blade"), Ball("fang"), Ball("pike") };
            var defense = new[] { Ball("fang"), Ball("pike"), Ball("brawler") };
            var draft = PvpDraft.Resolve(new PvpBan("fang", ArenaRegistry.Classic),
                new PvpBan("pike", ArenaRegistry.Pillar), "blade", attack, defense, 0);
            Assert.That(draft.BannedBalls, Is.EqualTo(new[] { "fang", "pike" }));
            Assert.That(draft.BannedArenas, Is.EqualTo(new[] { ArenaRegistry.Classic, ArenaRegistry.Pillar }));
            Assert.That(draft.Defender.Id, Is.EqualTo("brawler"));
            Assert.That(draft.ArenaId, Is.EqualTo(ArenaRegistry.Bumpers));
            Assert.Throws<ArgumentException>(() => PvpDraft.Resolve(new PvpBan("fang", ArenaRegistry.Classic),
                new PvpBan("pike", ArenaRegistry.Pillar), "fang", attack, defense, 0));
        }

        [Test]
        public void DuplicateBlindBansAreValidAndRemoveAnOptionOnce()
        {
            var draft = PvpDraft.Resolve(new PvpBan("fang", ArenaRegistry.Classic),
                new PvpBan("fang", ArenaRegistry.Classic), "blade", new[] { Ball("blade") },
                new[] { Ball("fang"), Ball("pike"), Ball("brawler") }, 0);
            Assert.That(draft.BannedBalls, Is.EqualTo(new[] { "fang" }));
            Assert.That(draft.BannedArenas, Is.EqualTo(new[] { ArenaRegistry.Classic }));
            Assert.That(draft.Defender.Id, Is.EqualTo("pike"));
            Assert.That(draft.ArenaId, Is.EqualTo(ArenaRegistry.Pillar));
        }

        [Test]
        public void BothPlayersCarryTheirOwnLevelsAndTreesIntoFreshLoadouts()
        {
            var attacker = new PvpBallSnapshot(new BallProgress
            {
                Id = "blade", Level = 4, Vit = 1, Pow = 1, Growth = 1, SkillUps = 1, Branch = 1
            });
            var defender = new PvpBallSnapshot(new BallProgress { Id = "fang", Level = 3, Vit = 2 });
            var draft = PvpDraft.Resolve(new PvpBan("pike", ArenaRegistry.Classic),
                new PvpBan("brawler", ArenaRegistry.Pillar), "blade", new[] { attacker },
                new[] { defender, Ball("venom"), Ball("aegis") }, 42);
            var a = draft.Challenger.ToLoadout();
            var b = draft.Defender.ToLoadout();
            // The trees are carried (before the PvP power table)...
            var aBuild = draft.Challenger.ToBuild();
            var bBuild = draft.Defender.ToBuild();
            Assert.That(aBuild.MaxHp, Is.GreaterThan(BallBattle.Sim.Run.RunTuning.PlayerStartHp));
            Assert.That(bBuild.MaxHp, Is.GreaterThan(aBuild.MaxHp));
            Assert.That(aBuild.DamagePct, Is.GreaterThan(Fix.Zero));
            Assert.That(bBuild.DamagePct, Is.EqualTo(Fix.Zero));
            // ...and the loadout is that build with the PvP power table (Rules 24) on top.
            PvpTuning.Apply(aBuild);
            PvpTuning.Apply(bBuild);
            Assert.That(a.MaxHp, Is.EqualTo(aBuild.MaxHp));
            Assert.That(b.MaxHp, Is.EqualTo(bBuild.MaxHp));
            Assert.That(a.Bonus.DamagePct, Is.EqualTo(aBuild.DamagePct));
            Assert.That(a.SkillLevel, Is.EqualTo(2));
            Assert.That(b.SkillLevel, Is.EqualTo(1));
            Assert.That(a.Tree.Growth, Is.EqualTo(1));
            Assert.That(b.Tree.Growth, Is.Zero);
            Assert.That(a.Hp, Is.EqualTo(a.MaxHp));
            Assert.That(b.Hp, Is.EqualTo(b.MaxHp));
            Assert.That(a.TapEnabled && b.TapEnabled, Is.True, "both sides use the active tree skill policy");
        }

        [Test]
        public void MmrRewardsUpsetsAndChargesFavouritesMoreForLosses()
        {
            Assert.That(PvpRating.Delta(1000, 1000, 0, 1), Is.EqualTo(16));
            Assert.That(PvpRating.Delta(1000, 1000, 0, 0), Is.EqualTo(-16));
            Assert.That(PvpRating.Delta(1000, 1000, 10, 1), Is.EqualTo(12));
            Assert.That(PvpRating.Delta(1000, 1200, 10, 1), Is.GreaterThan(12));
            Assert.That(PvpRating.Delta(1000, 800, 10, 1), Is.LessThan(12));
            Assert.That(PvpRating.Delta(1000, 800, 10, 0), Is.LessThan(-12));
            Assert.That(PvpRating.Delta(1000, 1000, 10, -1), Is.Zero);
            Assert.That(PvpRating.Delta(100, 1200, 10, 0), Is.Zero);
        }

        [Test]
        public void ThreeOffersAreRealDistinctNearbyAccountsWithRecentRematchesExcluded()
        {
            var now = new DateTime(2026, 10, 20, 8, 0, 0, DateTimeKind.Utc);
            var pool = new[]
            {
                Candidate("self", 1000, 3), Candidate("lower", 890, 3),
                Candidate("equal", 1010, 3), Candidate("higher", 1120, 3),
                Candidate("fake", 1000, 3, verified: false),
                Candidate("recent", 990, 3, last: now.AddHours(-1)),
                Candidate("far", 1800, 9)
            };
            var offers = PvpMatchmaking.ThreeOffers("self", 1000, 3, now, pool);
            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(new[] { offers[0].AccountId, offers[1].AccountId, offers[2].AccountId },
                Is.EquivalentTo(new[] { "lower", "equal", "higher" }));
            Assert.That(PvpMatchmaking.ThreeOffers("self", 1000, 3, now,
                new[] { Candidate("one", 1000, 3), Candidate("two", 1010, 3) }), Is.Empty);
        }

        [Test]
        public void RankedSeriesReplaysFromTheServerSeedAndResolvedDraft()
        {
            var draft = PvpDraft.Resolve(new PvpBan("pike", ArenaRegistry.Classic),
                new PvpBan("brawler", ArenaRegistry.Pillar), "blade", new[] { Ball("blade", 4) },
                new[] { Ball("fang", 4), Ball("venom"), Ball("aegis") }, 501);
            var a = PvpSeries.Run(draft, 714);
            var b = PvpSeries.Run(draft, 714);
            Assert.That(a.Winner, Is.EqualTo(b.Winner));
            Assert.That(a.RoundSeeds, Is.EqualTo(b.RoundSeeds));
            Assert.That(a.RoundHashes, Is.EqualTo(b.RoundHashes));
            Assert.That(a.RoundSeeds.Length, Is.InRange(2, PvpSeries.MaxRounds));
            Assert.That(a.ChallengerWins + a.DefenderWins + a.Draws, Is.EqualTo(a.RoundSeeds.Length));
            Assert.That(a.Winner == -1 || a.ChallengerWins == 2 || a.DefenderWins == 2, Is.True);
        }

        [Test]
        public void PvpPowerTableCoversEveryRankedBallAndBranch()
        {
            foreach (var w in WeaponRegistry.All)
            {
                if (WeaponRegistry.IsPlayerOnly(w.Id)) continue;
                Assert.That(PvpTuning.Power.ContainsKey(w.Id), Is.True, w.Id);
                Assert.That(PvpTuning.Power[w.Id].Length, Is.EqualTo(3), w.Id);
            }
            var hive = new BallBattle.Sim.Run.RunBuild { WeaponId = "rig" };
            var hp = hive.MaxHp;
            PvpTuning.Apply(hive);
            Assert.That(hive.MaxHp < hp, Is.True, "HIVE is weaker in PvP than in Run");
        }

        [Test]
        public void LiveSeriesReplaysTheChallengersPressesExactly()
        {
            var draft = PvpDraft.ForFight(ArenaRegistry.Classic, Ball("blade", 4), Ball("fang", 4));
            // Played like the client: a press every 40 steps, recorded as (round, step) before that step runs.
            var taps = new System.Collections.Generic.List<PvpTap>();
            var hashes = new System.Collections.Generic.List<ulong>();
            int mine = 0, theirs = 0;
            for (var r = 0; r < PvpSeries.MaxRounds && mine < 2 && theirs < 2; r++)
            {
                var sim = PvpSeries.CreateRound(draft, 714, r, out var me);
                Assert.That(me, Is.EqualTo(r % 2), "sides swap every round");
                Assert.That(sim.Balls[1 - me].AutoSkill, Is.True, "the defender's bot presses its skill");
                Assert.That(sim.Balls[me].AutoSkill, Is.False, "the challenger presses their own");
                var step = 0;
                while (sim.Outcome == BallBattle.Sim.MatchOutcome.Ongoing && step < PvpSeries.MaxStepsPerRound)
                {
                    if (step % 40 == 7) { taps.Add(new PvpTap(r, step)); sim.Tap(me); }
                    sim.Step();
                    step++;
                }
                hashes.Add(sim.ComputeHash());
                if (sim.WinnerIndex == me) mine++; else if (sim.WinnerIndex >= 0) theirs++;
            }
            var server = PvpSeries.Run(draft, 714, taps);
            Assert.That(server.RoundHashes, Is.EqualTo(hashes.ToArray()), "the server's rounds are the ones the client played");
            Assert.That(PvpSeries.Run(draft, 714).RoundHashes, Is.Not.EqualTo(server.RoundHashes), "presses change the fight");
        }

        static PvpCandidate Candidate(string id, int mmr, int level, bool verified = true, DateTime last = default, bool bot = false) =>
            new PvpCandidate { AccountId = id, Mmr = mmr, AverageDefenseLevel = level,
                Verified = verified, LastChallengedUtc = last, IsBot = bot };

        [Test]
        public void BotsOnlyFillWhatRealPlayersCannot()
        {
            var now = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);
            var bots = new[] { Candidate("bot-a", 900, 3, bot: true), Candidate("bot-b", 1000, 3, bot: true), Candidate("bot-c", 1100, 3, bot: true) };
            var humans = new[] { Candidate("h1", 890, 3), Candidate("h2", 1010, 3), Candidate("h3", 1120, 3) };
            var offers = PvpMatchmaking.ThreeOffers("self", 1000, 3, now, humans.Concat(bots));
            Assert.That(offers.Select(o => o.AccountId), Is.EquivalentTo(new[] { "h1", "h2", "h3" }), "three real players: no bot");

            // Two real players, both near: both kept, one bot fills the missing slot.
            offers = PvpMatchmaking.ThreeOffers("self", 1000, 3, now, new[] { Candidate("h1", 1000, 3), Candidate("h2", 1020, 3) }.Concat(bots));
            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(offers.Select(o => o.AccountId), Does.Contain("h1").And.Contain("h2"));
            Assert.That(offers.Count(o => o.IsBot), Is.EqualTo(1));

            // Nobody real: three bots spread lower / even / higher.
            offers = PvpMatchmaking.ThreeOffers("self", 1000, 3, now, bots);
            Assert.That(offers.Select(o => o.AccountId), Is.EquivalentTo(new[] { "bot-a", "bot-b", "bot-c" }));

            // Far from every bot band: the nearest bots still fill in.
            Assert.That(PvpMatchmaking.ThreeOffers("self", 2600, 10, now, bots).Count, Is.EqualTo(3));
        }

        [Test]
        public void HouseBotsAreLegalLabelledAndCoverEveryLevel()
        {
            var all = PvpBots.All();
            Assert.That(all.Count, Is.EqualTo(PvpBots.Count));
            Assert.That(all.Select(b => b.Id).Distinct().Count(), Is.EqualTo(all.Count));
            for (var level = 1; level <= MetaTuning.MaxLevel; level++) Assert.That(all.Count(b => b.Level == level), Is.EqualTo(PvpBots.PerLevel));
            foreach (var bot in all)
            {
                Assert.That(bot.Name, Does.StartWith(PvpBots.NamePrefix));
                Assert.That(bot.Name.Length, Is.LessThanOrEqualTo(18));
                Assert.That(PvpBots.IsBot(bot.Id), Is.True);
                Assert.That(bot.Roster.Distinct().Count(), Is.EqualTo(3));
                foreach (var ball in bot.Balls)
                {
                    var snap = new PvpBallSnapshot(ball);   // throws on an illegal tree
                    Assert.That(WeaponRegistry.IsPlayerOnly(ball.Id), Is.False);
                    Assert.That(MetaTree.Free(ball), Is.LessThanOrEqualTo(1), $"{bot.Name} spends its points");
                    Assert.That(Math.Abs(ball.Level - bot.Level), Is.LessThanOrEqualTo(1));
                }
                Assert.That(bot.Roster, Does.Not.Contain(bot.BanBall));
                new PvpBan(bot.BanBall, bot.BanArena);
                var again = PvpBots.Make(all.ToList().IndexOf(bot));
                Assert.That(again.Roster, Is.EqualTo(bot.Roster), "same bot after a restart");
            }
            // A level-1 player at the start MMR finds three bots in the closest band.
            var now = new DateTime(2026, 10, 21, 8, 0, 0, DateTimeKind.Utc);
            var pool = all.Select(b => Candidate(b.Id, b.Mmr, b.Level, bot: true));
            var offers = PvpMatchmaking.ThreeOffers("self", PvpRating.Initial, 1, now, pool);
            Assert.That(offers.Count, Is.EqualTo(3));
            Assert.That(offers.All(o => Math.Abs(o.Mmr - PvpRating.Initial) <= 150 && o.AverageDefenseLevel <= 3), Is.True);
        }
    }
}
