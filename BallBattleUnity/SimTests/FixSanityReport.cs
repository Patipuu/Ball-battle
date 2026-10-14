using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// 6a.6b/c sanity check (diagnostic, not a gate): the same table as the float baseline recorded before the conversion
    /// (docs/claude-runs/6a6bc-float-baseline.csv: Classic, 8x8 ordered pairs, seeds 1..100, ticks = MatchSim.Tick), now from
    /// the Fix sim. BB_SANITY_OUT=path dotnet test --filter Name=WriteFixSanityTable
    /// </summary>
    public class FixSanityReport
    {
        [Test, Category("Diagnostic"), Explicit("long: writes BB_SANITY_OUT")]
        public void WriteFixSanityTable()
        {
            var ids = WeaponRegistry.All.Select(e => e.Id).ToArray();
            var rows = new string[ids.Length * ids.Length];
            System.Threading.Tasks.Parallel.For(0, rows.Length, k =>
            {
                var a = ids[k / ids.Length];
                var b = ids[k % ids.Length];
                int wa = 0, wb = 0, dr = 0; long ticks = 0;
                for (uint seed = 1; seed <= 100; seed++)
                {
                    var m = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
                    while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                    if (m.WinnerIndex == 0) wa++; else if (m.WinnerIndex == 1) wb++; else dr++;
                    ticks += m.Tick;
                }
                rows[k] = string.Join(",", a, b, 100, wa, wb, dr, (wa / 100.0).ToString("0.000", CultureInfo.InvariantCulture), (ticks / 100.0).ToString("0.0", CultureInfo.InvariantCulture));
            });
            var sb = new StringBuilder("weaponA,weaponB,matches,winsA,winsB,draws,winRateA,avgTicks\n");
            foreach (var r in rows) sb.Append(r).Append('\n');
            File.WriteAllText(Environment.GetEnvironmentVariable("BB_SANITY_OUT") ?? "6a6bc-fix-sanity.csv", sb.ToString());
        }
    }
}
