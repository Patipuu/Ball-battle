using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using BallBattle.Sim.Skills;

namespace BallBattle.SimTests
{
    /// <summary>BB-12 no-SKILL-UP tree prototype; never modifies production Sim.</summary>
    static class Program
    {
        static readonly string[] Weapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };

        static int Main(string[] args)
        {
            if ((args.Length != 3 && args.Length != 5 && args.Length != 6 && args.Length != 7 && args.Length != 8 && args.Length != 11) || !int.TryParse(args[0], out var firstSeed) ||
                !int.TryParse(args[1], out var seedCount) || firstSeed < 1 || seedCount < 1)
            {
                Console.Error.WriteLine("Usage: TreeLab <first-seed> <seed-count> [<vitality-rank> <power-rank> [<growth-rank>] [<hp-pct-per-rank> <damage-pct-per-rank>] [<start 0|1> <skill L1|A2|B2|A3|B3> <keystone ->]] <output.csv>");
                return 2;
            }
            var vitality = 0;
            var power = 0;
            var growth = 0;
            var hpPctPerRank = 8;
            var damagePctPerRank = 6;
            if (args.Length >= 5 && (!int.TryParse(args[2], out vitality) || !int.TryParse(args[3], out power) ||
                vitality < 0 || vitality > 2 || power < 0 || power > 2))
            {
                Console.Error.WriteLine("VITALITY and POWER ranks must each be 0..2");
                return 2;
            }
            if ((args.Length == 6 || args.Length == 8 || args.Length == 11) && (!int.TryParse(args[4], out growth) || growth < 0 || growth > 2))
            {
                Console.Error.WriteLine("GROWTH rank must be 0..2");
                return 2;
            }
            var coeffIndex = args.Length == 8 || args.Length == 11 ? 5 : 4;
            if (args.Length >= 7 && (!int.TryParse(args[coeffIndex], out hpPctPerRank) || !int.TryParse(args[coeffIndex + 1], out damagePctPerRank) ||
                hpPctPerRank < 0 || hpPctPerRank > 50 || damagePctPerRank < 0 || damagePctPerRank > 50))
            {
                Console.Error.WriteLine("Per-rank percentages must be 0..50");
                return 2;
            }
            var start = false;
            var skill = "L1";
            var keystone = "-";
            if (args.Length == 11)
            {
                if (args[7] != "0" && args[7] != "1" ||
                    !new[] { "L1", "A2", "B2", "A3", "B3" }.Contains(args[8]) ||
                    !new[] { "-", "A", "B" }.Contains(args[9]))
                {
                    Console.Error.WriteLine("Invalid start, skill or keystone");
                    return 2;
                }
                start = args[7] == "1";
                skill = args[8];
                keystone = args[9];
            }
            TreeLabParams.GrowthRank = growth;
            TreeLabParams.StartEnabled = start;
            TreeLabParams.Keystone = keystone[0];

            var results = new ConcurrentDictionary<string, (int wins, int fightsWon)>();
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Parallel.ForEach(Weapons, weapon =>
            {
                var wins = 0;
                var fights = 0;
                RunBot.TreeSetup = run =>
                {
                    run.Build.MaxHp *= Fix.Ratio(100 + hpPctPerRank * vitality, 100);
                    run.Build.Hp = run.Build.MaxHp;
                    run.Build.DamagePct += Fix.Ratio(damagePctPerRank * power, 100);
                    run.Build.SkillLevel = skill == "L1" ? 1 : skill.EndsWith("2") ? 2 : 3;
                    run.Build.SkillBranch = skill == "L1" ? SkillBranch.None : skill.StartsWith("A") ? SkillBranch.A : SkillBranch.B;
                };
                for (var n = 0; n < seedCount; n++)
                {
                    var result = RunBot.Play((uint)(firstSeed + n), RunBot.Policy.Greedy, null, null, weapon);
                    if (result.Won) wins++;
                    fights += result.FightsWon;
                }
                RunBot.TreeSetup = null;
                results[weapon] = (wins, fights);
            });

            var lines = new[] { "weapon,first_seed,seed_count,vitality,power,growth,hp_pct_per_rank,damage_pct_per_rank,start,skill,keystone,wins,win_pct,avg_fights_won" }
                .Concat(Weapons.Select(weapon =>
                {
                    var r = results[weapon];
                    return string.Join(",", weapon, firstSeed, seedCount, vitality, power, growth, hpPctPerRank, damagePctPerRank, start ? 1 : 0, skill, keystone, r.wins,
                        (100.0 * r.wins / seedCount).ToString("0.00", CultureInfo.InvariantCulture),
                        ((double)r.fightsWon / seedCount).ToString("0.000", CultureInfo.InvariantCulture));
                })).ToArray();
            File.WriteAllLines(args[args.Length - 1], lines);
            foreach (var line in lines) Console.WriteLine(line);
            Console.Error.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }

    }
}
