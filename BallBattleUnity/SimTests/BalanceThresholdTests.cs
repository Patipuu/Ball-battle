using System.Linq;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Regression gate for balance (plan acceptance #3). Seeds are fixed, so results are exact for a given build:
    /// a tuning or rule change that pushes any pair out of the band fails here. ~15 s on 16 threads.
    /// </summary>
    [Explicit("Slow fixture setup: per-class measurement 149.61 seconds"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class BalanceThresholdTests
    {
        const int SeedsPerSide = 200;

        static System.Collections.Generic.List<MatchupReport.PairStats> rows;

        [OneTimeSetUp]
        public void PlayMatrix() => rows = MatchupReport.PlayAll(SeedsPerSide);

        [Test]
        public void NonMirrorPairsWinBetween20And80Percent()
        {
            // BB-6c: band = the 6c target "no weapon pair above 80% (both sides combined)"; replaces the
            // temporary 30-70 band with its fang-vs-rig 72% exception.
            var bad = rows.Where(r => r.A != r.B && (r.WinPctA < 20f || r.WinPctA > 80f))
                          .Select(r => $"{r.A} vs {r.B}: {r.WinPctA:0.0}%").ToList();
            Assert.That(bad, Is.Empty, MatchupReport.Table(rows));
        }

        [Test]
        public void MedianMatchLengthBetween20And90Seconds()
        {
            var bad = rows.Where(r => r.MedianSeconds < 20f || r.MedianSeconds > 90f)
                          .Select(r => $"{r.A} vs {r.B}: {r.MedianSeconds:0.0}s").ToList();
            Assert.That(bad, Is.Empty, MatchupReport.Table(rows));
        }

        [Test]
        public void DrawsUnder5PercentAndCapUnder1Percent()
        {
            var bad = rows.Where(r => r.DrawPct >= 5f || r.CapPct >= 1f)
                          .Select(r => $"{r.A} vs {r.B}: draw {r.DrawPct:0.0}% cap {r.CapPct:0.0}%").ToList();
            Assert.That(bad, Is.Empty, MatchupReport.Table(rows));
        }
    }
}
