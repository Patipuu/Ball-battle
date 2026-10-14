using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Tools
{
    /// <summary>
    /// Headless weapon round-robin (6a.6b/c part C): every ordered pair of registered weapons (mirrors included, so each
    /// weapon plays both sides), <see cref="Options.Seeds"/> matches per pair, on one arena or all. Pure and deterministic:
    /// the seed of match <c>i</c> of pair (A, B) depends only on (A, B, i), every number in the CSV is computed with integer
    /// arithmetic, and the same options always give byte-identical text (pairs run in parallel but are written in order).
    /// File I/O and the command line live in the callers (the player's -roundrobin mode, SimTests).
    /// </summary>
    public static class RoundRobin
    {
        /// <summary>Tick columns: Tick = every step played; ActiveTicks = MatchSim.ActiveTick (the clock of the 180 s cap and the
        /// shrink start), added by BB-FONT1 after the existing tick columns. Output only: no match result changes.</summary>
        public const string MainHeader = "arena,weaponA,weaponB,matches,winsA,winsB,draws,winRateA,avgTicks,medianTicks,maxTicks,avgActiveTicks,medianActiveTicks,maxActiveTicks";
        public const string WeaponHeader = "weapon,matches,wins,draws,winRate,avgTicks,avgActiveTicks,medianActiveTicks,maxActiveTicks";

        public sealed class Options
        {
            public int Seeds = 200;
            /// <summary>Arena id, or "all".</summary>
            public string Arena = ArenaRegistry.Classic;
            public string Out = "results.csv";
            /// <summary>Stop a match after this many ticks and count it as a draw; 0 = no extra limit (the match's own time cap decides).</summary>
            public int MaxTicks;
        }

        public sealed class PairResult
        {
            public string Arena, WeaponA, WeaponB;
            public int Matches, WinsA, WinsB, Draws, MaxTicks, MaxActiveTicks;
            public long TotalTicks, TotalActiveTicks;
            /// <summary>Median × 2 (an integer even when the count is even).</summary>
            public long MedianTicks2, MedianActiveTicks2;
            /// <summary>ActiveTick of every match, sorted (per-weapon medians are taken over these).</summary>
            public int[] ActiveTicksSorted = new int[0];
        }

        public sealed class Result
        {
            public readonly List<PairResult> Pairs = new List<PairResult>();
            public string MainCsv, WeaponCsv;
        }

        /// <summary>Parses "-seeds N -arena id|all -out path -maxTicks N" (any order; other arguments are ignored). Throws ArgumentException on bad values.</summary>
        public static Options Parse(IReadOnlyList<string> args)
        {
            var o = new Options();
            for (var i = 0; i < args.Count; i++)
            {
                string Next()
                {
                    if (i + 1 >= args.Count) throw new ArgumentException($"{args[i]} needs a value");
                    return args[++i];
                }
                switch (args[i])
                {
                    case "-seeds": o.Seeds = PositiveInt(Next(), "-seeds"); break;
                    case "-arena": o.Arena = Next(); break;
                    case "-out": o.Out = Next(); break;
                    case "-maxTicks": o.MaxTicks = PositiveInt(Next(), "-maxTicks"); break;
                }
            }
            if (o.Arena != "all") ArenaRegistry.Get(o.Arena);   // throws for an unknown id
            if (string.IsNullOrEmpty(o.Out)) throw new ArgumentException("-out needs a path");
            return o;
        }

        static int PositiveInt(string s, string name)
        {
            if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v <= 0)
                throw new ArgumentException($"{name} must be a positive integer, got '{s}'");
            return v;
        }

        /// <summary>The per-weapon file next to the main one: results.csv → results-weapons.csv.</summary>
        public static string WeaponPath(string mainPath)
        {
            var dot = mainPath.LastIndexOf('.');
            var slash = Math2.Max(mainPath.LastIndexOf('/'), mainPath.LastIndexOf('\\'));
            return dot > slash ? mainPath.Substring(0, dot) + "-weapons" + mainPath.Substring(dot) : mainPath + "-weapons";
        }

        /// <summary>Seed of match <paramref name="index"/> for (weaponA, weaponB): FNV-1a of both ids and the index; never 0.</summary>
        public static uint Seed(string weaponA, string weaponB, int index)
        {
            var h = SimHash.Seed;
            h = SimHash.Mix(h, weaponA);
            h = SimHash.Mix(h, weaponB);
            h = SimHash.Mix(h, index);
            var s = unchecked((uint)(h ^ (h >> 32)));
            return s == 0 ? 1u : s;
        }

        /// <summary>One match to the end (or to <paramref name="maxTicks"/>): winner index (-1 = draw) and ticks played.</summary>
        public static (int winner, int ticks) Play(string arena, string weaponA, string weaponB, uint seed, int maxTicks)
        {
            var (winner, ticks, _) = PlayDetailed(arena, weaponA, weaponB, seed, maxTicks);
            return (winner, ticks);
        }

        /// <summary>Same match as <see cref="Play"/>, also returning MatchSim.ActiveTick at the end (read only, after the last step).</summary>
        public static (int winner, int ticks, int activeTicks) PlayDetailed(string arena, string weaponA, string weaponB, uint seed, int maxTicks)
        {
            var m = new MatchSim(ArenaRegistry.Create(arena), seed, new[] { WeaponRegistry.Create(weaponA), WeaponRegistry.Create(weaponB) });
            while (m.Outcome == MatchOutcome.Ongoing && (maxTicks <= 0 || m.Tick < maxTicks)) m.Step();
            return (m.Outcome == MatchOutcome.Ongoing ? -1 : m.WinnerIndex, m.Tick, m.ActiveTick);
        }

        /// <summary>Median × 2 of a sorted array (0 when empty).</summary>
        static long Median2(int[] sorted)
        {
            var n = sorted.Length;
            if (n == 0) return 0;
            return n % 2 == 1 ? 2L * sorted[n / 2] : (long)sorted[n / 2 - 1] + sorted[n / 2];
        }

        public static Result Run(Options o, bool parallel = true)
        {
            var arenas = new List<string>();
            if (o.Arena == "all") foreach (var a in ArenaRegistry.All) arenas.Add(a.Id);
            else arenas.Add(o.Arena);
            var weapons = new List<string>();
            foreach (var w in WeaponRegistry.All) weapons.Add(w.Id);

            var jobs = new List<PairResult>();
            foreach (var arena in arenas)
                foreach (var a in weapons)
                    foreach (var b in weapons)
                        jobs.Add(new PairResult { Arena = arena, WeaponA = a, WeaponB = b });

            void RunPair(int j)
            {
                var p = jobs[j];
                var ticks = new int[o.Seeds];
                var active = new int[o.Seeds];
                for (var i = 0; i < o.Seeds; i++)
                {
                    var (winner, t, at) = PlayDetailed(p.Arena, p.WeaponA, p.WeaponB, Seed(p.WeaponA, p.WeaponB, i), o.MaxTicks);
                    if (winner == 0) p.WinsA++;
                    else if (winner == 1) p.WinsB++;
                    else p.Draws++;
                    ticks[i] = t;
                    p.TotalTicks += t;
                    if (t > p.MaxTicks) p.MaxTicks = t;
                    active[i] = at;
                    p.TotalActiveTicks += at;
                    if (at > p.MaxActiveTicks) p.MaxActiveTicks = at;
                }
                Array.Sort(ticks);
                Array.Sort(active);
                var n = ticks.Length;
                p.MedianTicks2 = Median2(ticks);
                p.MedianActiveTicks2 = Median2(active);
                p.ActiveTicksSorted = active;
                p.Matches = n;
            }

            if (parallel) Parallel.For(0, jobs.Count, RunPair);
            else for (var j = 0; j < jobs.Count; j++) RunPair(j);

            var r = new Result();
            r.Pairs.AddRange(jobs);
            r.MainCsv = MainCsv(jobs);
            r.WeaponCsv = WeaponCsv(jobs, weapons);
            return r;
        }

        static string MainCsv(List<PairResult> pairs)
        {
            var sb = new StringBuilder(MainHeader).Append('\n');
            foreach (var p in pairs)
                sb.Append(p.Arena).Append(',').Append(p.WeaponA).Append(',').Append(p.WeaponB).Append(',')
                  .Append(I(p.Matches)).Append(',').Append(I(p.WinsA)).Append(',').Append(I(p.WinsB)).Append(',').Append(I(p.Draws)).Append(',')
                  .Append(Decimal(p.WinsA, p.Matches, 4)).Append(',').Append(Decimal(p.TotalTicks, p.Matches, 1)).Append(',')
                  .Append(Decimal(p.MedianTicks2, 2, 1)).Append(',').Append(I(p.MaxTicks)).Append(',')
                  .Append(Decimal(p.TotalActiveTicks, p.Matches, 1)).Append(',').Append(Decimal(p.MedianActiveTicks2, 2, 1)).Append(',')
                  .Append(I(p.MaxActiveTicks)).Append('\n');
            return sb.ToString();
        }

        /// <summary>Per weapon, over every arena and both sides (a mirror match counts once for each side).</summary>
        static string WeaponCsv(List<PairResult> pairs, List<string> weapons)
        {
            var sb = new StringBuilder(WeaponHeader).Append('\n');
            foreach (var w in weapons)
            {
                long matches = 0, wins = 0, draws = 0, ticks = 0, activeTicks = 0;
                var maxActive = 0;
                var all = new List<int>();
                void Side(PairResult p, int sideWins)
                {
                    matches += p.Matches; wins += sideWins; draws += p.Draws; ticks += p.TotalTicks;
                    activeTicks += p.TotalActiveTicks;
                    if (p.MaxActiveTicks > maxActive) maxActive = p.MaxActiveTicks;
                    all.AddRange(p.ActiveTicksSorted);
                }
                foreach (var p in pairs)
                {
                    if (p.WeaponA == w) Side(p, p.WinsA);
                    if (p.WeaponB == w) Side(p, p.WinsB);
                }
                var sorted = all.ToArray();
                Array.Sort(sorted);
                sb.Append(w).Append(',').Append(L(matches)).Append(',').Append(L(wins)).Append(',').Append(L(draws)).Append(',')
                  .Append(Decimal(wins, matches, 4)).Append(',').Append(Decimal(ticks, matches, 1)).Append(',')
                  .Append(Decimal(activeTicks, matches, 1)).Append(',').Append(Decimal(Median2(sorted), 2, 1)).Append(',')
                  .Append(I(maxActive)).Append('\n');
            }
            return sb.ToString();
        }

        static string I(int v) => v.ToString(CultureInfo.InvariantCulture);
        static string L(long v) => v.ToString(CultureInfo.InvariantCulture);

        /// <summary>num/den (both ≥ 0, den &gt; 0) with exactly <paramref name="decimals"/> digits, rounded half up, integer-only ("0.5000", "1234.5").</summary>
        public static string Decimal(long num, long den, int decimals)
        {
            long scale = 1;
            for (var i = 0; i < decimals; i++) scale *= 10;
            var q = (num * scale * 2 + den) / (den * 2);
            var ip = q / scale;
            var fp = q % scale;
            var s = ip.ToString(CultureInfo.InvariantCulture);
            return decimals == 0 ? s : s + "." + fp.ToString(CultureInfo.InvariantCulture).PadLeft(decimals, '0');
        }

        static class Math2
        {
            public static int Max(int a, int b) => a > b ? a : b;
        }
    }
}
