using System.IO;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BallBattle.Tests
{
    public class VersusTraceTests
    {
        [Test]
        public void VersusRoundLogHasExactSeedTickAndHpBits()
        {
            var series = new Series("blade", "fang", 1454667u);
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), series.CurrentRoundSeed,
                new[] { WeaponRegistry.Create("blade"), WeaponRegistry.Create("fang") });
            sim.Balls[0].Hp = TFix.X(1.25f);
            sim.Balls[1].Hp = TFix.X(-0.5f);
            Assert.That(VersusRoundLog.Ended(series, sim), Is.EqualTo(
                "[BallBattle] Versus round 1 ended: match seed 1454667, round seed 4134000519, blade vs fang, winner DRAW, tick 0, hp A=0x00014000/1.25 B=0xFFFF8000/-0.5"));
            LogAssert.Expect(LogType.Log, "[BallBattle] Versus seed forced: 1454667");
            Assert.That(GameController.NewVersusSeed(1454667u), Is.EqualTo(1454667u));
            Assert.That(DebugOptions.ParseTrace(new[] { "-bbTrace" }), Is.True);
        }

        [Test]
        public void VersusTraceDump()
        {
            const uint matchSeed = 1454667u;
            var seeds = new[] { 4134000519u, 2686071142u };
            var root = Directory.GetParent(Application.dataPath).Parent.FullName;
            foreach (var seed in seeds)
            {
                var path = Path.Combine(root, "docs", "claude-runs", "trace-unity-" + seed + ".txt");
                var fieldsPath = Path.Combine(root, "docs", "claude-runs", "trace-unity-" + seed + ".fields.txt");
                var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), seed,
                    new[] { WeaponRegistry.Create("blade"), WeaponRegistry.Create("fang") });
                using (var trace = new StreamWriter(path, false))
                using (var fields = new StreamWriter(fieldsPath, false))
                {
                    trace.WriteLine(SimTrace.Line(sim));
                    fields.WriteLine(SimTrace.FieldsLine(sim));
                    while (sim.Outcome == MatchOutcome.Ongoing)
                    {
                        sim.Step();
                        trace.WriteLine(SimTrace.Line(sim));
                        fields.WriteLine(SimTrace.FieldsLine(sim));
                    }
                }
                Debug.Log($"[BallBattle] EditMode trace match seed {matchSeed}, round seed {seed}, ticks {sim.Tick}, winner {sim.WinnerIndex}: {path}");
            }
        }
    }
}
