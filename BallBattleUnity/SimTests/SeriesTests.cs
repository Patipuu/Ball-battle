using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class SeriesTests
    {
        [Test]
        public void FirstToTwoWins()
        {
            var s = new Series("blade", "fang", 7);
            s.ReportRound(0);
            Assert.That(s.IsOver, Is.False);
            Assert.That(s.DisplayRound, Is.EqualTo(2));
            s.ReportRound(1);
            Assert.That(s.DisplayRound, Is.EqualTo(3));
            s.ReportRound(0);
            Assert.That(s.IsOver, Is.True);
            Assert.That(s.Winner, Is.EqualTo(0));
            Assert.That(s.WinsA, Is.EqualTo(2));
            Assert.That(s.WinsB, Is.EqualTo(1));
            Assert.Throws<System.InvalidOperationException>(() => s.ReportRound(1));
        }

        [Test]
        public void DrawIsReplayedWithNewSeedAndSameRoundNumber()
        {
            var s = new Series("blade", "fang", 7);
            var seed0 = s.CurrentRoundSeed;
            s.ReportRound(-1);
            Assert.That(s.Draws, Is.EqualTo(1));
            Assert.That(s.DisplayRound, Is.EqualTo(1));
            Assert.That(s.CurrentRoundSeed, Is.Not.EqualTo(seed0));
            Assert.That(s.IsOver, Is.False);
        }

        [Test]
        public void RoundSeedsAreDistinctAndNonZero()
        {
            var seeds = new HashSet<uint>();
            for (uint m = 0; m < 200; m++)
                for (var r = 0; r < 10; r++)
                {
                    var v = Series.SeedFor(m, r);
                    Assert.That(v, Is.Not.EqualTo(0u));
                    seeds.Add(v);
                }
            Assert.That(seeds.Count, Is.EqualTo(2000));
        }

        /// <summary>Plays a whole series headless; returns per-round final hashes and the series winner.</summary>
        static (List<ulong> hashes, int winner) PlaySeries(string a, string b, uint matchSeed)
        {
            var s = new Series(a, b, matchSeed);
            var hashes = new List<ulong>();
            while (!s.IsOver && s.RoundsPlayed < 20)
            {
                var m = new MatchSim(new MatchConfig(), s.CurrentRoundSeed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                hashes.Add(m.ComputeHash());
                s.ReportRound(m.WinnerIndex);
            }
            return (hashes, s.Winner);
        }

        [Test]
        public void RematchWithSameSeedReplaysTheWholeSeriesExactly()
        {
            var first = PlaySeries("pike", "brawler", 424242);
            var again = PlaySeries("pike", "brawler", 424242);
            Assert.That(again.hashes, Is.EqualTo(first.hashes));
            Assert.That(again.winner, Is.EqualTo(first.winner));
            Assert.That(first.hashes.Count, Is.InRange(2, 20));

            var other = PlaySeries("pike", "brawler", 424243);
            Assert.That(other.hashes.SequenceEqual(first.hashes), Is.False, "a new match seed gives a different series");
        }
    }
}
