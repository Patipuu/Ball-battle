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
        // ComputeHash includes Rules and Fix raw state. BB-6d (Rules 9) changes Versus on purpose (MIRROR grows on combat, MANTIS
        // tuning), so both arrays were re-pinned once at the end of BB-6d, and again at the end of BB-6e (still Rules 9: WARDEN floor, MIRROR shot guard, WRECKER wall boost). ExpectedBySeedMasked is the same digest with the Rules field
        // masked (hashed as 0): a later Rules-only bump must leave it unchanged. Old -> new values in docs/claude-runs/bb6d.md.
        static readonly ulong[] ExpectedBySeedMasked =
        {
            0x7B733571A0A174EEUL, 0x8FEB52249BE8F4D7UL, 0x6C9C855BBB4930ACUL, 0x3A70B07E6E96604DUL, 0xD37BB5B3F9635AFEUL,
            0x5108F3C2E47C5E80UL, 0xAA1B4E8FD0E5FBC5UL, 0x7C1B4642762C87B6UL, 0xA1A965D13107BD66UL, 0xB49F52E4B2D46E90UL,
            0xF999F35EB17B7562UL, 0x33F4FBFECA4880CDUL, 0xAC19B1C7283763FBUL, 0xB4695EB2CC9B5E2CUL, 0x0DFDF4F2BD59D071UL,
            0x6432E20EDAF4154AUL, 0x4A7C9B8824CD71DFUL, 0xD6CC8BD303003FA0UL, 0x9EF5905DDD58FA58UL, 0xCE4F568AA841F200UL
        };

        /// <summary>Rules 9 (BB-6d, re-pinned at the end of BB-6e).</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0x2263C40910E58196UL, 0xF6D1E46153E04287UL, 0x69B4CFC5F081DECCUL, 0xEAAEF9D3BD339DCDUL, 0x75C116BBE433083EUL,
            0x8ED498C2C17FC614UL, 0x4B2CEAB22AC45D35UL, 0x920286B10A5E0F66UL, 0xB95397FCA404E5DEUL, 0xD2608EADD4AB3484UL,
            0x42DDA87E536FFEAAUL, 0xAFE354FF7B4D0EB9UL, 0xD47C7955621DE5ABUL, 0x39B4612A127592D0UL, 0xBD82BB1811202DF9UL,
            0x3A67ECC40F96C9B6UL, 0xF81CEB9F71FF77FBUL, 0x8C96F8F64DA63264UL, 0x529D4DA548C5C994UL, 0xEB69BE3F546F6144UL
        };

        static ulong[] Digests(System.Func<MatchSim, ulong> hash, string label)
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
                    digest = (digest ^ hash(match)) * 1099511628211UL;
                }
                actual[i] = digest;
                TestContext.Out.WriteLine($"{label} seed {seed}: 0x{digest:X16}");
            }
            return actual;
        }

        /// <summary>
        /// Rules-independent pin of the same matches: the hash with the Rules field masked (computed with Rules = 0). BB-04 used this to
        /// prove its Rules bump changed no Versus result; keep it for the next Rules-only bump (only ExpectedBySeed may change then).
        /// </summary>
        [Test]
        public void VersusGoldensWithTheRulesFieldMaskedAreUnchanged()
        {
            Assert.That(SimVersion.Rules, Is.EqualTo(9));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules9");
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
