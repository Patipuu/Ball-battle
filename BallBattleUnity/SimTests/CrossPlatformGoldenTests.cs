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

        /// <summary>Rules 13 (BB-BAL3: Run DAMAGE card scales SPORE poison / MIRROR reflects; a Rules-only change for these Versus matches, ExpectedBySeedMasked unchanged).</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0x07587B313D420384UL, 0x244CB1F9C4409EA9UL, 0x7E4F4F266704C875UL, 0x3F0FC129E10A6537UL, 0x68FBDF8871966702UL,
            0x0DD505E7CCA82473UL, 0x406ED75FE702DF60UL, 0x24292B80CB91F80BUL, 0x4FEC23821F8204CAUL, 0xA3A71A9C7C0792D8UL,
            0xD1D8913E8EE76BCAUL, 0x8E8E58413AB777F7UL, 0xDB9FAC3D441FE65AUL, 0x4265F0602269CE60UL, 0x584B42B093C14AA7UL,
            0x6464B23ABCFEA0ABUL, 0xDEA042267A7AC96FUL, 0x6A3292AF97DC0050UL, 0xB72A7BFDCA62EC4BUL, 0x65A6A1798D9CD195UL
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
            Assert.That(SimVersion.Rules, Is.EqualTo(13));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules13");
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
