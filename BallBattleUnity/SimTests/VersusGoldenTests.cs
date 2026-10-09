using System;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Golden outcomes of plain Versus matches (winner, length, final HP of every weapon pair over a few seeds).
    /// Guards Step 1 results against accidental change while the sim grows. If a change is MEANT to alter
    /// Versus results (rule or WeaponTuning change), bump SimVersion.Rules, rerun the balance report and
    /// update the expected digest here.
    /// </summary>
    public class VersusGoldenTests
    {
        /// <summary>Computed on commit 91a1bf6 (end of Step 1) and unchanged by Step 2 Phase 1.</summary>
        const ulong ExpectedDigest = 0x2E92A642D4E02B1AUL;

        [Test]
        public void VersusOutcomesMatchGolden()
        {
            var h = 14695981039346656037UL;
            foreach (var a in WeaponRegistry.All)
            foreach (var b in WeaponRegistry.All)
            for (uint seed = 1; seed <= 5; seed++)
            {
                var m = new MatchSim(new MatchConfig(), seed, new[] { a.Create(), b.Create() });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                h = Mix(h, (ulong)(m.WinnerIndex + 2));
                h = Mix(h, (ulong)m.Tick);
                h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(m.Balls[0].Hp));
                h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(m.Balls[1].Hp));
            }

            TestContext.Out.WriteLine($"VERSUS DIGEST 0x{h:X16}UL");
            Assert.That(h, Is.EqualTo(ExpectedDigest), "Versus results changed; see class comment");
        }

        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
    }
}
