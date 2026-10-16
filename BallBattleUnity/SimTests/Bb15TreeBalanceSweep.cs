using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Reproducible BB-15 Run sweep: every legal ten-point tree, same seed set and greedy bot for each ball.</summary>
    [Explicit("Slow balance sweep; set BB_BAL_REPORT to save the CSV"), Category("Slow"), Category("Balance"), NonParallelizable]
    public class Bb15TreeBalanceSweep
    {
        static BallProgress Copy(BallProgress b) => new BallProgress
        {
            Id = b.Id, Level = b.Level, Vit = b.Vit, Pow = b.Pow, Growth = b.Growth,
            Start = b.Start, SkillUps = b.SkillUps, Branch = b.Branch, Finale = b.Finale
        };

        static string Key(BallProgress b) => $"V{b.Vit}P{b.Pow}I{b.Growth}O{(b.Start ? 1 : 0)}{(b.Branch == 1 ? 'A' : b.Branch == 2 ? 'B' : '-')}L{b.SkillUps + 1}F{(b.Finale ? 1 : 0)}";

        static BallProgress[] Builds(string id)
        {
            var seen = new Dictionary<string, BallProgress>();
            var stack = new Stack<BallProgress>();
            stack.Push(new BallProgress { Id = id, Level = 10 });
            while (stack.Count > 0)
            {
                var b = stack.Pop();
                if (seen.ContainsKey(Key(b))) continue;
                seen.Add(Key(b), b);
                foreach (TreeNode n in Enum.GetValues(typeof(TreeNode)))
                {
                    var next = Copy(b);
                    if (MetaTree.Add(next, n)) stack.Push(next);
                }
            }
            var result = seen.Values.Where(b => MetaTree.Spent(b) == 10).OrderBy(Key).ToArray();
            Assert.That(result.Length, Is.EqualTo(18), id);
            return result;
        }

        static bool Play(uint seed, string id, BallProgress tree)
        {
            // Start with the normal returning-player unlocks and offer. Force the measured ball exactly as the earlier bot
            // sweeps do, then apply its tree before the first card and fight. The initially selected ball has an empty tree.
            var progress = RunBot.BotProgress();
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress));
            var rng = new SimRandom(seed ^ 0xB07B07u);
            var run = RunState.Start(seed, choices[(int)(rng.NextUInt() % (uint)choices.Count)], progress);
            run.Build.WeaponId = id;
            if (tree != null) MetaTree.ApplyToRun(run.Build, tree);
            RunBot.PickGreedyStart(run);
            while (!run.Over)
            {
                if (run.StartPickPending) RunBot.PickGreedyStart(run);
                else RunBot.PickGreedy(run);
                var match = run.CreateMatch();
                while (match.Outcome == MatchOutcome.Ongoing) match.Step();
                run.ApplyResult(match);
            }
            return run.Won;
        }

        [Test]
        public void Sweep()
        {
            var count = int.TryParse(Environment.GetEnvironmentVariable("BB_BAL_SEEDS"), out var parsed) ? parsed : 200;
            Assert.That(count, Is.InRange(1, 2000));
            var start = int.TryParse(Environment.GetEnvironmentVariable("BB_BAL_START"), out var first) ? first : 3001;
            Assert.That(start, Is.GreaterThan(0));
            var ids = (Environment.GetEnvironmentVariable("BB_BAL_BALLS") ?? string.Join(",", TreeTuning.WeaponOrder))
                .Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToArray();
            var csv = new StringBuilder("ball,build,seeds,wins,pct\n");
            foreach (var id in ids)
            {
                Assert.That(TreeTuning.WeaponOrder, Does.Contain(id));
                var builds = Builds(id).Cast<BallProgress>().Prepend(null).ToArray();
                foreach (var b in builds)
                {
                    var wins = new bool[count];
                    Parallel.For(0, count, i => wins[i] = Play((uint)(start + i), id, b));
                    var won = wins.Count(x => x);
                    csv.AppendLine($"{id},{(b == null ? "LV1" : Key(b))},{count},{won},{100.0 * won / count:0.0}");
                }
                TestContext.WriteLine($"{id}: sweep done");
            }
            var path = Environment.GetEnvironmentVariable("BB_BAL_REPORT");
            if (!string.IsNullOrEmpty(path)) File.WriteAllText(path, csv.ToString());
            TestContext.WriteLine(csv.ToString());
        }
    }
}
