using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim.Tap;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-19: Run bot win rates for a few manual skill cooldowns (share of the old automatic cooldown). Set BB_REPORT to save.</summary>
    [Explicit("Slow balance grid"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class ActCooldownGrid
    {
        [Test]
        public void Grid()
        {
            var runs = int.TryParse(Environment.GetEnvironmentVariable("BB_BAL_SEEDS"), out var n) ? n : 400;
            var list = (Environment.GetEnvironmentVariable("BB_PCTS") ?? "100,130,160").Split(',').Select(int.Parse).ToArray();
            var p0 = TapTuning.ManualCooldownPercent;
            var sb = new StringBuilder("| cooldown % | greedy win % | random win % |\n|---|---|---|\n");
            try
            {
                foreach (var pct in list)
                {
                    TapTuning.ManualCooldownPercent = pct;
                    double Win(RunBot.Policy p)
                    {
                        var r = new RunBot.Result[runs];
                        Parallel.For(0, runs, i => r[i] = RunBot.Play((uint)(i + 1), p));
                        return r.Count(x => x.Won) * 100.0 / runs;
                    }
                    sb.AppendLine($"| {pct} | {Win(RunBot.Policy.Greedy):0.0} | {Win(RunBot.Policy.Random):0.0} |");
                }
            }
            finally { TapTuning.ManualCooldownPercent = p0; }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
