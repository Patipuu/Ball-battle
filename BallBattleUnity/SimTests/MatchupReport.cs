using System;
using System.Collections.Generic;
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
    /// Win-rate matrix across all weapon pairs. Each pair is played with both side assignments so the spawn
    /// side cannot bias it. Matches run in parallel (each match is independent and deterministic).
    /// Env: BB_SEEDS (seeds per side, default 200), BB_REPORT (file to write the table to).
    /// Run: dotnet test --filter "FullyQualifiedName~MatchupReport"
    /// </summary>
    [Explicit, Category("Diagnostic")]
    public class MatchupReport
    {
        public struct PairStats
        {
            public string A, B;
            public int WinsA, WinsB, Draws, Caps;
            public float MedianSeconds, P90Seconds;
            public int Total => WinsA + WinsB + Draws;
            public float WinPctA => 100f * WinsA / Total;
            public float WinPctB => 100f * WinsB / Total;
            public float DrawPct => 100f * Draws / Total;
            public float CapPct => 100f * Caps / Total;
        }

        struct One
        {
            public int Winner;   // 0 = A, 1 = B, -1 = draw
            public bool Cap;
            public float Seconds;
        }

        public static PairStats Play(string idA, string idB, int seedsPerSide)
        {
            var results = new One[seedsPerSide * 2];
            Parallel.For(0, results.Length, k =>
            {
                var side = k / seedsPerSide;
                var seed = (uint)(k % seedsPerSide + 1);
                var first = side == 0 ? idA : idB;
                var second = side == 0 ? idB : idA;
                var m = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(first), WeaponRegistry.Create(second) });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                var r = new One { Cap = m.EndReason == MatchEndReason.TimeCap, Seconds = m.ActiveTick / 60f, Winner = -1 };
                if (m.Outcome == MatchOutcome.Win)
                {
                    // Map ball index back to "A"/"B" of this pair (for mirrors, A = ball on the side-0 slot).
                    var aIndex = side == 0 ? 0 : 1;
                    r.Winner = m.WinnerIndex == aIndex ? 0 : 1;
                }
                results[k] = r;
            });

            var s = new PairStats { A = idA, B = idB };
            foreach (var r in results)
            {
                if (r.Cap) s.Caps++;
                if (r.Winner == 0) s.WinsA++;
                else if (r.Winner == 1) s.WinsB++;
                else s.Draws++;
            }
            var secs = results.Select(r => r.Seconds).OrderBy(x => x).ToArray();
            s.MedianSeconds = secs[secs.Length / 2];
            s.P90Seconds = secs[(int)(secs.Length * 0.9)];
            return s;
        }

        public static List<PairStats> PlayAll(int seedsPerSide)
        {
            var ids = WeaponRegistry.All.Select(e => e.Id).ToArray();
            var rows = new List<PairStats>();
            for (var i = 0; i < ids.Length; i++)
                for (var j = i; j < ids.Length; j++)
                    rows.Add(Play(ids[i], ids[j], seedsPerSide));
            return rows;
        }

        public static string Table(IEnumerable<PairStats> rows)
        {
            var sb = new StringBuilder();
            sb.AppendLine("| A vs B | A win % | B win % | Draw % | Median s | P90 s | Cap % |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            foreach (var r in rows)
                sb.AppendLine($"| {r.A} vs {r.B} | {r.WinPctA:0.0} | {r.WinPctB:0.0} | {r.DrawPct:0.0} | {r.MedianSeconds:0.0} | {r.P90Seconds:0.0} | {r.CapPct:0.0} |");
            return sb.ToString();
        }

        [Test]
        public void WinRateMatrix()
        {
            var seeds = int.TryParse(Environment.GetEnvironmentVariable("BB_SEEDS"), out var n) ? n : 200;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var table = Table(PlayAll(seeds)) + $"\n{seeds} seeds/side, {sw.Elapsed.TotalSeconds:0.0} s\n";
            TestContext.Out.WriteLine(table);
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, table);
        }
    }
}