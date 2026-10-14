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
        // ComputeHash includes Rules and Fix raw state. Re-pinned for Rules 7 (BB-6c: spike immunity state + tuning);
        // old -> new values are recorded in docs/claude-runs/bb6c.md.
        static readonly ulong[] ExpectedBySeed =
        {
            0x811EE019E6FF9EA5UL, 0xC1EBDB28D8A39134UL, 0xB594B3FC1A893861UL, 0x77292940629549A5UL, 0x6490DE3218A0494BUL,
            0xA3B0EC99568C8285UL, 0x7B5D0CF71CE95692UL, 0x9518F1861721ABC0UL, 0x208606C6110A95B7UL, 0x2D13DAD3E301EC0DUL,
            0x3A2DB9CEBB2D1F72UL, 0xEA097C749277748DUL, 0xB7B4CFCD4E373D85UL, 0xCB1A5E6CF7CFB24BUL, 0xB9E763B351ABC249UL,
            0x64C75AB957AD6F77UL, 0xC1E7633A073D60B4UL, 0xE5C04579EFC31AD3UL, 0x0ED005F15E3BB30BUL, 0x7544B05225D4ECFDUL

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
