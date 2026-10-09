using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class RunBotReport
    {
        [Test]
        public void BotsFinishRunsDeterministically()
        {
            var a = RunBot.Play(11, RunBot.Policy.Greedy);
            var b = RunBot.Play(11, RunBot.Policy.Greedy);
            Assert.That((a.Won, a.FightsWon, a.BossesDefeated), Is.EqualTo((b.Won, b.FightsWon, b.BossesDefeated)));
            Assert.That(a.FightsWon, Is.InRange(0, 8));
        }

        /// <summary>Difficulty snapshot: 2,000 runs per policy, in parallel. Not a gate until Phase 7 tuning.</summary>
        [Test, Category("Diagnostic"), Explicit("long report: dotnet test --filter Name=RunWinRates")]
        public void RunWinRates()
        {
            const int runs = 2000;
            foreach (var policy in new[] { RunBot.Policy.Random, RunBot.Policy.Greedy })
            {
                var sw = Stopwatch.StartNew();
                var results = new RunBot.Result[runs];
                Parallel.For(0, runs, i => results[i] = RunBot.Play((uint)(i + 1), policy));
                sw.Stop();
                var won = results.Count(r => r.Won) * 100.0 / runs;
                var fights = results.Average(r => r.FightsWon);
                var bossHist = string.Join(" ", Enumerable.Range(0, 4).Select(k => $"{k}:{results.Count(r => r.BossesDefeated == k) * 100.0 / runs:0.#}%"));
                TestContext.Out.WriteLine($"{policy}: win {won:0.0}%  avg fights won {fights:0.00}  bosses {bossHist}  ({sw.Elapsed.TotalSeconds:0.0} s)");
            }
        }
    }
}
