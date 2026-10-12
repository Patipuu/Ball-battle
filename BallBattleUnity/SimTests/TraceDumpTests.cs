using System;
using System.IO;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class TraceDumpTests
    {
        [Test, Explicit("Run with dotnet test --filter Name~TraceDump")]
        public void TraceDump()
        {
            const uint matchSeed = 1454667u;
            var seeds = new[] { 4134000519u, 2686071142u };
            Assert.That(Series.SeedFor(matchSeed, 0), Is.EqualTo(seeds[0]));
            Assert.That(Series.SeedFor(matchSeed, 1), Is.EqualTo(seeds[1]));
            var root = Directory.GetCurrentDirectory();
            while (!Directory.Exists(Path.Combine(root, "docs", "claude-runs")))
            {
                var parent = Directory.GetParent(root);
                if (parent == null) throw new DirectoryNotFoundException("docs/claude-runs");
                root = parent.FullName;
            }
            foreach (var seed in seeds)
            {
                var path = Path.Combine(root, "docs", "claude-runs", "trace-dotnet-" + seed + ".txt");
                var fieldsPath = Path.Combine(root, "docs", "claude-runs", "trace-dotnet-" + seed + ".fields.txt");
                var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), seed,
                    new[] { WeaponRegistry.Create("blade"), WeaponRegistry.Create("fang") });
                using (var trace = new StreamWriter(path, false))
                using (var fields = new StreamWriter(fieldsPath, false))
                {
                    trace.WriteLine(SimTrace.Line(sim));
                    fields.WriteLine(SimTrace.FieldsLine(sim));
                    while (sim.Outcome == MatchOutcome.Ongoing)
                    {
                        sim.Step();
                        if (sim.Tick == 27)
                            foreach (var e in sim.Events) TestContext.Progress.WriteLine($"seed {seed} tick 27 event {e.Type} A={e.A} B={e.B}");
                        trace.WriteLine(SimTrace.Line(sim));
                        fields.WriteLine(SimTrace.FieldsLine(sim));
                    }
                }
                TestContext.Progress.WriteLine($"trace {seed}: {sim.Tick} ticks, winner {sim.WinnerIndex}, {path}");
            }
        }
    }
}
