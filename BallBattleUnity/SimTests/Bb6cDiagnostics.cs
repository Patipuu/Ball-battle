using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Tools;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// BB-6c diagnostic (no tuning): per weapon and arena, against every other weapon (both sides, round-robin seeds), where the
    /// damage comes from. Env: BB_DIAG_ARENAS (comma list, default all), BB_SEEDS (per side, default 30), BB_REPORT (file).
    /// Run: dotnet test --filter Name=WeaponArenaBreakdown
    /// </summary>
    [Explicit("diagnostic"), Category("Diagnostic"), Category("Slow")]
    public class Bb6cDiagnostics
    {
        sealed class Acc
        {
            public long Matches, Wins, Active, Parries;
            public double Melee, Shots, ReflectOther, Poison, Taken, SpikeTaken, Healed;
        }

        [Test]
        public void WeaponArenaBreakdown()
        {
            var seeds = int.TryParse(Environment.GetEnvironmentVariable("BB_SEEDS"), out var s) ? s : 30;
            var only = Environment.GetEnvironmentVariable("BB_DIAG_ARENAS");
            var arenas = ArenaRegistry.All.Select(a => a.Id).Where(a => string.IsNullOrEmpty(only) || only.Split(',').Contains(a)).ToArray();
            var weapons = WeaponRegistry.All.Select(w => w.Id).ToArray();
            var sb = new StringBuilder($"per match averages, {seeds} seeds per side, mirrors excluded\n");
            foreach (var arena in arenas)
            {
                sb.AppendLine($"\n## {arena}\n| weapon | win% | active | melee | shots | reflect+trait | poison | taken | spike taken | healed | parries |\n|---|---|---|---|---|---|---|---|---|---|---|");
                foreach (var w in weapons)
                {
                    var accs = new Acc[weapons.Length * 2 * seeds];
                    Parallel.For(0, accs.Length, k =>
                    {
                        var o = weapons[k / (2 * seeds)];
                        var rest = k % (2 * seeds);
                        var first = rest < seeds;
                        var i = rest % seeds;
                        if (o == w) return;
                        var a = first ? w : o; var b = first ? o : w;
                        var me = first ? 0 : 1;
                        var m = new MatchSim(ArenaRegistry.Create(arena), RoundRobin.Seed(a, b, i), new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
                        var acc = new Acc { Matches = 1 };
                        while (m.Outcome == MatchOutcome.Ongoing)
                        {
                            m.Step();
                            foreach (var e in m.Events)
                            {
                                var v = e.Value.ToFloat();
                                switch (e.Type)
                                {
                                    case SimEventType.Hit: if (e.A == me) acc.Melee += v; else if (e.B == me) acc.Taken += v; break;
                                    case SimEventType.ProjectileHit: if (e.A == me) acc.Shots += v; else if (e.B == me) acc.Taken += v; break;
                                    case SimEventType.Damage: if (e.A == me) acc.ReflectOther += v; else if (e.B == me) acc.Taken += v; break;
                                    case SimEventType.StatusTick: if (e.B == me) acc.Poison += v; else if (e.A == me) acc.Taken += v; break;
                                    case SimEventType.WallBounce: if (e.A == me) { acc.SpikeTaken += v; acc.Taken += v; } break;
                                    case SimEventType.ObstacleHit: if (e.A == me) acc.Taken += v; break;
                                    case SimEventType.Heal: if (e.A == me) acc.Healed += v; break;
                                    case SimEventType.Parry: if (e.A == me || e.B == me) acc.Parries++; break;
                                }
                            }
                        }
                        acc.Active = m.ActiveTick;
                        acc.Wins = m.Outcome == MatchOutcome.Win && m.WinnerIndex == me ? 1 : 0;
                        accs[k] = acc;
                    });
                    var all = accs.Where(x => x != null).ToList();
                    double n = all.Count;
                    sb.AppendLine($"| {w} | {100.0 * all.Sum(x => x.Wins) / n:0.0} | {all.Sum(x => x.Active) / n:0} | {all.Sum(x => x.Melee) / n:0.0} | {all.Sum(x => x.Shots) / n:0.0} | " +
                                  $"{all.Sum(x => x.ReflectOther) / n:0.0} | {all.Sum(x => x.Poison) / n:0.0} | {all.Sum(x => x.Taken) / n:0.0} | {all.Sum(x => x.SpikeTaken) / n:0.0} | " +
                                  $"{all.Sum(x => x.Healed) / n:0.0} | {all.Sum(x => x.Parries) / n:0.0} |");
                }
            }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }
}
