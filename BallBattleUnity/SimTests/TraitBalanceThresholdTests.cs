using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim.Traits;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Regression gate for trait balance (Phase 3). Fixed seeds, so exact for a given build: a trait change that
    /// pushes level 1 out of +3..+15 win-rate points, or makes level 2 weaker than level 1, fails here. ~2 min.
    /// </summary>
    [Category("Balance"), NonParallelizable]
    public class TraitBalanceThresholdTests
    {
        const int Seeds = 24;
        static readonly Dictionary<string, (double l1, double l2)> deltas = new Dictionary<string, (double, double)>();

        [OneTimeSetUp]
        public void Measure()
        {
            foreach (var e in TraitRegistry.All)
                deltas[e.Id] = (TraitBalanceReport.Delta(e.Id, 1, Seeds), TraitBalanceReport.Delta(e.Id, 2, Seeds));
        }

        static string Table() => string.Join("\n", deltas.Select(d => $"{d.Key}: L1 {d.Value.l1:+0.0;-0.0} L2 {d.Value.l2:+0.0;-0.0}"));

        [Test]
        public void EveryTraitAtLevel1AddsThreeToFifteenPoints()
        {
            var bad = deltas.Where(d => d.Value.l1 < 3.0 || d.Value.l1 > 15.0).Select(d => $"{d.Key} {d.Value.l1:+0.0;-0.0}").ToList();
            Assert.That(bad, Is.Empty, Table());
        }

        [Test]
        public void Level2IsStrongerThanLevel1()
        {
            var bad = deltas.Where(d => d.Value.l2 <= d.Value.l1).Select(d => d.Key).ToList();
            Assert.That(bad, Is.Empty, Table());
        }
    }
}