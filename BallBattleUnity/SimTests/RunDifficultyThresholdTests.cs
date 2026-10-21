using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Run difficulty gate (plan phase 7). Fixed seeds, so exact for a given build. Bots only guard both ends
    /// (too easy / too hard); human playtests decide the rest. 800 runs per policy, LV1 balls.
    /// Target (Phu, 21/10): a LV1 player who uses the skill clears a run in about 5-6 runs, so Skilled wins 15-25% of runs.
    /// </summary>
    [Category("Balance"), NonParallelizable]
    public class RunDifficultyThresholdTests
    {
        const int Runs = 800;
        static RunBot.Result[] random;
        static RunBot.Result[] greedy;
        static RunBot.Result[] skilled;

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
            skilled = PlayAll(RunBot.Policy.Skilled);
            TestContext.Progress.WriteLine($"Run bot wins (N={Runs}): random {WinPct(random):0.0}%, greedy {WinPct(greedy):0.0}%, skilled {WinPct(skilled):0.0}%");
        }

        [Test]
        // Rules 23: a player who never taps the skill should almost never clear the run (was 1-6%).
        public void RandomBotWinsAtMost6Percent() => Assert.That(WinPct(random), Is.InRange(0.0, 6.0), $"N={Runs}, measured {WinPct(random):0.0}%");

        [Test]
        // Rules 23: casting the skill only once per fight is not enough any more (was 22-40%; measured 1.1%).
        public void GreedyBotWinsAtMost10Percent() => Assert.That(WinPct(greedy), Is.InRange(0.0, 10.0), $"N={Runs}, measured {WinPct(greedy):0.0}%");

        [Test]
        // The reference: taps whenever the skill is ready. 15-25% per run = first clear in about 4-7 runs (chapter 1, measured 20.1%).
        public void SkilledBotWins15To25Percent() => Assert.That(WinPct(skilled), Is.InRange(15.0, 25.0), $"N={Runs}, measured {WinPct(skilled):0.0}%");

        [Test]
        // The first three fights stay friendly: a skilled LV1 player reaches the first boss most of the time.
        public void SkilledBotUsuallyReachesTheFirstBoss() =>
            Assert.That(skilled.Count(x => x.FightsWon >= 3) * 100.0 / Runs, Is.GreaterThanOrEqualTo(60.0));
    }
}
