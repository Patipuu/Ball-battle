using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Fixed v4 match checkpoints for comparing Windows Unity and Linux .NET.</summary>
    public class CrossPlatformGoldenTests
    {
        static readonly string[] WeaponIds =
        {
            "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig"
        };

        // Each digest combines all eight matches for the corresponding seed, 1001 through 1020.
        static readonly ulong[] ExpectedBySeed =
        {
            0xD2A88D3A96477721UL, 0xE41DE80F5673FEE4UL, 0x0FBF8A985272AAA8UL, 0x7DECC41486EFF74AUL, 0x4E3026B530577565UL,
            0x97F88E0485EA4B17UL, 0x0023568BD318A85CUL, 0x4E832E97BAE519C2UL, 0xE96FF8CD4002AEBDUL, 0xBD0175ED4EA5802BUL,
            0x4BF78EC9088D4ACAUL, 0xA812849A6309EC3CUL, 0xDA5499B6F3353975UL, 0xBEDF963BA1E32256UL, 0x6903CCE4D91DAB4DUL,
            0xDC5C8AE2ECE8F330UL, 0xF4D3BDA889E1AE31UL, 0xEBEF9835A4E5C86AUL, 0xC9352F26E5AFCC83UL, 0x2E6F293A3DBD6F44UL
        };

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = new ulong[ExpectedBySeed.Length];
            for (var i = 0; i < actual.Length; i++)
            {
                var seed = (uint)(1001 + i);
                var digest = 14695981039346656037UL;
                foreach (var id in WeaponIds)
                {
                    var match = new MatchSim(new MatchConfig(), seed, new[]
                    {
                        WeaponRegistry.Create(id), WeaponRegistry.Create("blade")
                    });
                    while (match.Outcome == MatchOutcome.Ongoing && match.Tick < 1800)
                        match.Step();
                    digest = (digest ^ match.ComputeHash()) * 1099511628211UL;
                }
                actual[i] = digest;
                TestContext.Out.WriteLine($"seed {seed}: 0x{digest:X16}");
            }
            Assert.That(actual, Is.EqualTo(ExpectedBySeed));
        }

        [Test]
        public void TrigTableHasExactCardinalDirections()
        {
            Assert.That(Vec2.FromAngleDeg(0f).X, Is.EqualTo(1f));
            Assert.That(Vec2.FromAngleDeg(0f).Y, Is.EqualTo(0f));
            Assert.That(Vec2.FromAngleDeg(90f).X, Is.EqualTo(0f));
            Assert.That(Vec2.FromAngleDeg(90f).Y, Is.EqualTo(1f));
            Assert.That(Vec2.FromAngleDeg(180f).X, Is.EqualTo(-1f));
            Assert.That(Vec2.FromAngleDeg(180f).Y, Is.EqualTo(0f));
            Assert.That(Vec2.FromAngleDeg(-90f).Y, Is.EqualTo(-1f));
        }
    }
}
