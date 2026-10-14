using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// BB-BAL1 balance report (Explicit). Env: BB_RUNS (runs per bot, 500), BB_FORCED (Greedy runs per forced start weapon, 125),
    /// BB_TSEEDS (trait seeds, 200), BB_REPORT (markdown path).
    /// </summary>
    [Explicit("BB-BAL1 balance report (slow)"), Category("Slow"), NonParallelizable]
    public class BbBal1Report
    {
        static int Env(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;
        static string P(double v) => double.IsNaN(v) ? "-" : v.ToString("0.0");

        sealed class Fight { public string Boss; public bool Retry, Won; }

        sealed class Bot
        {
            public int Runs, Won;
            public List<Fight> Fights = new List<Fight>();
            public int A, B;
            public double WinPct => 100.0 * Won / Runs;
            public int Met(string b) => Fights.Count(f => f.Boss == b && !f.Retry);
            public double Beaten(string b) { var m = Met(b); return m == 0 ? double.NaN : 100.0 * Fights.Count(f => f.Boss == b && f.Won) / m; }
            public double First(string b) { var m = Met(b); return m == 0 ? double.NaN : 100.0 * Fights.Count(f => f.Boss == b && !f.Retry && f.Won) / m; }
        }

        static Bot Play(RunBot.Policy policy, int runs, string weapon)
        {
            var results = new RunBot.Result[runs];
            var fights = new List<Fight>[runs];
            var branches = new int[runs, 2];
            Parallel.For(0, runs, i =>
            {
                var list = fights[i] = new List<Fight>();
                RunBot.PickObserved = (run, card, branch) =>
                {
                    if (card.Kind == CardKind.SkillUp && run.Build.SkillLevel == 1) branches[i, branch == SkillBranch.B ? 1 : 0]++;
                };
                try
                {
                    results[i] = RunBot.Play((uint)(i + 1), policy, (run, m) =>
                    {
                        if (run.CurrentBossId != null) list.Add(new Fight { Boss = run.CurrentBossId, Retry = run.IsRetry, Won = m.WinnerIndex == 0 });
                    }, null, weapon);
                }
                finally { RunBot.PickObserved = null; }
            });
            var bot = new Bot { Runs = runs, Won = results.Count(r => r.Won), Fights = fights.SelectMany(f => f).ToList() };
            for (var i = 0; i < runs; i++) { bot.A += branches[i, 0]; bot.B += branches[i, 1]; }
            return bot;
        }

        /// <summary>Ticks from each boss's warn event to the event it warns of (first cycle, one plain fight per boss).</summary>
        static string WarnLeads()
        {
            var sb = new StringBuilder();
            foreach (var (boss, warn, hit) in new[] { (BossIds.ShellKing, BossEvent.ShellWarn, BossEvent.ShellOn), (BossIds.Magnet, BossEvent.MagnetWarn, BossEvent.MagnetPull), (BossIds.Warden, BossEvent.WardenWarn, BossEvent.WardenStep) })
            {
                var player = new BallLoadout(WeaponRegistry.Create("blade")) { MaxHp = 9999 };
                var b = new BallLoadout(WeaponRegistry.Create("blade")) { MaxHp = 9999, Radius = RunTuning.BossRadius, Boss = BossRegistry.Create(boss) };
                var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[] { player, b });
                int w = -1, h = -1;
                while (m.Outcome == MatchOutcome.Ongoing && h < 0 && m.ActiveTick < 3000)
                {
                    m.Step();
                    foreach (var e in m.Events.Where(e => e.Type == SimEventType.Boss))
                    {
                        if (e.Variant == (byte)warn && w < 0) w = m.ActiveTick;
                        else if (e.Variant == (byte)hit && w >= 0 && h < 0) h = m.ActiveTick;
                    }
                }
                sb.AppendLine($"| {boss} | {warn} @ {w} | {hit} @ {h} | **{h - w}** |");
            }
            return sb.ToString();
        }

        [Test]
        public void RunReport()
        {
            var runs = Env("BB_RUNS", 500);
            var forced = Env("BB_FORCED", 125);
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var greedy = Play(RunBot.Policy.Greedy, runs, null);
            var random = Play(RunBot.Policy.Random, runs, null);
            var weapons = WeaponRegistry.All.Select(e => e.Id).ToArray();
            var perWeapon = weapons.ToDictionary(w => w, w => Play(RunBot.Policy.Greedy, forced, w));

            var sb = new StringBuilder();
            sb.AppendLine($"## Run ({runs} runs per bot; forced-weapon Greedy {forced} runs each)\n");
            sb.AppendLine($"RUN greedy={P(greedy.WinPct)} random={P(random.WinPct)}\n");
            sb.AppendLine("| bot | boss | met | first try % | beaten (retry) % |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var (name, bot) in new[] { ("Greedy", greedy), ("Random", random) })
                foreach (var b in BossIds.All)
                    sb.AppendLine($"| {name} | {b} | {bot.Met(b)} | {P(bot.First(b))} | {P(bot.Beaten(b))} |");
            sb.AppendLine($"\nGreedy snapshot branch split at level-1 SKILL UP: A {greedy.A} / B {greedy.B}\n");

            sb.AppendLine("### Forced start weapon, Greedy (beaten incl. retry, per run that met the boss; n = runs that met it)\n");
            sb.AppendLine("| weapon | run win % | " + string.Join(" | ", BossIds.All) + " | A/B branch |");
            sb.AppendLine("|---|---|" + string.Join("", BossIds.All.Select(_ => "---|")) + "---|");
            var min = double.MaxValue; var minAt = "";
            foreach (var w in weapons)
            {
                var bot = perWeapon[w];
                var cells = BossIds.All.Select(b =>
                {
                    var v = bot.Beaten(b);
                    if (!double.IsNaN(v) && v < min) { min = v; minAt = $"{w} vs {b}"; }
                    return $"{P(v)} (n={bot.Met(b)})";
                });
                sb.AppendLine($"| {w} | {P(bot.WinPct)} | " + string.Join(" | ", cells) + $" | {bot.A}/{bot.B} |");
            }
            sb.AppendLine($"\nWEAPONVSBOSS min={P(min)} at {minAt}\n");
            sb.AppendLine("### Boss warn lead times (ActiveTicks from warn to the event)\n");
            sb.AppendLine("| boss | warn | event | lead |\n|---|---|---|---|");
            sb.Append(WarnLeads());
            sb.AppendLine($"\n{sw.Elapsed.TotalSeconds:0} s");
            Emit(sb.ToString());
        }

        [Test]
        public void TraitTable()
        {
            var seeds = Env("BB_TSEEDS", 200);
            var ids = TraitRegistry.All.Select(e => e.Id).ToArray();
            var rows = new (double l1, double l2)[ids.Length];
            Parallel.For(0, ids.Length, i => rows[i] = (TraitBalanceReport.Delta(ids[i], 1, seeds), TraitBalanceReport.Delta(ids[i], 2, seeds)));
            var sb = new StringBuilder($"## Traits ({seeds} seeds; band L1 +3..+15, heavy +1.5..+15; L2 > L1)\n\n| trait | L1 | L2 |\n|---|---|---|\n");
            for (var i = 0; i < ids.Length; i++) sb.AppendLine($"| {ids[i]} | {rows[i].l1:+0.0;-0.0} | {rows[i].l2:+0.0;-0.0} |");
            Emit(sb.ToString());
        }

        static void Emit(string text)
        {
            TestContext.Out.WriteLine(text);
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, text + "\n");
        }
    }
}
