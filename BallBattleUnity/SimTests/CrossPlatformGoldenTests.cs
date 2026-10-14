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
        // ComputeHash includes Rules and Fix raw state. Pinned for Rules 7 (BB-6c: spike immunity state + tuning; old -> new
        // values in docs/claude-runs/bb6c.md). BB-04 (Rules 8) must not change any Versus result: these Rules-7 values are kept and
        // checked with the Rules field masked (hash computed with Rules = 7), and only the Rules field may differ in ExpectedBySeed.
        static readonly ulong[] ExpectedBySeedRules7 =
        {
            0x811EE019E6FF9EA5UL, 0xC1EBDB28D8A39134UL, 0xB594B3FC1A893861UL, 0x77292940629549A5UL, 0x6490DE3218A0494BUL,
            0xA3B0EC99568C8285UL, 0x7B5D0CF71CE95692UL, 0x9518F1861721ABC0UL, 0x208606C6110A95B7UL, 0x2D13DAD3E301EC0DUL,
            0x3A2DB9CEBB2D1F72UL, 0xEA097C749277748DUL, 0xB7B4CFCD4E373D85UL, 0xCB1A5E6CF7CFB24BUL, 0xB9E763B351ABC249UL,
            0x64C75AB957AD6F77UL, 0xC1E7633A073D60B4UL, 0xE5C04579EFC31AD3UL, 0x0ED005F15E3BB30BUL, 0x7544B05225D4ECFDUL

        };

        /// <summary>Rules 8 (BB-04): the same matches; only the Rules field of the hash changed (see the masked test).</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0xFC278AB9A6DE20E1UL, 0x33FA9BB8C468D298UL, 0x49A9A7D4188E10ADUL, 0xD5B3FB8B0CA61051UL, 0xDF8360972E58C94FUL,
            0xC2FCFDA2118DB38DUL, 0x194DBB261BDDAC66UL, 0xC504669D35C3AB74UL, 0x96F698F886006E03UL, 0xDD623B2EED691341UL,
            0x85C3CB809D806AA6UL, 0x319FB12041AA6069UL, 0xF87C9B871BA92C85UL, 0x6570DAC441ECD09BUL, 0xD0F38E815B359529UL,
            0xBC51977F375F8DA3UL, 0xABAFE0556A9BBB84UL, 0xF5735B8AD273999FUL, 0xFE333A3F5FAD7B1BUL, 0x2CECC837591FC039UL
        };

        static ulong[] Digests(System.Func<MatchSim, ulong> hash, string label)
        {
            var actual = new ulong[ExpectedBySeedRules7.Length];
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
                    digest = (digest ^ hash(match)) * 1099511628211UL;
                }
                actual[i] = digest;
                TestContext.Out.WriteLine($"{label} seed {seed}: 0x{digest:X16}");
            }
            return actual;
        }

        /// <summary>
        /// BB-04 proof that the Rules 7 → 8 bump changed nothing in Versus: with the Rules field masked (the hash computed with the old
        /// Rules number 7) every match hash equals its Rules-7 golden exactly: positions, velocities, HP, ticks, RNG, all of it.
        /// </summary>
        [Test]
        public void VersusGoldensWithTheRulesFieldMaskedAreUnchanged()
        {
            Assert.That(SimVersion.Rules, Is.EqualTo(8));
            Assert.That(Digests(m => m.ComputeHash(7), "masked"), Is.EqualTo(ExpectedBySeedRules7));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules8");
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
