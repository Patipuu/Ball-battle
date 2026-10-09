using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Per-arena balance: each weapon's average win rate against the other seven (both sides, fixed seeds).
    /// Env: BB_SEEDS (seeds per side, default 100), BB_REPORT (file). Run: dotnet test --filter "FullyQualifiedName~ArenaBalanceReport"
    /// </summary>
    [Category("Balance"), NonParallelizable]
    public class ArenaBalanceReport
    {
        static string[] weapons;
        static string[] arenas;
        static float[][] avg;     // [arena][weapon] average win %
        static int seedsPerSide;

        [OneTimeSetUp]
        public void Play()
        {
            seedsPerSide = int.TryParse(Environment.GetEnvironmentVariable("BB_SEEDS"), out var n) ? n : 100;
            weapons = WeaponRegistry.All.Select(e => e.Id).ToArray();
            arenas = ArenaRegistry.All.Select(e => e.Id).ToArray();
            var wins = new int[arenas.Length][];
            var games = new int[arenas.Length][];
            avg = new float[arenas.Length][];
            for (var a = 0; a < arenas.Length; a++)
            {
                var cnt = new int[weapons.Length];
                var tot = new int[weapons.Length];
                for (var i = 0; i < weapons.Length; i++)
                    for (var j = i + 1; j < weapons.Length; j++)
                    {
                        var res = new int[seedsPerSide * 2];   // 0 = i wins, 1 = j wins, -1 draw
                        var arena = arenas[a];
                        var wi = weapons[i]; var wj = weapons[j];
                        Parallel.For(0, res.Length, k =>
                        {
                            var side = k / seedsPerSide;
                            var seed = (uint)(k % seedsPerSide + 1);
                            var f = side == 0 ? wi : wj; var s = side == 0 ? wj : wi;
                            var m = new MatchSim(ArenaRegistry.Create(arena), seed, new[] { WeaponRegistry.Create(f), WeaponRegistry.Create(s) });
                            while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                            res[k] = m.Outcome != MatchOutcome.Win ? -1 : ((m.WinnerIndex == 0) == (side == 0) ? 0 : 1);
                        });
                        foreach (var r in res)
                        {
                            tot[i]++; tot[j]++;
                            if (r == 0) cnt[i]++; else if (r == 1) cnt[j]++;
                        }
                    }
                avg[a] = new float[weapons.Length];
                for (var w = 0; w < weapons.Length; w++) avg[a][w] = 100f * cnt[w] / tot[w];
            }
        }

        static int[] Ranks(float[] v) => v.Select(x => v.Count(y => y > x)).ToArray();

        static string Table()
        {
            var sb = new StringBuilder("| weapon | " + string.Join(" | ", arenas) + " |\n|---|" + string.Concat(arenas.Select(_ => "---|")) + "\n");
            for (var w = 0; w < weapons.Length; w++)
                sb.AppendLine($"| {weapons[w]} | " + string.Join(" | ", arenas.Select((_, a) => $"{avg[a][w]:0.0} (#{Ranks(avg[a])[w] + 1})")) + " |");
            return sb.ToString() + $"\n{seedsPerSide} seeds/side\n";
        }

        [Test]
        public void ReportAndWeaponAveragesStay25To75()
        {
            var t = Table();
            TestContext.Out.WriteLine(t);
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, t);
            var bad = Enumerable.Range(0, arenas.Length).SelectMany(a => Enumerable.Range(0, weapons.Length)
                .Where(w => avg[a][w] < 25f || avg[a][w] > 75f).Select(w => $"{arenas[a]}/{weapons[w]} {avg[a][w]:0.0}")).ToList();
            Assert.That(bad, Is.Empty, t);
        }

        [Test]
        public void EachVariantChangesAtLeastTwoRanksVsClassic()
        {
            var classic = Ranks(avg[0]);
            var flat = Enumerable.Range(1, arenas.Length - 1)
                .Where(a => Ranks(avg[a]).Where((r, w) => r != classic[w]).Count() < 2).Select(a => arenas[a]).ToList();
            Assert.That(flat, Is.Empty, Table());
        }
    }
}