using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Fixed v6 match checkpoints for comparing Unity and .NET after the Q16.16 conversion.</summary>
    public class CrossPlatformGoldenTests
    {
        static readonly string[] WeaponIds =
        {
            "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig"
        };

        // Each digest combines all eight matches for the corresponding seed, 1001 through 1020 (classic arena only).
        // Re-pinned only after the 500-new-seeds-per-side behaviour sanity check (6a.6b/c v3).
        // ComputeHash now includes Rules=6 and Fix raw state. Old -> new values are recorded in the task report.
        static readonly ulong[] ExpectedBySeed =
        {
            0xD431BAA2B27A7F85UL, 0x09B6D8FAB34726B1UL, 0x9DE2E81275E19CAAUL, 0x8D34C8605AE00592UL, 0xBB96BFE8B8B74FB0UL,
            0xAFFD9CF6EBF43C3BUL, 0x31AAB7BFB1C8432AUL, 0x17CD3635A56F18EFUL, 0xBD291ECDF617ECD2UL, 0x4C72CBBB5438199CUL,
            0xF18F3DA3E030682BUL, 0x13C44905533C4034UL, 0xA771D79EF8434B06UL, 0x39117D5FD64FA57EUL, 0x1EBB04D09D742B79UL,
            0x616561F76BC4B6F6UL, 0x18395BBA0F443050UL, 0x3FBCA3642292576FUL, 0x22884B6DAC023333UL, 0xD75E9E34F512B2BAUL

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
            Assert.That(FixVec2.FromAngleDeg(0).X, Is.EqualTo(Fix.One));
            Assert.That(FixVec2.FromAngleDeg(0).Y, Is.EqualTo(Fix.Zero));
            Assert.That(FixVec2.FromAngleDeg(90).X, Is.EqualTo(Fix.Zero));
            Assert.That(FixVec2.FromAngleDeg(90).Y, Is.EqualTo(Fix.One));
            Assert.That(FixVec2.FromAngleDeg(180).X, Is.EqualTo(-Fix.One));
            Assert.That(FixVec2.FromAngleDeg(180).Y, Is.EqualTo(Fix.Zero));
            Assert.That(FixVec2.FromAngleDeg(-90).Y, Is.EqualTo(-Fix.One));
        }
    }
}
