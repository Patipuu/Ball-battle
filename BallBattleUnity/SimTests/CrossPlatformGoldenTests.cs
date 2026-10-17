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

        /// <summary>Rules 20 (BB-19b: ten active skills for the Run player; Versus unchanged, so a Rules-only change, ExpectedBySeedMasked unchanged).
        /// Rules 19 values, in order: 0x60EAE7FE32229524 0xEB9DA0D389F2D6E9 0x33375D4CCDD024C9 0xFD4E5C70CCA4DD5F 0xBB72EE5BEA949212
        /// 0x739F78FDDC69C30B 0xB28CDE3B8B018BBC 0xCFEF2A861CA22B37 0x6BE6D961BCE28206 0x0043EBB35335A10C
        /// 0xB3E0D0B17E0574D2 0x1DDADB39D6169E5B 0xE383B0E39A0B1982 0x1D81AC868A486EE4 0x7BFB57667131F83B
        /// 0x70A578478A2CAE0F 0x2F14F64110E3F68B 0x448AF273120D8958 0xF158B468BE2769F3 0x7B93F4C343B29741.
        /// Rules 19 was (BB-19: the Run player's tap presses the skill; Versus unchanged, so a Rules-only change, ExpectedBySeedMasked unchanged).
        /// Rules 18 values, in order: 0xC7FE47921B069C84 0xE0989E5A7BB29B5D 0x0DF2DD95145037CD 0x1BB65D387DEADA0F 0xF005B64DFC39D3B6
        /// 0x0DD2F06DB09DFD17 0xCDFE77B483E61CD0 0xDA2EAD620589288B 0xD439F0DA2BDDB9DA 0x14EB308826040A80
        /// 0x025F66139B7F9B3A 0x558CBDD56B710BAB 0x9F4D1838B3602D32 0x31AD563767EB8164 0xEFAD7026DDF228AB
        /// 0x5FD7E3B2762A41CF 0x32C8C54A4ED60CB7 0xE20390D30FC07728 0xD120444F058369F3 0x2BB013D16C7F0085.
        /// Rules 18 was (BB-18 Versus profile; no chorus in these matches, so a Rules-only change, ExpectedBySeedMasked unchanged).
        /// Rules 17 values, in order: 0x8920196A66A316A4 0xC151A4F82A0F4201 0x48D92CD6DA4FC175 0x42AB077625198E07 0x70780901ADC764AA
        /// 0x66C9D470EF3CB67B 0x04CED1CB59B1A388 0x034B17D1569E5B93 0x6FB6D4456B7AEA62 0xC32FC4A6DEEB5240
        /// 0x89D64868E9B3526A 0x0FD772539CAC22BF 0x9CDE732FFE9F237A 0x9FC26F5900DC4858 0x29CE9160844845EF
        /// 0xE70FE670DB63100B 0x2EEA2710489552B7 0x9D0DCFCF9FE46648 0x529B1204C58DD26B 0x87EE92CD72F4A24D.
        /// Rules 16 values, in order: 0xCBE0F8688DC1243C 0xB8934AB1F8FC1A25 0x8D611E07AE30E9B1 0xC66406475747B5A7 0xF25BCCC79F57CFD6
        /// 0xC91B6611B1C0373F 0x59F94B4D33606534 0x5E6681491838EF5F 0x3E931D5BD1DA3D26 0x17B596CAF3A2F73C 0x34536EB596A34F12
        /// 0x91AF300E3A0B443F 0x9FAE34FB748AC5F2 0xB72B46FAC1EBDD70 0x66DAA3AD6D5480D7 0x986EF27758CA4A73 0xC4B31A5C6E560463
        /// 0x2E73365FA468B0C8 0xF8853AB98F496B93 0x0EE5A784DE906BB1.
        /// Rules 14 values, in order: 0x9E50742DA7669D6C 0xD725BD29BC476DBD 0xEFD6C99CE31C9F65 0x8BC55B5CA8771DEF 0x851E8A5A802FFEE6
        /// 0xC132495C9401A96F 0x597D57587E058EE8 0x235501535BA4786B 0x9A4213EAFF608E5A 0x0FEF36E7B896AF68 0xED64044064E76E02
        /// 0xE93742412830316B 0xA87E52B80DDBEF42 0x8DD8798F9E516E54 0x07BE331E824F7203 0x58FFBD549A51AD1F 0x38B4D00F420BAFFF
        /// 0x43F45697702DE948 0x6FF67070AAC1A293 0x45935FB5C9792F35.</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0xEE96574E679D21B4UL, 0x6B3A7D78D7AF6C15UL, 0x8CA855DFDF86A6E1UL, 0x5C70D359CA5D9D47UL, 0xD1DBE3D947611D76UL,
            0x355A227493CA81F7UL, 0xA1E41985420C5C74UL, 0xAF65B915BD725177UL, 0xE1C7154A2C5C59BEUL, 0x1A42228544735E8CUL,
            0x44F00DD91B81316AUL, 0x341D6D21C8DF0537UL, 0x61BB941CDBD2A882UL, 0x115ADC3D0FE11498UL, 0xFD7B00C25C8FC807UL,
            0x2CB9CBE6C389FF9BUL, 0xC95AE07EBF73C803UL, 0xE472084E2F1E5AB8UL, 0xB02457D9F1F0F7B3UL, 0x7B27CFFF4F0CB529UL
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
            Assert.That(SimVersion.Rules, Is.EqualTo(20));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules20");
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
