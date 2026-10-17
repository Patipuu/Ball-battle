using System;
using System.IO;
using System.Linq;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-18: BGLGXLQ in Versus against every other ball (both sides, fixed seeds). Set BB_REPORT to save the table.</summary>
    [Explicit("Slow balance report"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class ChorusOrbReport
    {
        [Test]
        public void ChorusVersusEveryBall()
        {
            var seeds = int.TryParse(Environment.GetEnvironmentVariable("BB_BAL_SEEDS"), out var n) ? n : 200;
            var sb = new StringBuilder("| vs | chorus win % | draw % | median s |\n|---|---|---|---|\n");
            var total = 0f; var count = 0;
            foreach (var e in WeaponRegistry.All.Where(e => e.Id != ChorusRule.WeaponId))
            {
                var s = MatchupReport.Play(ChorusRule.WeaponId, e.Id, seeds);
                sb.AppendLine($"| {e.Id} | {s.WinPctA:0.0} | {s.DrawPct:0.0} | {s.MedianSeconds:0.0} |");
                total += s.WinPctA; count++;
            }
            sb.AppendLine($"| **mean** | {total / count:0.0} | | |");
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
            [Test]
        public void OrbTuningGrid()
        {
            var seeds = int.TryParse(Environment.GetEnvironmentVariable("BB_BAL_SEEDS"), out var n) ? n : 100;
            var r0 = TributeTuning.ChorusDamagePerRing; var b0 = TributeTuning.ChorusUnityBase; var u0 = TributeTuning.ChorusUnityPerRing;
            var g0 = TributeTuning.ChorusLightGapTicks; var d0 = TributeTuning.ChorusDarkTicks; var s0 = TributeTuning.ChorusStartDamage; var p0 = TributeTuning.ChorusPinkHeal;
            var sb = new StringBuilder("| gap | melee/ring | unity | mean win % | min | max | longest median s |\n|---|---|---|---|---|---|---|\n");
            try
            {
                TributeTuning.ChorusDarkTicks = 90; TributeTuning.ChorusStartDamage = Fix.Ratio(3, 2); TributeTuning.ChorusPinkHeal = Fix.Ratio(3, 5);
                foreach (var gap in new[] { 60, 90, 120 })
                foreach (var ring in new[] { 3, 5 })        // quarters
                foreach (var ub in new[] { 2, 4 })          // unity base (per ring = half of it)
                {
                    TributeTuning.ChorusLightGapTicks = gap;
                    TributeTuning.ChorusDamagePerRing = Fix.Ratio(ring, 4); TributeTuning.ChorusUnityBase = ub; TributeTuning.ChorusUnityPerRing = Fix.Ratio(ub, 2);
                    var rows = WeaponRegistry.All.Where(e => e.Id != ChorusRule.WeaponId)
                        .Select(e => (e.Id, s: MatchupReport.Play(ChorusRule.WeaponId, e.Id, seeds))).ToList();
                    var lo = rows.OrderBy(r => r.s.WinPctA).First(); var hi = rows.OrderBy(r => r.s.WinPctA).Last();
                    sb.AppendLine($"| {gap} | {ring / 4f} | {ub}+{ub / 2f}/ring | {rows.Average(r => r.s.WinPctA):0.0} | {lo.Id} {lo.s.WinPctA:0.0} | {hi.Id} {hi.s.WinPctA:0.0} | {rows.Max(r => r.s.MedianSeconds):0.0} |");
                }
            }
            finally
            {
                TributeTuning.ChorusDamagePerRing = r0; TributeTuning.ChorusUnityBase = b0; TributeTuning.ChorusUnityPerRing = u0;
                TributeTuning.ChorusLightGapTicks = g0; TributeTuning.ChorusDarkTicks = d0; TributeTuning.ChorusStartDamage = s0; TributeTuning.ChorusPinkHeal = p0;
            }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
