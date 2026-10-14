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
            0x556BAF253A2154FCUL, 0x8266C02873BC4CC5UL, 0x6BCE88E2863FF991UL, 0xE4C23C4CB9F5F767UL, 0x7D6E5D7818072BF6UL,
            0xD31FC1CC6C6091FFUL, 0xB5164B18310B9F94UL, 0xCF80BA671B3E3B1FUL, 0x09FF6668B4393D26UL, 0xF3182FCF0C30D13CUL,
            0x986481D242A1D1D2UL, 0xC873C192C572B5BFUL, 0x216CA5EF04EC1072UL, 0xE5C5EFABD73047D0UL, 0x9F6BEF0A41EC3957UL,
            0x794573E6B0A58033UL, 0x32089F649AA33643UL, 0x711E97B9F59E2D48UL, 0x7B39BD1379F55673UL, 0x56AE5105EE29D931UL
        };

        /// <summary>Rules 12 (BB-BAL1 values pass).</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0xEF52DEA026BF8E54UL, 0xD66ADB60E13DF335UL, 0x44ADC495C8F211A1UL, 0x98C3BDB4E76AC2D7UL, 0x69C19665F766C756UL,
            0xC53623F1C01196C7UL, 0xA3085EFB4D8B3124UL, 0x2A626D3D0672AF67UL, 0x7323175FFF8A6C1EUL, 0x15AA4CABA3A6DF0CUL,
            0x16502A9C0A90CE0AUL, 0x1B62BBF63D3DE747UL, 0xAD350D500B37B452UL, 0x41AD448818F5E5E8UL, 0x2725CE1735EF7CC7UL,
            0xCD8D662F59FDD30BUL, 0x7D9A87A7E5AD1CA3UL, 0x5512E9990F301C28UL, 0xFC33DEB355AC4E53UL, 0x994231179209EE99UL
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
        public void MaskedMatchHashesMatchTheSignatureSkillGolden()
        {
            Assert.That(SimVersion.Rules, Is.EqualTo(12));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules12");
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
