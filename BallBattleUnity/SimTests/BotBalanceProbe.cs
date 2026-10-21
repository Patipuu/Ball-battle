using System;
using System.Linq;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Tuning probe (explicit): run win rate of LV1 balls. BB_PROBE = cases separated by ';', each
    /// "f0,f1,f2:r0,r1,r2:hp0,hp1,hp2:dmg0,dmg1,dmg2" (bot first-cast and repeat ticks, normal-enemy HP and damage % per stage).
    /// </summary>
    [Explicit, Category("Balance")]
    public class BotBalanceProbe
    {
        static Fix Pct(string s) => Fix.Ratio(int.Parse(s), 100);

        [Test]
        public void Probe()
        {
            var spec = Environment.GetEnvironmentVariable("BB_PROBE") ?? "240,240,240:120,120,120:0,45,90:0,30,55";
            var runs = int.Parse(Environment.GetEnvironmentVariable("BB_PROBE_N") ?? "800");
            RunBot.Chapter = int.Parse(Environment.GetEnvironmentVariable("BB_PROBE_CHAPTER") ?? "0");
            RunBot.Heat = int.Parse(Environment.GetEnvironmentVariable("BB_PROBE_HEAT") ?? "0");
            var hp0 = RunTuning.EnemyHpPct.ToArray();
            var dmg0 = RunTuning.EnemyDamagePct.ToArray();
            var first0 = RunTuning.BotFirstTicks.ToArray();
            var repeat0 = RunTuning.BotRepeatTicks.ToArray();
            try
            {
                foreach (var c in spec.Split(';'))
                {
                    var p = c.Split(':');
                    var f = p[0].Split(',');
                    var rep = p[1].Split(',');
                    var hp = p[2].Split(',');
                    var dmg = p[3].Split(',');
                    for (var s = 0; s < 3; s++)
                    {
                        RunTuning.BotFirstTicks[s] = int.Parse(f[s]); RunTuning.BotRepeatTicks[s] = int.Parse(rep[s]);
                        RunTuning.EnemyHpPct[s] = Pct(hp[s]); RunTuning.EnemyDamagePct[s] = Pct(dmg[s]);
                    }
                    foreach (var policy in new[] { RunBot.Policy.Skilled, RunBot.Policy.Greedy })
                    {
                        var r = new RunBot.Result[runs];
                        Parallel.For(0, runs, i => r[i] = RunBot.Play((uint)(i + 1), policy));
                        var win = r.Count(x => x.Won) * 100.0 / runs;
                        var boss1 = r.Count(x => x.BossesDefeated >= 1) * 100.0 / runs;
                        var reach = r.Count(x => x.FightsWon >= 3) * 100.0 / runs;
                        TestContext.Progress.WriteLine($"PROBE {c} {policy}: run win {win:0.0}%  reach boss1 {reach:0.0}%  boss1 {boss1:0.0}%  avg fights won {r.Average(x => x.FightsWon):0.00}");
                    }
                }
            }
            finally
            {
                RunBot.Chapter = 0; RunBot.Heat = 0;
                for (var s = 0; s < 3; s++)
                {
                    RunTuning.EnemyHpPct[s] = hp0[s]; RunTuning.EnemyDamagePct[s] = dmg0[s];
                    RunTuning.BotFirstTicks[s] = first0[s]; RunTuning.BotRepeatTicks[s] = repeat0[s];
                }
            }
        }
    }
}
