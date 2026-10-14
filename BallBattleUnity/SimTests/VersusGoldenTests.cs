using System.Linq;
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
        /// <summary>Recomputed for SimVersion 7 (BB-6c: spike immunity + Fang/Brawler tuning). Was 0xDA949C380918B81DUL.</summary>
        const ulong ExpectedDigest = 0x6EBF6B88AB6F72B3UL;

        /// <summary>The four Step 1 weapons only: later weapons have their own balance gate.</summary>
        static readonly string[] Step1Ids = { "blade", "fang", "pike", "brawler" };
        static System.Collections.Generic.IEnumerable<WeaponRegistry.Entry> Step1Weapons => WeaponRegistry.All.Where(e => Step1Ids.Contains(e.Id));

        [Test]
        public void VersusOutcomesMatchGolden()
        {
            var h = 14695981039346656037UL;
            foreach (var a in Step1Weapons)
            foreach (var b in Step1Weapons)
            for (uint seed = 1; seed <= 5; seed++)
            {
                var m = new MatchSim(new MatchConfig(), seed, new[] { a.Create(), b.Create() });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                h = Mix(h, (ulong)(m.WinnerIndex + 2));
                h = Mix(h, (ulong)m.Tick);
                h = Mix(h, (ulong)(uint)m.Balls[0].Hp.Raw);
                h = Mix(h, (ulong)(uint)m.Balls[1].Hp.Raw);
            }

            TestContext.Out.WriteLine($"VERSUS DIGEST 0x{h:X16}UL");
            Assert.That(h, Is.EqualTo(ExpectedDigest), "Versus results changed; see class comment");
        }

        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;
    }
}
