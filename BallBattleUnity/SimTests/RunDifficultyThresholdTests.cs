using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Run difficulty gate (plan phase 7). Fixed seeds, so exact for a given build. Bots only guard both ends
    /// (too easy / too hard); human playtests decide the rest. 800 runs per policy, ~1.5 min.
    /// </summary>
    [Category("Balance"), NonParallelizable]
    public class RunDifficultyThresholdTests
    {
        const int Runs = 800;
        static RunBot.Result[] random;
        static RunBot.Result[] greedy;

        static RunBot.Result[] PlayAll(RunBot.Policy policy)
        {
            var r = new RunBot.Result[Runs];
            Parallel.For(0, Runs, i => r[i] = RunBot.Play((uint)(i + 1), policy));
            return r;
        }

        static double WinPct(RunBot.Result[] r) => r.Count(x => x.Won) * 100.0 / r.Length;

        [OneTimeSetUp]
        public void Play()
        {
            random = PlayAll(RunBot.Policy.Random);
            greedy = PlayAll(RunBot.Policy.Greedy);
        }

        [Test]
        public void RandomBotWins1To6Percent() => Assert.That(WinPct(random), Is.InRange(1.0, 6.0), $"N={Runs}, measured {WinPct(random):0.0}%");

        [Test]
        public void GreedyBotWins25To40Percent() => Assert.That(WinPct(greedy), Is.InRange(25.0, 40.0), $"N={Runs}, measured {WinPct(greedy):0.0}%");
    }
}
