using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Trait balance (Phase 3). Per trait and level: win-rate change of a ball that has it, over every old-weapon
    /// pair, same seeds, both spawn sides. Also samples random 3-trait combos against the plain weapons.
    /// Env: BB_SEEDS (seeds per side, default 40), BB_COMBOS (default 500), BB_REPORT (file to write).
    /// Run: dotnet test --filter "FullyQualifiedName~TraitBalanceReport"
    /// </summary>
    [Explicit, Category("Diagnostic")]
    public class TraitBalanceReport
    {
        /// <summary>Win % (draw = half) of ball A (weapon + traits) against ball B (weapon + traits), both sides, over seeds.</summary>
        public static double WinPct(string weaponA, (string id, int level)[] traitsA, string weaponB, (string id, int level)[] traitsB, int seedsPerSide)
        {
            var points = new double[seedsPerSide * 2];
            Parallel.For(0, points.Length, k =>
            {
                var aFirst = k < seedsPerSide;
                var seed = (uint)(k % seedsPerSide + 1);
                var a = Loadout(weaponA, traitsA);
                var b = Loadout(weaponB, traitsB);
                var m = new MatchSim(new MatchConfig(), seed, aFirst ? new[] { a, b } : new[] { b, a });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                var aIndex = aFirst ? 0 : 1;
                points[k] = m.Outcome == MatchOutcome.Win ? (m.WinnerIndex == aIndex ? 1.0 : 0.0) : 0.5;
            });
            return points.Average() * 100.0;
        }

        static BallLoadout Loadout(string weaponId, (string id, int level)[] traits)
        {
            var l = new BallLoadout(WeaponRegistry.Create(weaponId));
            foreach (var (id, level) in traits) l.Traits.Add(TraitRegistry.Create(id, level));
            return l;
        }

        /// <summary>Trait balance is defined over the four Step 1 weapons (stable reference field).</summary>
        public static readonly string[] BaseWeapons = { "blade", "fang", "pike", "brawler" };

        const int FieldSize = 8;
        static readonly (string, int)[] None = new (string, int)[0];

        /// <summary>Average win-rate change in points for trait T at the level, over all eligible weapon pairs.</summary>
        public static double Delta(string traitId, int level, int seeds)
        {
            var ids = BaseWeapons;
            var sum = 0.0;
            var n = 0;
            foreach (var a in ids)
            {
                if (!TraitRegistry.IsEligible(traitId, a)) continue;
                foreach (var b in ids)
                {
                    var with = WinPct(a, new[] { (traitId, level) }, b, None, seeds);
                    var without = WinPct(a, None, b, None, seeds);
                    sum += with - without;
                    n++;
                }
            }
            return sum / n;
        }

        [Test]
        public void TraitDeltas()
        {
            var seeds = int.TryParse(Environment.GetEnvironmentVariable("BB_SEEDS"), out var s) ? s : 40;
            var sb = new StringBuilder("| trait | L1 (points) | L2 (points) |\n|---|---|---|\n");
            var only = Environment.GetEnvironmentVariable("BB_TRAITS");
            foreach (var e in TraitRegistry.All.Where(x => string.IsNullOrEmpty(only) || only.Split(',').Contains(x.Id)))
                sb.AppendLine($"| {e.Id} | {Delta(e.Id, 1, seeds):+0.0;-0.0} | {Delta(e.Id, 2, seeds):+0.0;-0.0} |");
            Output(sb.ToString() + $"{seeds} seeds/side\n");
        }

        [Test]
        public void RandomThreeTraitCombos()
        {
            var combos = int.TryParse(Environment.GetEnvironmentVariable("BB_COMBOS"), out var c) ? c : 500;
            var ids = BaseWeapons;
            var traitIds = TraitRegistry.All.Select(e => e.Id).ToArray();
            var rng = new Random(12345);
            var rows = new List<(string text, double pct, string weapon, (string, int)[] picks)>();
            for (var i = 0; i < combos; i++)
            {
                var weapon = ids[rng.Next(ids.Length)];
                var picks = traitIds.Where(t => TraitRegistry.IsEligible(t, weapon)).OrderBy(_ => rng.Next()).Take(3).Select(t => (t, rng.Next(1, 3))).ToArray();
                var total = 0.0;
                for (var f = 0; f < FieldSize; f++)
                {
                    var foe = ids[rng.Next(ids.Length)];
                    var foeTraits = traitIds.Where(t => TraitRegistry.IsEligible(t, foe)).OrderBy(_ => rng.Next()).Take(3).Select(t => (t, rng.Next(1, 3))).ToArray();
                    total += WinPct(weapon, picks, foe, foeTraits, 6);
                }
                var pct = total / FieldSize;
                rows.Add(($"{weapon} + {string.Join(", ", picks.Select(p => p.Item1 + p.Item2))}", pct, weapon, picks));
            }
            var sorted = rows.OrderByDescending(r => r.pct).ToList();
            var sb = new StringBuilder($"{combos} combos, best 8 (avg win % vs 8 random 3-trait builds):\n");
            foreach (var r in sorted.Take(8)) sb.AppendLine($"{r.pct:0.0}  {r.text}");
            sb.AppendLine($"over 85%: {sorted.Count(r => r.pct > 85.0)}   mean {rows.Average(r => r.pct):0.0}");
            sb.AppendLine("re-measured (6x the matches vs a fixed 16-build field):");
            var field = new List<(string weapon, (string, int)[] traits)>();
            for (var f = 0; f < 16; f++)
            {
                var foe = ids[rng.Next(ids.Length)];
                field.Add((foe, traitIds.Where(t => TraitRegistry.IsEligible(t, foe)).OrderBy(_ => rng.Next()).Take(3).Select(t => (t, rng.Next(1, 3))).ToArray()));
            }
            foreach (var r in sorted.Take(8))
                sb.AppendLine($"{field.Average(foe => WinPct(r.weapon, r.picks, foe.weapon, foe.traits, 18)):0.0}  {r.text}");
            Output(sb.ToString());
        }

        static void Output(string text)
        {
            TestContext.Out.WriteLine(text);
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, text);
        }
    }
}