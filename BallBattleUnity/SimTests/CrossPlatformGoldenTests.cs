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

        // Each digest combines all eight matches for the corresponding seed, 1001 through 1020 (classic arena only).
        // SimVersion 5 (BB-FIX1a): ComputeHash mixes SimVersion.Rules, so every value changed with the 4 -> 5 bump alone.
        // Proof the physics did not: with Rules temporarily set back to 4 and the spike change in place, all 20 v4 values
        // (0xD2A88D3A96477721 ... 0x2E6F293A3DBD6F44, see git history) still matched; VersusGoldenTests is unchanged.
        static readonly ulong[] ExpectedBySeed =
        {
            0xEEF73442912C5F45UL, 0x6C9CB1F3F47585BCUL, 0x0D583A4B652498D4UL, 0xE10BE86758682CAAUL, 0x615720C12CA952DDUL,
            0x5CC4EF5B8454F3C7UL, 0x77D845F46A6EED88UL, 0xA5809087BE6321EEUL, 0x0939F4085273C6D9UL, 0xBCBB361DF229BEC7UL,
            0x7539F734E141493EUL, 0xE4625F6355701BE0UL, 0xE1FDD36872D3E8ADUL, 0x0451B5F480D2F852UL, 0x39EED389C95D132DUL,
            0xDC267EEC4A98C1E8UL, 0xDEAA56E5352B12FDUL, 0x49A9C47C5F4DFE72UL, 0x838AED19B6A6037FUL, 0x0F586DA5838887A4UL
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
