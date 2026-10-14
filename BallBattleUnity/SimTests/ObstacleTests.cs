using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class ObstacleTests
    {
        static ArenaLayout PillarAndBar(float damage = 0f, float boost = 0f) => new ArenaLayout(new[]
        {
            Obstacle.Circle(FT.V(0f, 0f), FT.X(14f), FT.X(damage), FT.X(boost)),
            Obstacle.Segment(FT.V(-50f, -70f), FT.V(50f, -70f), FT.X(2f), FT.X(damage), FT.X(boost))
        });

        static float Penetration(BallState b, Obstacle o)
        {
            var d = (b.Pos - o.ClosestCore(b.Pos)).Length;
            return (b.Radius + o.Radius - d).F();
        }

        [Test]
        public void BallsNeverPenetrateObstaclesOverOneMillionTicks()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue, ShrinkStartTick = int.MaxValue, Layout = PillarAndBar() };
            var m = new MatchSim(cfg, 11, new WeaponRule[] { new HarmlessRule(), new HarmlessRule() });
            m.Balls[0].Vel = FT.V(cfg.MaxSpeed * FT.X(0.7f), cfg.MaxSpeed * FT.X(0.7f));
            m.Balls[1].Vel = FT.V(-cfg.MaxSpeed * FT.X(0.7f), cfg.MaxSpeed * FT.X(0.7f));
            var obstacleHits = 0;

            for (var t = 0; t < 1_000_000; t++)
            {
                m.Step();
                foreach (var e in m.Events) if (e.Type == SimEventType.ObstacleHit) obstacleHits++;
                foreach (var b in m.Balls)
                    for (var k = 0; k < cfg.Layout.ObstacleCount; k++)
                        if (Penetration(b, cfg.Layout.Get(k)) > 1e-3f)
                            Assert.Fail($"Ball {b.Index} inside obstacle {k} at tick {m.Tick}: pos {b.Pos}");
            }

            Assert.That(obstacleHits, Is.GreaterThan(1000), "balls really bounced off the obstacles");
        }

        [Test]
        public void ShrinkingArenaKeepsBallsInsideWallsWithObstacles()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue, Layout = PillarAndBar() };
            var m = new MatchSim(cfg, 3, new WeaponRule[] { new HarmlessRule(), new HarmlessRule() });
            for (var t = 0; t < 20_000; t++)
            {
                m.Step();
                var a = m.Arena;
                foreach (var b in m.Balls)
                    if (b.Pos.X - b.Radius < a.Left - FT.X(1e-3f) || b.Pos.X + b.Radius > a.Right + FT.X(1e-3f) ||
                        b.Pos.Y - b.Radius < a.Bottom - FT.X(1e-3f) || b.Pos.Y + b.Radius > a.Top + FT.X(1e-3f))
                        Assert.Fail($"Ball {b.Index} outside arena {a} at tick {m.Tick}");
            }
        }

        [Test]
        public void SpikedObstacleDamagesOnBounce()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(0f, 0f), FT.X(14f), damage: FT.X(2f)) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(-60f, 0f), 0f), (Stage.L(new HarmlessRule()), FT.V(80f, 80f), 0f));
            m.Balls[0].Vel = FT.V(5f, 0f);
            var hit = Enumerable.Range(0, 30).SelectMany(_ => { m.Step(); return m.Events.ToArray(); })
                .First(e => e.Type == SimEventType.ObstacleHit);

            Assert.That(hit.A, Is.EqualTo(0));
            Assert.That(hit.Value.F(), Is.EqualTo(2f));
            Assert.That(m.Balls[0].Hp.F(), Is.EqualTo(98f));
            Assert.That(m.Balls[0].Vel.X.F(), Is.LessThan(0f), "bounced back");
        }

        [Test]
        public void BumperBoostsSpeed()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(0f, 0f), FT.X(10f), boost: FT.X(0.5f)) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(-60f, 0f), 0f), (Stage.L(new HarmlessRule()), FT.V(80f, 80f), 0f));
            m.Balls[0].Vel = FT.V(4f, 0f);
            for (var i = 0; i < 15; i++) m.Step();
            Assert.That(m.Balls[0].Vel.X.F(), Is.EqualTo(-6f).Within(1e-3f));
        }

        [Test]
        public void SpikeWallsDamageOnlyInSpikeZones()
        {
            // BB-FIX1a: only the corner zones hurt (layout default corners: TL + BR). Mid-wall bounce is harmless now.
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(null, wallDamage: FT.X(2f));
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(80f, 0f), 0f), (Stage.L(new HarmlessRule()), FT.V(-80f, 80f), 0f));
            m.Balls[0].Vel = FT.V(5f, 0f);
            for (var i = 0; i < 10; i++) m.Step();
            Assert.That(m.Balls[0].Hp.F(), Is.EqualTo(100f), "right wall midpoint");

            var n = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(80f, -95f), 0f), (Stage.L(new HarmlessRule()), FT.V(-80f, 80f), 0f));
            n.Balls[0].Vel = FT.V(5f, 0f);
            for (var i = 0; i < 10; i++) n.Step();
            Assert.That(n.Balls[0].Hp.F(), Is.EqualTo(98f), "right wall inside the bottom-right zone");
        }

        [Test]
        public void ProjectileBouncesOffObstacle()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(0f, 0f), FT.X(14f)) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(-80f, 60f), 0f), (Stage.L(new HarmlessRule()), FT.V(80f, 60f), 0f));
            m.FireProjectile(m.Balls[0], FT.V(-40f, 0f), FT.V(5f, 0f), new ProjectileSpec { Radius = FT.X(2f), Damage = FT.X(1f), Bounces = 1 });
            for (var i = 0; i < 8; i++) m.Step();
            var p = m.Projectiles.Get(0);
            Assert.That(p.Active, Is.True);
            Assert.That(p.Vel.X.F(), Is.LessThan(0f));
            Assert.That(p.BouncesLeft, Is.EqualTo(0));
        }

        [Test]
        public void LayoutIsHashed()
        {
            var plain = new MatchSim(new MatchConfig(), 1, new WeaponRule[] { new HarmlessRule(), new HarmlessRule() });
            var withPillar = new MatchSim(new MatchConfig { Layout = PillarAndBar() }, 1, new WeaponRule[] { new HarmlessRule(), new HarmlessRule() });
            Assert.That(plain.ComputeHash(), Is.Not.EqualTo(withPillar.ComputeHash()));
        }
    }
}
