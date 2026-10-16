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

        /// <summary>Rules 16 (BB-15 Run tuning; a Rules-only change for these Versus matches, ExpectedBySeedMasked unchanged).
        /// Rules 14 values, in order: 0x9E50742DA7669D6C 0xD725BD29BC476DBD 0xEFD6C99CE31C9F65 0x8BC55B5CA8771DEF 0x851E8A5A802FFEE6
        /// 0xC132495C9401A96F 0x597D57587E058EE8 0x235501535BA4786B 0x9A4213EAFF608E5A 0x0FEF36E7B896AF68 0xED64044064E76E02
        /// 0xE93742412830316B 0xA87E52B80DDBEF42 0x8DD8798F9E516E54 0x07BE331E824F7203 0x58FFBD549A51AD1F 0x38B4D00F420BAFFF
        /// 0x43F45697702DE948 0x6FF67070AAC1A293 0x45935FB5C9792F35.</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0xCBE0F8688DC1243CUL, 0xB8934AB1F8FC1A25UL, 0x8D611E07AE30E9B1UL, 0xC66406475747B5A7UL, 0xF25BCCC79F57CFD6UL,
            0xC91B6611B1C0373FUL, 0x59F94B4D33606534UL, 0x5E6681491838EF5FUL, 0x3E931D5BD1DA3D26UL, 0x17B596CAF3A2F73CUL,
            0x34536EB596A34F12UL, 0x91AF300E3A0B443FUL, 0x9FAE34FB748AC5F2UL, 0xB72B46FAC1EBDD70UL, 0x66DAA3AD6D5480D7UL,
            0x986EF27758CA4A73UL, 0xC4B31A5C6E560463UL, 0x2E73365FA468B0C8UL, 0xF8853AB98F496B93UL, 0x0EE5A784DE906BB1UL
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
            Assert.That(SimVersion.Rules, Is.EqualTo(16));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules16");
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
