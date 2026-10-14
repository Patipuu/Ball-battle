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
            0xB08A3E8532EB75EBUL, 0x821A3EB95657CB56UL, 0xF7AD8BBCF8DC1960UL, 0xF254144217407D67UL, 0xFE274C2FF70CE0A3UL,
            0xCF97B7BEEB54EEEDUL, 0xEBDAE17F7A61BFE5UL, 0x870C0CCBDD1F660BUL, 0xC4575675F591A221UL, 0x49F51F9BD9383AD0UL,
            0x1C0EB81F63004706UL, 0x1DCD11D667DE8704UL, 0x7316918634282CF4UL, 0x16A268215B6317E6UL, 0x0FE14F5F692918ECUL,
            0x438D84AC34BDA54EUL, 0xDB301CB2B1D7B3E0UL, 0xE8701EAC69CD65B0UL, 0x49B44B94068D6E98UL, 0x11977DC067D5C643UL
        };

        /// <summary>Rules 9 (BB-6d, re-pinned at the end of BB-6e).</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0xA518CFAAA4AB71F3UL, 0x42F710633954DDB2UL, 0x6DD0388483739098UL, 0xC47CD7871E3A034BUL, 0xA54A12BA320B4423UL,
            0xDAACBEE377CB13F5UL, 0x56CEDF75327ED721UL, 0x02D1A8069B1D1F13UL, 0x0D5DD14F35474681UL, 0xC86D38B3110E1258UL,
            0x3EC79E31F463CFB6UL, 0x7391C407F5397C70UL, 0x5FC45BC93A2CB930UL, 0x9D59061858545702UL, 0xD71809752C5A9AC8UL,
            0x5A24210AA10C94FEUL, 0xC3F8837D6FD98C6CUL, 0x26B44DD5BEFFDD50UL, 0x99DE2F84C29503C4UL, 0x0430C2F848D877D3UL
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
            Assert.That(SimVersion.Rules, Is.EqualTo(10));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules10");
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
