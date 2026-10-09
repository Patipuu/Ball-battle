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
            var l = new BallLoadout(new HarmlessRule()) { Hp = 0f };
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
                new BallLoadout(new FixedBladeRule()) { Radius = 24f },
                new BallLoadout(new FixedBladeRule())
            });
            float Inner(BallState s) => (s.BladeStart - s.Pos).Length - s.Radius;
            Assert.That(Inner(m.Balls[0]), Is.EqualTo(Inner(m.Balls[1])).Within(1e-3f));
        }

        [Test]
        public void BossSizedBallWithCarriedHpSpawnsCorrectly()
        {
            var boss = new BallLoadout(new HarmlessRule()) { Radius = 24f, MaxHp = 250f, Hp = 100f };
            var m = new MatchSim(new MatchConfig(), 1, new[] { boss, new BallLoadout(new HarmlessRule()) });
            var b = m.Balls[0];

            Assert.That(b.Radius, Is.EqualTo(24f));
            Assert.That(b.MaxHp, Is.EqualTo(250f));
            Assert.That(b.Hp, Is.EqualTo(100f));
            Assert.That(b.HpFraction, Is.EqualTo(0.4f).Within(1e-6f));
            Assert.That(m.Balls[1].Radius, Is.EqualTo(16f), "default size for the other ball");
        }

        [Test]
        public void CarriedHpIsClampedToMaxHp()
        {
            var l = new BallLoadout(new HarmlessRule()) { MaxHp = 80f, Hp = 120f };
            var m = new MatchSim(new MatchConfig(), 1, new[] { l, new BallLoadout(new HarmlessRule()) });
            Assert.That(m.Balls[0].Hp, Is.EqualTo(80f));
        }

        [Test]
        public void BigBallStaysInsideAndNeverStaysOverlapped()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue };
            var m = new MatchSim(cfg, 8, new[]
            {
                new BallLoadout(new HarmlessRule()) { Radius = 24f },
                new BallLoadout(new HarmlessRule())
            });
            var overlapRun = 0;
            for (var t = 0; t < 200_000; t++)
            {
                m.Step();
                var a = m.Arena;
                foreach (var b in m.Balls)
                    if (b.Pos.X - b.Radius < a.Left - 1e-3f || b.Pos.X + b.Radius > a.Right + 1e-3f ||
                        b.Pos.Y - b.Radius < a.Bottom - 1e-3f || b.Pos.Y + b.Radius > a.Top + 1e-3f)
                        Assert.Fail($"Ball {b.Index} outside arena {a} at tick {m.Tick}");

                // Walls resolve last, so a wall can leave the pair overlapping for a substep (Step 1 does this
                // too: up to ~3 px with two r16 balls). It must never persist.
                var overlap = 40f - (m.Balls[0].Pos - m.Balls[1].Pos).Length;
                if (overlap > 5f) Assert.Fail($"Balls overlap by {overlap} at tick {m.Tick}");
                overlapRun = overlap > 0.5f ? overlapRun + 1 : 0;
                if (overlapRun > 3) Assert.Fail($"Balls stayed overlapped for {overlapRun} ticks at tick {m.Tick}");
            }
        }

        [Test]
        public void HeavierBallIsPushedLess()
        {
            var m = Stage.Build(null,
                (new BallLoadout(new HarmlessRule()) { Radius = 24f }, new Vec2(-40f, 0f), 0f),
                (new BallLoadout(new HarmlessRule()), new Vec2(40f, 0f), 0f));
            m.Balls[0].Vel = new Vec2(4f, 0f);
            m.Balls[1].Vel = new Vec2(-4f, 0f);
            for (var i = 0; i < 12; i++) m.Step();

            Assert.That(m.Balls[0].Vel.X, Is.LessThan(0f));
            Assert.That(MathF.Abs(m.Balls[0].Vel.X), Is.LessThan(MathF.Abs(m.Balls[1].Vel.X)), "big ball rebounds slower");
            var momentum = m.Balls[0].Vel.X * 24f * 24f + m.Balls[1].Vel.X * 16f * 16f;
            Assert.That(momentum, Is.EqualTo(4f * 24f * 24f - 4f * 16f * 16f).Within(1e-2f), "momentum conserved");
        }

        [Test]
        public void SpeedBonusRaisesCap()
        {
            var fast = new BallLoadout(new HarmlessRule()) { Bonus = new StatBonus { SpeedPct = 0.2f } };
            var cfg = Stage.StillConfig();
            var m = Stage.Build(cfg, (fast, new Vec2(-60f, 0f), 0f), (new BallLoadout(new HarmlessRule()), new Vec2(60f, 60f), 0f));
            m.Balls[0].Vel = new Vec2(0f, 50f);
            m.Step();
            Assert.That(m.Balls[0].Vel.Length, Is.EqualTo(cfg.MaxSpeed * 1.2f).Within(1e-3f));
        }

        [Test]
        public void StepAllocatesNothing()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue, Layout = new ArenaLayout(new[] { Obstacle.Circle(new Vec2(0f, 0f), 14f, 1f, 0.1f) }, 0.5f) };
            var m = new MatchSim(cfg, 3, new[]
            {
                new BallLoadout(new ShooterRule(10, 0.01f)) { MaxHp = 100000f }.With(new PoisonOnHitTrait()),
                new BallLoadout(new FixedBladeRule(0.01f)) { MaxHp = 100000f }.With(new ReflectTrait(0.1f)),
                new BallLoadout(new FixedBodyRule(0.01f)) { MaxHp = 100000f }
            });
            m.AddShield(m.Balls[1], 1000);
            m.ApplyRegen(m.Balls[2], 1f, int.MaxValue);
            for (var i = 0; i < 600; i++) m.Step(); // warm up: list capacities, JIT

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var i = 0; i < 10_000; i++) m.Step();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Ongoing), "match must stay alive for the whole measurement");
            Assert.That(allocated, Is.EqualTo(0));
        }
    }
}
