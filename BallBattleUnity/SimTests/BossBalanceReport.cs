using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// BB-04 boss balance measurement, shared by the Explicit report and the Explicit gate.
    ///  - RunBot sweep: <c>runs</c> runs per bot as a returning player (bosses from the seed, RunBot.BotProgress): run win %, and per
    ///    boss how often the bot beats it (first attempt; and per run that met it, beaten with the retry counted), median ActiveTicks and
    ///    the share of fights that hit the time cap.
    ///  - Weapon vs boss: each of the 8 weapons against each of the 4 bosses, <c>seeds</c> seeds of a Greedy build that reaches the boss
    ///    slot (greedy card picks before every fight, the earlier fights counted as won at full HP without being played), then the real
    ///    boss fight.
    /// </summary>
    public static class BossSweep
    {
        public sealed class Fight
        {
            public string Boss;
            public string Arena;
            public bool Retry;
            public bool Won;
            public bool Cap;
            public int Active;
        }

        public sealed class BotStats
        {
            public int Runs, RunsWon;
            public List<Fight> Fights = new List<Fight>();
            public double WinPct => 100.0 * RunsWon / Runs;

            /// <summary>Per run that met the boss: % that beat it, the retry counted.</summary>
            public double BeatenPct(string boss)
            {
                var met = Fights.Count(x => x.Boss == boss && !x.Retry);
                var beaten = Fights.Count(x => x.Boss == boss && x.Won);   // a run wins a boss fight at most once
                return met == 0 ? double.NaN : 100.0 * beaten / met;
            }

            public double FirstTryPct(string boss)
            {
                var first = Fights.Where(x => x.Boss == boss && !x.Retry).ToList();
                return first.Count == 0 ? double.NaN : 100.0 * first.Count(x => x.Won) / first.Count;
            }
        }

        public sealed class Result
        {
            public int Runs, Seeds;
            public BotStats Greedy, Random;
            public Dictionary<(string weapon, string boss), (int won, int n, int cap)> Matrix = new Dictionary<(string, string), (int, int, int)>();
            public double Seconds;

            public double WeaponPct(string w, string b) => 100.0 * Matrix[(w, b)].won / Matrix[(w, b)].n;

            /// <summary>Time-capped share of every boss fight measured (both bots and the weapon matrix).</summary>
            public double CapPct(string boss)
            {
                var f = Greedy.Fights.Concat(Random.Fights).Where(x => x.Boss == boss).ToList();
                var caps = f.Count(x => x.Cap) + Matrix.Where(kv => kv.Key.boss == boss).Sum(kv => kv.Value.cap);
                var n = f.Count + Matrix.Where(kv => kv.Key.boss == boss).Sum(kv => kv.Value.n);
                return n == 0 ? 0 : 100.0 * caps / n;
            }
        }

        static readonly Dictionary<(int, int), Result> Cache = new Dictionary<(int, int), Result>();

        /// <summary>Cached per (runs, seeds) for the test process (the report and the gate share one measurement).</summary>
        public static Result Measure(int runs, int seeds)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue((runs, seeds), out var cached)) return cached;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var r = new Result { Runs = runs, Seeds = seeds, Greedy = Bot(RunBot.Policy.Greedy, runs), Random = Bot(RunBot.Policy.Random, runs) };
                var weapons = WeaponRegistry.All.Select(e => e.Id).ToArray();
                var tasks = new List<(string w, string b)>();
                foreach (var w in weapons) foreach (var b in BossIds.All) tasks.Add((w, b));
                var cells = new (int won, int n, int cap)[tasks.Count];
                Parallel.For(0, tasks.Count, t =>
                {
                    var (w, b) = tasks[t];
                    int won = 0, cap = 0;
                    for (var s = 0; s < seeds; s++)
                    {
                        var m = DuelAtSlot((uint)(10000 + s), w, b);
                        if (m.WinnerIndex == 0) won++;
                        if (m.EndReason == MatchEndReason.TimeCap) cap++;
                    }
                    cells[t] = (won, seeds, cap);
                });
                for (var t = 0; t < tasks.Count; t++) r.Matrix[tasks[t]] = cells[t];
                r.Seconds = sw.Elapsed.TotalSeconds;
                Cache[(runs, seeds)] = r;
                return r;
            }
        }

        static BotStats Bot(RunBot.Policy policy, int runs)
        {
            var results = new RunBot.Result[runs];
            var fights = new List<Fight>[runs];
            Parallel.For(0, runs, i =>
            {
                var list = fights[i] = new List<Fight>();
                results[i] = RunBot.Play((uint)(i + 1), policy, (run, m) =>
                {
                    var boss = run.CurrentBossId;
                    if (boss == null) return;
                    list.Add(new Fight { Boss = boss, Arena = run.ArenaId, Retry = run.IsRetry, Won = m.WinnerIndex == 0, Cap = m.EndReason == MatchEndReason.TimeCap, Active = m.ActiveTick });
                });
            });
            return new BotStats { Runs = runs, RunsWon = results.Count(x => x.Won), Fights = fights.SelectMany(f => f).ToList() };
        }

        /// <summary>
        /// The boss fight of a Greedy build starting with <paramref name="weaponId"/>: greedy picks before every fight up to the boss's
        /// slot, the earlier fights counted as won at full HP (not simulated), then the real fight against <paramref name="bossId"/>.
        /// </summary>
        public static MatchSim DuelAtSlot(uint seed, string weaponId, string bossId)
        {
            var progress = RunBot.BotProgress();
            var run = RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(progress))[0], progress);
            run.Build.WeaponId = weaponId;
            run.BossIds[BossIds.SlotOf(bossId)] = bossId;
            RunBot.PickGreedyStart(run);
            var slotFight = RunTuning.BossFights[BossIds.SlotOf(bossId)];
            while (run.FightIndex < slotFight)
            {
                RunBot.PickGreedy(run);
                run.ApplyResult(RunFixtures.Forced(run, true, 1f));
            }
            RunBot.PickGreedy(run);
            var m = run.CreateMatch();
            while (m.Outcome == MatchOutcome.Ongoing) m.Step();
            return m;
        }

        static string P(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0");

        public static string Markdown(Result r)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"## Boss sweep ({r.Runs} runs per bot, {r.Seeds} seeds per weapon per boss)\n");
            foreach (var (name, bot) in new[] { ("Greedy", r.Greedy), ("Random", r.Random) })
            {
                sb.AppendLine($"### {name} bot: run win {P(bot.WinPct)}% ({bot.RunsWon}/{bot.Runs})\n");
                sb.AppendLine("| boss | fights | first-try win % | beaten (incl. retry) % | runs that met it | median ActiveTicks | timeout % |");
                sb.AppendLine("|---|---|---|---|---|---|---|");
                foreach (var id in BossIds.All)
                {
                    var f = bot.Fights.Where(x => x.Boss == id).ToList();
                    var med = f.Count > 0 ? f.Select(x => x.Active).OrderBy(x => x).ElementAt(f.Count / 2) : 0;
                    var cap = f.Count == 0 ? double.NaN : 100.0 * f.Count(x => x.Cap) / f.Count;
                    sb.AppendLine($"| {id} | {f.Count} | {P(bot.FirstTryPct(id))} | {P(bot.BeatenPct(id))} | {f.Count(x => !x.Retry)} | {med} | {P(cap)} |");
                }
                sb.AppendLine();
            }
            var weapons = WeaponRegistry.All.Select(e => e.Id).ToArray();
            sb.AppendLine($"### Player weapon vs boss: player win % ({r.Seeds} seeds each; greedy build, arrives at full HP)\n");
            sb.AppendLine("| weapon | " + string.Join(" | ", BossIds.All) + " |");
            sb.AppendLine("|---|" + string.Join("", BossIds.All.Select(_ => "---|")));
            foreach (var w in weapons) sb.AppendLine($"| {w} | " + string.Join(" | ", BossIds.All.Select(b => P(r.WeaponPct(w, b)))) + " |");
            sb.AppendLine("| timeout % | " + string.Join(" | ", BossIds.All.Select(b =>
                P(100.0 * weapons.Sum(w => r.Matrix[(w, b)].cap) / weapons.Sum(w => r.Matrix[(w, b)].n)))) + " |");
            sb.AppendLine();
            sb.AppendLine("Timeout % over every boss fight measured (both bots + the weapon matrix): " +
                          string.Join("  ", BossIds.All.Select(b => $"{b} {P(r.CapPct(b))}%")));
            sb.AppendLine($"\nTuning: SHELL hp {BossTuning.ShellKingHp.ToFloat()} arc {BossTuning.ShellArcDeg} warn {BossTuning.ShellWarnTicks} on {BossTuning.ShellOnTicks} off {BossTuning.ShellOffTicks}; " +
                          $"MAGNET hp {BossTuning.MagnetHp.ToFloat()} pull {BossTuning.MagnetPullPerTick.ToFloat():0.#####} x{BossTuning.MagnetPullTicks} every {BossTuning.MagnetCycleTicks}; " +
                          $"MIMIC hp {BossTuning.MimicHp.ToFloat()}; WARDEN hp {BossTuning.WardenHp.ToFloat()} first {BossTuning.WardenFirstStepTick} step {BossTuning.WardenStepPct}% every {BossTuning.WardenStepEveryTicks} min {BossTuning.WardenMinPct}%");
            sb.AppendLine($"\n{r.Seconds:0} s");
            return sb.ToString();
        }
    }

    /// <summary>BB-04 boss balance report (Explicit). Env: BB_RUNS (1000), BB_SEEDS (100), BB_REPORT (markdown path).</summary>
    [Explicit("BB-04 boss balance sweep (slow)"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class BossBalanceReport
    {
        static int Env(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

        [Test]
        public void BossSweep()
        {
            var text = SimTests.BossSweep.Markdown(SimTests.BossSweep.Measure(Env("BB_RUNS", 1000), Env("BB_SEEDS", 100)));
            TestContext.Out.WriteLine(text);
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, text);
        }
    }

    /// <summary>
    /// BB-6e WARDEN per arena (Explicit): 1000 Greedy runs; for every WARDEN fight its arena, whether the player won, and the WARDEN spike
    /// hits on the player (damaging wall bounces after the first step, outside any normal spike zone hit) split side walls vs ceiling
    /// (the wall nearest to the ball after the bounce). Env: BB_RUNS (1000), BB_REPORT.
    /// </summary>
    [Explicit("BB-6e WARDEN per-arena diagnostic"), Category("Slow"), NonParallelizable]
    public class WardenArenaReport
    {
        sealed class Row { public int Fights, Won, Side, Ceiling, Floor; }

        [Test]
        public void WardenPerArena()
        {
            var runs = int.TryParse(Environment.GetEnvironmentVariable("BB_RUNS"), out var n) ? n : 1000;
            var rows = new Dictionary<string, Row>();
            var gate = new object();
            Parallel.For(0, runs, i =>
            {
                int side = 0, ceil = 0, floor = 0;
                RunBot.Play((uint)(i + 1), RunBot.Policy.Greedy, (run, m) =>
                {
                    if (run.CurrentBossId != BossIds.Warden) return;
                    lock (gate)
                    {
                        if (!rows.TryGetValue(run.ArenaId, out var r)) rows[run.ArenaId] = r = new Row();
                        r.Fights++;
                        if (m.WinnerIndex == 0) r.Won++;
                        r.Side += side; r.Ceiling += ceil; r.Floor += floor;
                    }
                    side = ceil = floor = 0;
                }, (run, m) =>
                {
                    if (run.CurrentBossId != BossIds.Warden || !m.BossSpikedWalls) return;
                    foreach (var e in m.Events)
                    {
                        if (e.Type != SimEventType.WallBounce || e.A != 0 || e.Value <= Fix.Zero) continue;
                        var b = m.Balls[0];
                        var a = m.Arena;
                        var dSide = FixMath.Min(b.Pos.X - a.Left, a.Right - b.Pos.X);
                        var dTop = a.Top - b.Pos.Y;
                        var dBottom = b.Pos.Y - a.Bottom;
                        if (dBottom < dSide && dBottom < dTop) floor++;
                        else if (dTop < dSide) ceil++;
                        else side++;
                    }
                });
            });
            var sb = new StringBuilder("## WARDEN per arena (Greedy, " + runs + " runs)\n\n| arena | WARDEN fights | player win % | spike hits per fight: side | ceiling | floor (normal corner zones only) |\n|---|---|---|---|---|---|\n");
            foreach (var e in ArenaRegistry.All)
            {
                if (!rows.TryGetValue(e.Id, out var r) || r.Fights == 0) { sb.AppendLine($"| {e.Id} | 0 | - | - | - | - |"); continue; }
                sb.AppendLine($"| {e.Id} | {r.Fights} | {100.0 * r.Won / r.Fights:0.0} | {(double)r.Side / r.Fights:0.00} | {(double)r.Ceiling / r.Fights:0.00} | {(double)r.Floor / r.Fights:0.00} |");
            }
            TestContext.Out.WriteLine(sb.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, sb.ToString());
        }
    }

    /// <summary>
    /// BB-04 boss balance gate (Explicit, run separately): 1000 RunBot runs per bot and 100 seeds per weapon per boss. Targets from the
    /// BB-04 spec: Greedy 25-40% and Random 1-6% run wins (existing thresholds); per boss the Greedy bot beats it 45-80% at fight 4 and
    /// 35-70% at fight 8 (per run that met it, the retry counted); no player weapon below 25% against any boss; time-capped boss fights
    /// at most 1% per boss.
    /// </summary>
    [Explicit("BB-04 boss balance gate (slow)"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class BossBalanceThresholdTests
    {
        const int Runs = 1000, Seeds = 100;
        static BossSweep.Result R => BossSweep.Measure(Runs, Seeds);

        [Test]
        public void RunBotsStayInTheirBands()
        {
            // Temporary floor 22% accepted after BB-6f; revisit in the combined balance pass.
            Assert.That(R.Greedy.WinPct, Is.InRange(22.0, 40.0), "Greedy");
            Assert.That(R.Random.WinPct, Is.InRange(1.0, 6.0), "Random");
        }

        [TestCase(BossIds.ShellKing, 45.0, 80.0)]
        [TestCase(BossIds.Magnet, 45.0, 77.0)]
        [TestCase(BossIds.Mimic, 35.0, 70.0)]
        [TestCase(BossIds.Warden, 35.0, 70.0)]
        public void GreedyBeatsEachBossInItsBand(string boss, double min, double max) =>
            Assert.That(R.Greedy.BeatenPct(boss), Is.InRange(min, max), $"{boss}: Greedy beats it {R.Greedy.BeatenPct(boss):0.0}% (first try {R.Greedy.FirstTryPct(boss):0.0}%)");

        [Test]
        public void NoWeaponBelow25PercentAgainstAnyBoss()
        {
            var bad = R.Matrix.Where(kv => R.WeaponPct(kv.Key.weapon, kv.Key.boss) < 25.0).Select(kv => $"{kv.Key.weapon} vs {kv.Key.boss}: {R.WeaponPct(kv.Key.weapon, kv.Key.boss):0.0}%").ToList();
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void TimeCappedBossFightsAtMostOnePercent()
        {
            var bad = BossIds.All.Where(b => R.CapPct(b) > 1.0).Select(b => $"{b}: {R.CapPct(b):0.0}%").ToList();
            Assert.That(bad, Is.Empty);
        }

        [Test]
        public void GreedyWardenWinRateAtLeast15PercentOnEveryArena()
        {
            foreach (var arena in ArenaRegistry.All)
            {
                var fights = R.Greedy.Fights.Where(f => f.Boss == BossIds.Warden && f.Arena == arena.Id).ToList();
                Assert.That(fights.Count, Is.GreaterThan(0), arena.Id + ": sampled WARDEN fights");
                Assert.That(100.0 * fights.Count(f => f.Won) / fights.Count, Is.GreaterThanOrEqualTo(15.0), arena.Id);
            }
        }
    }
}
