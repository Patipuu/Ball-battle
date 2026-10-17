using System;
using System.IO;
using System.Linq;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-18 diagnostic: how fast BGLGXLQ lights colours and completes rings against each ball (chorus on side 0, seeds 1-100).</summary>
    [Explicit("Diagnostic"), Category("Slow"), Category("Diagnostic"), NonParallelizable]
    public class ChorusRingsDiag
    {
        [Test]
        public void RingsPerMinute()
        {
            var sb = new StringBuilder("| vs | win % | lights/min | rings/min | median s |\n|---|---|---|---|---|\n");
            foreach (var e in WeaponRegistry.All.Where(e => e.Id != ChorusRule.WeaponId))
            {
                int wins = 0; long lights = 0, rings = 0, ticks = 0;
                var secs = new System.Collections.Generic.List<float>();
                for (uint seed = 1; seed <= 100; seed++)
                {
                    var m = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(ChorusRule.WeaponId), WeaponRegistry.Create(e.Id) });
                    while (m.Outcome == MatchOutcome.Ongoing && m.Tick < 60 * 240)
                    {
                        m.Step();
                        foreach (var ev in m.Events) if (ev.A == 0 && ev.Type == SimEventType.ChorusLit) lights++;
                    }
                    rings += ((ChorusRule)m.Balls[0].Weapon).Rings;
                    ticks += m.ActiveTick;
                    secs.Add(m.ActiveTick / 60f);
                    if (m.Outcome == MatchOutcome.Win && m.WinnerIndex == 0) wins++;
                }
                secs.Sort();
                var minutes = ticks / 3600f;
                sb.AppendLine($"| {e.Id} | {wins} | {lights / minutes:0.0} | {rings / minutes:0.00} | {secs[secs.Count / 2]:0.0} |");
            }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
            [Test]
        public void EventBreakdown()
        {
            var sb = new StringBuilder();
            foreach (var opp in new[] { "brawler", "serpent", "blade", "aegis" })
            {
                var agg = new System.Collections.Generic.SortedDictionary<string, (long n, double v)>();
                long ticks = 0;
                for (uint seed = 1; seed <= 100; seed++)
                {
                    var m = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(ChorusRule.WeaponId), WeaponRegistry.Create(opp) });
                    while (m.Outcome == MatchOutcome.Ongoing && m.Tick < 60 * 240)
                    {
                        m.Step();
                        foreach (var ev in m.Events)
                        {
                            var role = ev.A == 0 ? "A=chorus" : ev.B == 0 ? "B=chorus" : null;
                            if (role == null) continue;
                            var k = ev.Type + " " + role + (ev.Type == SimEventType.Damage ? " kind" + ev.Variant : "");
                            agg.TryGetValue(k, out var x);
                            agg[k] = (x.n + 1, x.v + ev.Value.ToFloat());
                        }
                    }
                    ticks += m.ActiveTick;
                }
                var minutes = ticks / 3600.0;
                sb.AppendLine($"### vs {opp} ({minutes:0} min)");
                foreach (var kv in agg) sb.AppendLine($"{kv.Key}: {kv.Value.n / minutes:0.0}/min, value {kv.Value.v / minutes:0.0}/min");
            }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
