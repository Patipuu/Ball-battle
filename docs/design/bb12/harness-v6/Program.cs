using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;

namespace BallBattle.SimTests
{
    /// <summary>BB-12 v2 isolated full-tree prototype; production Sim is unchanged.</summary>
    static class Program
    {
        static readonly string[] Weapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };
        static readonly int[] Hp = { 4, 6, 5, 1, 4, 5, 5, 3 };
        static readonly int[] Damage = { 4, 8, 5, 1, 4, 6, 6, 3 };

        static int Main(string[] args)
        {
            if ((args.Length != 10 && args.Length != 11) || !int.TryParse(args[0], out var firstSeed) ||
                !int.TryParse(args[1], out var seedCount) || !int.TryParse(args[2], out var vitality) ||
                !int.TryParse(args[3], out var power) || !int.TryParse(args[4], out var growth) ||
                (args[5] != "0" && args[5] != "1") ||
                !new[] { "L1", "A2", "B2", "A3", "B3" }.Contains(args[6]) ||
                !new[] { "-", "A", "B" }.Contains(args[7]) ||
                !new[] { "greedy", "random" }.Contains(args[8]) ||
                firstSeed < 1 || seedCount < 1 || vitality < 0 || vitality > 2 ||
                power < 0 || power > 2 || growth < 0 || growth > 2)
            {
                Console.Error.WriteLine("Usage: TreeLabV2 <seed> <count> <v 0..2> <p 0..2> <g 0..2> <start 0|1> <skill L1|A2|B2|A3|B3> <final -|A|B> <greedy|random> <output.csv> [fight-details.csv]");
                return 2;
            }
            var start = args[5] == "1";
            var skill = args[6];
            var final = args[7];
            var policy = args[8] == "greedy" ? RunBot.Policy.Greedy : RunBot.Policy.Random;
            var tier1 = vitality + power + growth;
            var hasSkill2 = skill != "L1";
            var hasSkill3 = skill.EndsWith("3");
            var lower = tier1 + (start ? 2 : 0) + (hasSkill2 ? 1 : 0);
            var spent = lower + (hasSkill3 ? 1 : 0) + (final == "-" ? 0 : 3);
            if (spent > 10 || ((start || hasSkill2 || final != "-") && (growth < 1 || tier1 < 2)) ||
                (hasSkill3 && lower < 5) || (final != "-" && (!hasSkill3 || skill[0] != final[0])))
            {
                Console.Error.WriteLine("Invalid tree topology or >10 points");
                return 2;
            }
            TreeLabParams.GrowthRank = growth;
            TreeLabParams.StartEnabled = start;
            TreeLabParams.Keystone = final[0];
            var results = new ConcurrentDictionary<string, (int wins, int fights, int bosses)>();
            var fightDetails = new List<string>[Weapons.Length];
            var weaponFilter = Environment.GetEnvironmentVariable("BB12_WEAPON_FILTER");
            var selected = Enumerable.Range(0, Weapons.Length)
                .Where(i => string.IsNullOrEmpty(weaponFilter) || Weapons[i] == weaponFilter).ToArray();
            if (selected.Length == 0) { Console.Error.WriteLine("Unknown BB12_WEAPON_FILTER"); return 2; }
            var watch = System.Diagnostics.Stopwatch.StartNew();
            Parallel.ForEach(selected, w =>
            {
                var wins = 0;
                var fights = 0;
                var bosses = 0;
                var detail = args.Length == 11 ? new List<string>() : null;
                RunBot.TreeSetup = run =>
                {
                    run.Build.MaxHp *= Fix.Ratio(100 + Hp[w] * vitality, 100);
                    run.Build.Hp = run.Build.MaxHp;
                    run.Build.DamagePct += Fix.Ratio(Damage[w] * power, 100);
                    run.Build.SkillLevel = skill == "L1" ? 1 : hasSkill3 ? 3 : 2;
                    run.Build.SkillBranch = skill == "L1" ? SkillBranch.None : skill[0] == 'A' ? SkillBranch.A : SkillBranch.B;
                };
                for (var n = 0; n < seedCount; n++)
                {
                    var seed = firstSeed + n;
                    var result = RunBot.Play((uint)seed, policy,
                        detail == null ? null : (Action<BallBattle.Sim.Run.RunState, MatchSim>)((run, match) =>
                            detail.Add(string.Join(",", Weapons[w], seed, run.FightIndex + 1, run.IsRetry ? 1 : 0,
                                run.CurrentBossId ?? "", run.ArenaId, run.Enemy.WeaponId,
                                match.WinnerIndex == 0 ? 1 : 0, match.EndReason))),
                        null, Weapons[w]);
                    if (result.Won) wins++;
                    fights += result.FightsWon;
                    bosses += result.BossesDefeated;
                }
                RunBot.TreeSetup = null;
                results[Weapons[w]] = (wins, fights, bosses);
                fightDetails[w] = detail;
            });
            var lines = new[] { "weapon,first_seed,seed_count,policy,vitality,power,growth,start,skill,final,spent,wins,win_pct,avg_fights_won,avg_bosses_defeated" }
                .Concat(selected.Select(w => Weapons[w]).Select(weapon =>
                {
                    var r = results[weapon];
                    return string.Join(",", weapon, firstSeed, seedCount, args[8], vitality, power, growth, start ? 1 : 0, skill, final, spent, r.wins,
                        (100.0 * r.wins / seedCount).ToString("0.00", CultureInfo.InvariantCulture),
                        ((double)r.fights / seedCount).ToString("0.000", CultureInfo.InvariantCulture),
                        ((double)r.bosses / seedCount).ToString("0.000", CultureInfo.InvariantCulture));
                })).ToArray();
            File.WriteAllLines(args[9], lines);
            if (args.Length == 11)
                File.WriteAllLines(args[10], new[] { "weapon,seed,fight,retry,boss,arena,enemy_weapon,won,end_reason" }
                    .Concat(selected.SelectMany(w => fightDetails[w])));
            foreach (var line in lines) Console.WriteLine(line);
            Console.Error.WriteLine($"Elapsed: {watch.Elapsed.TotalSeconds:0.0}s");
            return 0;
        }
    }
}
