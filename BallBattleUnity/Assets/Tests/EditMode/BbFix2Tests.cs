using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>BB-FIX2 View side: the SHELL KING trace logs the same angle the Sim's block test judged.</summary>
    public class BbFix2Tests
    {
        /// <summary>One WRECKER body blow on SHELL KING (centre, facing left) from <paramref name="tenths"/> off the facing. With
        /// <paramref name="bossMoves"/> the boss is moving, so it is somewhere else by the end of the step than at the contact.</summary>
        static List<(SimEvent e, string line, float endOfStepAngle)> BodyBlow(int tenths, bool bossMoves)
        {
            var cfg = new MatchConfig { Gravity = Fix.Zero, MinHorizontalSpeed = Fix.Zero, ShrinkStartTick = int.MaxValue, CapTicks = int.MaxValue };
            var player = new BallLoadout(WeaponRegistry.Create(BrawlerRule.WeaponId)) { MaxHp = 9999 };
            var shell = new ShellKingRule();
            var bossWeapon = WeaponRegistry.Create(BladeRule.WeaponId);
            bossWeapon.SpinDegPerTick = Fix.Zero;
            var boss = new BallLoadout(bossWeapon) { MaxHp = 9999, Radius = RunTuning.BossRadius, Boss = shell };
            var m = new MatchSim(cfg, 1, new[] { player, boss });
            void Park(FixVec2 p) { m.Balls[0].Pos = p; m.Balls[0].Vel = FixVec2.Zero; m.Balls[1].Pos = FixVec2.Zero; m.Balls[1].Vel = FixVec2.Zero; }
            m.Balls[1].WeaponAngleDeg = Fix.FromInt(90);   // boss blade pointing up and away from the contact side
            Park(TFix.V(-90, 0));
            while (!shell.ShellOn) { m.Step(); Park(TFix.V(-90, 0)); }
            var off = new FixVec2(-FixedTrig.Cos(tenths), -FixedTrig.Sin(tenths));   // lower side, away from the boss blade
            Park(off * (RunTuning.BossRadius + m.Balls[0].Radius - 1));
            if (bossMoves) m.Balls[1].Vel = TFix.V(5, -3);   // roughly along the contact tangent: the blow still lands
            m.Step();
            var list = new List<(SimEvent, string, float)>();
            foreach (var e in m.Events.Where(e => e.Type == SimEventType.Hit && e.A == 0 && e.B == 1))
                list.Add((e, ShellTrace.Line(e, m), ShellTrace.AngleDeg(m.Balls[1].Pos, shell.Facing, e.Point)));
            return list;
        }

        [TestCase(599, "angle=59.9", "blocked=true")]
        [TestCase(601, "angle=60.1", "blocked=false")]
        public void TraceLogsTheJudgedAngleAtTheArcEdge(int tenths, string angle, string blocked)
        {
            var hits = BodyBlow(tenths, false);
            Assert.That(hits.Count, Is.EqualTo(1));
            var (e, line, _) = hits[0];
            Assert.That(e.ArcAngleTenths, Is.EqualTo(tenths));
            Assert.That(line, Does.Contain(angle + " ").And.Contain(blocked), line);
        }

        [Test]
        public void TraceAngleIsTheJudgedOneEvenWhenTheBossMovesAfterTheContact()
        {
            var hits = BodyBlow(590, true);
            Assert.That(hits.Count, Is.EqualTo(1));
            var (e, line, endOfStep) = hits[0];
            var judged = e.ArcAngleTenths / 10f;
            Assert.That(line, Does.Contain("angle=" + judged.ToString("0.0", CultureInfo.InvariantCulture) + " "), line);
            Assert.That(line.Contains("blocked=true"), Is.EqualTo(e.ArcAngleTenths <= 600), line);
            TestContext.Out.WriteLine($"{line} | old end-of-step angle {endOfStep:0.0}");
            Assert.That(System.Math.Abs(endOfStep - judged), Is.GreaterThan(0.5f), "fixture: the old end-of-step angle drifts (the BB-6d trace bug)");
        }
    }
}
