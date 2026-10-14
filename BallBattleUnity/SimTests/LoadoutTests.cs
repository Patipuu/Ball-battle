using System;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class LoadoutTests
    {
        [Test]
        public void ZeroStartHpIsRejected()
        {
            var l = new BallLoadout(new HarmlessRule()) { Hp = FT.X(0f) };
            Assert.Throws<ArgumentException>(() => new MatchSim(new MatchConfig(), 1, new[] { l, new BallLoadout(new HarmlessRule()) }));
        }

        [Test]
        public void RejectedLoadoutsBindNothing()
        {
            var shared = new ScaleTrait(1f, 1f);
            var a = new BallLoadout(new HarmlessRule()).With(shared);
            var b = new BallLoadout(new HarmlessRule()).With(shared);
            Assert.Throws<InvalidOperationException>(() => new MatchSim(new MatchConfig(), 1, new[] { a, b }));

            b.Traits.Clear();
            b.Traits.Add(new ScaleTrait(1f, 1f));
            Assert.DoesNotThrow(() => new MatchSim(new MatchConfig(), 1, new[] { a, b }), "same weapons and first trait are still free");
        }

        [Test]
        public void BigBallBladeStartsAtSameDistanceFromEdge()
        {
            var m = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new FixedBladeRule()) { Radius = FT.X(24f) },
                new BallLoadout(new FixedBladeRule())
            });
            float Inner(BallState s) => ((s.BladeStart - s.Pos).Length - s.Radius).F();
            Assert.That(Inner(m.Balls[0]).F(), Is.EqualTo(Inner(m.Balls[1]).F()).Within(1e-3f));
        }

        [Test]
        public void BossSizedBallWithCarriedHpSpawnsCorrectly()
        {
            var boss = new BallLoadout(new HarmlessRule()) { Radius = FT.X(24f), MaxHp = FT.X(250f), Hp = FT.X(100f) };
            var m = new MatchSim(new MatchConfig(), 1, new[] { boss, new BallLoadout(new HarmlessRule()) });
            var b = m.Balls[0];

            Assert.That(b.Radius.F(), Is.EqualTo(24f));
            Assert.That(b.MaxHp.F(), Is.EqualTo(250f));
            Assert.That(b.Hp.F(), Is.EqualTo(100f));
            // 100/250 truncates to exactly 26214 raw; decimal 0.4 is not representable in Q16.16.
            Assert.That(b.HpFraction.Raw, Is.EqualTo(26214));
            Assert.That(m.Balls[1].Radius.F(), Is.EqualTo(16f), "default size for the other ball");
        }

        [Test]
        public void CarriedHpIsClampedToMaxHp()
        {
            var l = new BallLoadout(new HarmlessRule()) { MaxHp = FT.X(80f), Hp = FT.X(120f) };
            var m = new MatchSim(new MatchConfig(), 1, new[] { l, new BallLoadout(new HarmlessRule()) });
            Assert.That(m.Balls[0].Hp.F(), Is.EqualTo(80f));
        }

        [Test]
        public void BigBallStaysInsideAndNeverStaysOverlapped()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue };
            var m = new MatchSim(cfg, 8, new[]
            {
                new BallLoadout(new HarmlessRule()) { Radius = FT.X(24f) },
                new BallLoadout(new HarmlessRule())
            });
            var overlapRun = 0;
            for (var t = 0; t < 200_000; t++)
            {
                m.Step();
                var a = m.Arena;
                foreach (var b in m.Balls)
                    if (b.Pos.X - b.Radius < a.Left - FT.X(1e-3f) || b.Pos.X + b.Radius > a.Right + FT.X(1e-3f) ||
                        b.Pos.Y - b.Radius < a.Bottom - FT.X(1e-3f) || b.Pos.Y + b.Radius > a.Top + FT.X(1e-3f))
                        Assert.Fail($"Ball {b.Index} outside arena {a} at tick {m.Tick}");

                // Walls resolve last, so a wall can leave the pair overlapping for a substep (Step 1 does this
                // too: up to ~3 px with two r16 balls). It must never persist.
                var overlap = FT.X(40f) - (m.Balls[0].Pos - m.Balls[1].Pos).Length;
                if (overlap > FT.X(5)) Assert.Fail($"Balls overlap by {overlap} at tick {m.Tick}");
                overlapRun = overlap > FT.X(0.5) ? overlapRun + 1 : 0;
                if (overlapRun > 3) Assert.Fail($"Balls stayed overlapped for {overlapRun} ticks at tick {m.Tick}");
            }
        }

        [Test]
        public void HeavierBallIsPushedLess()
        {
            var m = Stage.Build(null,
                (new BallLoadout(new HarmlessRule()) { Radius = FT.X(24f) }, FT.V(-40f, 0f), 0f),
                (new BallLoadout(new HarmlessRule()), FT.V(40f, 0f), 0f));
            m.Balls[0].Vel = FT.V(4f, 0f);
            m.Balls[1].Vel = FT.V(-4f, 0f);
            for (var i = 0; i < 12; i++) m.Step();

            Assert.That(m.Balls[0].Vel.X.F(), Is.LessThan(0f));
            Assert.That(MathF.Abs((m.Balls[0].Vel.X).F()), Is.LessThan(MathF.Abs((m.Balls[1].Vel.X).F())), "big ball rebounds slower");
            var momentum = m.Balls[0].Vel.X * FT.X(24f) * FT.X(24f) + m.Balls[1].Vel.X * FT.X(16f) * FT.X(16f);
            Assert.That(momentum.F(), Is.EqualTo((4f * 24f * 24f - 4f * 16f * 16f).F()).Within(1e-2f), "momentum conserved");
        }

        [Test]
        public void SpeedBonusRaisesCap()
        {
            var fast = new BallLoadout(new HarmlessRule()) { Bonus = new StatBonus { SpeedPct = FT.X(0.2f) } };
            var cfg = Stage.StillConfig();
            var m = Stage.Build(cfg, (fast, FT.V(-60f, 0f), 0f), (new BallLoadout(new HarmlessRule()), FT.V(60f, 60f), 0f));
            m.Balls[0].Vel = FT.V(0f, 50f);
            m.Step();
            Assert.That(m.Balls[0].Vel.Length.F(), Is.EqualTo((cfg.MaxSpeed * FT.X(1.2f)).F()).Within(1e-3f));
        }

        [Test]
        public void StepAllocatesNothing()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue, Layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(0f, 0f), FT.X(14f), FT.X(1f), FT.X(0.1f)) }, FT.X(0.5f)) };
            var m = new MatchSim(cfg, 3, new[]
            {
                new BallLoadout(new ShooterRule(10, 0.01f)) { MaxHp = FT.X(30000f) }.With(new PoisonOnHitTrait()),
                new BallLoadout(new FixedBladeRule(0.01f)) { MaxHp = FT.X(30000f) }.With(new ReflectTrait(0.1f)),
                new BallLoadout(new FixedBodyRule(0.01f)) { MaxHp = FT.X(30000f) }
            });
            m.AddShield(m.Balls[1], 1000);
            m.ApplyRegen(m.Balls[2], FT.X(1f), int.MaxValue);
            for (var i = 0; i < 600; i++) m.Step(); // warm up: list capacities, JIT

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) m.Step();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Ongoing), "match must stay alive for the whole measurement");
            Assert.That(allocated, Is.EqualTo(0));
        }
    }
}
