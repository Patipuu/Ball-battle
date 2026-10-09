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
        public void RandomBotWins5To20Percent() => Assert.That(WinPct(random), Is.InRange(5.0, 20.0));

        [Test]
        public void GreedyBotWins30To60Percent() => Assert.That(WinPct(greedy), Is.InRange(30.0, 60.0));

        [Test]
        public void ThirdGiantBossBeatsRandomBotAtLeast40Percent()
        {
            var reached = random.Count(x => x.BossesDefeated >= 2);
            var lost = random.Count(x => x.BossesDefeated == 2);
            Assert.That(reached, Is.GreaterThan(30), "too few runs reach the last boss to judge");
            Assert.That(lost * 100.0 / reached, Is.GreaterThanOrEqualTo(40.0));
        }
    }
}