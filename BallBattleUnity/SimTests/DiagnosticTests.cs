using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Pacing report with default MatchConfig (not a gate). Run: dotnet test --filter Category=Diagnostic --logger "console;verbosity=detailed"</summary>
    [Explicit, Category("Diagnostic")]
    public class DiagnosticTests
    {
        static void Report(string name, System.Func<WeaponRule> make)
        {
            var secs = new List<float>();
            int caps = 0, hits = 0, parries = 0;
            for (uint seed = 1; seed <= 300; seed++)
            {
                var m = new MatchSim(new MatchConfig(), seed, new[] { make(), make() });
                while (m.Outcome == MatchOutcome.Ongoing)
                {
                    m.Step();
                    foreach (var e in m.Events)
                    {
                        if (e.Type == SimEventType.Hit) hits++;
                        else if (e.Type == SimEventType.Parry) parries++;
                    }
                }
                secs.Add(m.ActiveTick / 60f);
                if (m.EndReason == MatchEndReason.TimeCap) caps++;
            }
            secs.Sort();
            var minutes = secs.Sum() / 60f;
            TestContext.Out.WriteLine($"{name}: median {secs[secs.Count / 2]:0.0}s p90 {secs[(int)(secs.Count * 0.9)]:0.0}s cap {caps}/300 hits/min {hits / minutes:0.0} parries/min {parries / minutes:0.0}");
        }

        [Test]
        public void PacingReport()
        {
            Report("fixed dmg 3", () => new FixedBladeRule(3f));
            Report("scaling +1", () => new ScalingBladeRule());
        }
    }
}