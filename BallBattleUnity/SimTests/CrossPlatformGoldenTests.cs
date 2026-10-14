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

        /// <summary>Rules 11 (BB-FIX2). Only the Rules field moved: the masked golden, the Versus digest and the arena digests are unchanged.</summary>
        static readonly ulong[] ExpectedBySeed =
        {
            0x59396B36A67AF523UL, 0xE28B326CEE1B59BEUL, 0x41D452F4E0553030UL, 0x9EB2198894AFACABUL, 0x980921F0FDD5CD17UL,
            0xB86AA0D24489C841UL, 0x75402D09D3949F35UL, 0x146E46DBC3AB641BUL, 0x87C1419A3DF638F1UL, 0x58A639D28E5D94A0UL,
            0x000521C008077FBEUL, 0x8612AEBC1B3BAF9CUL, 0x5C2DABE3C90C1540UL, 0x691E07D64B202F72UL, 0xB144D7340FD79354UL,
            0x0CBD0D8B875DA87EUL, 0x5FE475D796D0BC00UL, 0x1187B1FF28DBDF48UL, 0x80E358CEE8C234D0UL, 0x5B1F4043949D5DEFUL
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
            Assert.That(SimVersion.Rules, Is.EqualTo(11));
            Assert.That(Digests(m => m.ComputeHash(0), "masked"), Is.EqualTo(ExpectedBySeedMasked));
        }

        [Test]
        public void FixedMatchHashesMatchAcrossPlatforms()
        {
            var actual = Digests(m => m.ComputeHash(), "rules11");
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
