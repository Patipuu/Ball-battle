using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class ObstacleTests
    {
        static ArenaLayout PillarAndBar(float damage = 0f, float boost = 0f) => new ArenaLayout(new[]
        {
            Obstacle.Circle(new Vec2(0f, 0f), 14f, damage, boost),
            Obstacle.Segment(new Vec2(-50f, -70f), new Vec2(50f, -70f), 2f, damage, boost)
        });

        static float Penetration(BallState b, Obstacle o)
        {
            var d = (b.Pos - o.ClosestCore(b.Pos)).Length;
            return b.Radius + o.Radius - d;
        }

        [Test]
        public void BallsNeverPenetrateObstaclesOverOneMillionTicks()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue, ShrinkStartTick = int.MaxValue, Layout = PillarAndBar() };
            var m = new MatchSim(cfg, 11, new WeaponRule[] { new HarmlessRule(), new HarmlessRule() });
            m.Balls[0].Vel = new Vec2(cfg.MaxSpeed * 0.7f, cfg.MaxSpeed * 0.7f);
            m.Balls[1].Vel = new Vec2(-cfg.MaxSpeed * 0.7f, cfg.MaxSpeed * 0.7f);
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
                    if (b.Pos.X - b.Radius < a.Left - 1e-3f || b.Pos.X + b.Radius > a.Right + 1e-3f ||
                        b.Pos.Y - b.Radius < a.Bottom - 1e-3f || b.Pos.Y + b.Radius > a.Top + 1e-3f)
                        Assert.Fail($"Ball {b.Index} outside arena {a} at tick {m.Tick}");
            }
        }

        [Test]
        public void SpikedObstacleDamagesOnBounce()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(new Vec2(0f, 0f), 14f, damage: 2f) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), new Vec2(-60f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(80f, 80f), 0f));
            m.Balls[0].Vel = new Vec2(5f, 0f);
            var hit = Enumerable.Range(0, 30).SelectMany(_ => { m.Step(); return m.Events.ToArray(); })
                .First(e => e.Type == SimEventType.ObstacleHit);

            Assert.That(hit.A, Is.EqualTo(0));
            Assert.That(hit.Value, Is.EqualTo(2f));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(98f));
            Assert.That(m.Balls[0].Vel.X, Is.LessThan(0f), "bounced back");
        }

        [Test]
        public void BumperBoostsSpeed()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(new Vec2(0f, 0f), 10f, boost: 0.5f) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), new Vec2(-60f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(80f, 80f), 0f));
            m.Balls[0].Vel = new Vec2(4f, 0f);
            for (var i = 0; i < 15; i++) m.Step();
            Assert.That(m.Balls[0].Vel.X, Is.EqualTo(-6f).Within(1e-3f));
        }

        [Test]
        public void SpikeWallsDamageOnWallBounce()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(null, wallDamage: 2f);
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), new Vec2(80f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(-80f, 80f), 0f));
            m.Balls[0].Vel = new Vec2(5f, 0f);
            for (var i = 0; i < 10; i++) m.Step();
            Assert.That(m.Balls[0].Hp, Is.EqualTo(98f));
        }

        [Test]
        public void ProjectileBouncesOffObstacle()
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = new ArenaLayout(new[] { Obstacle.Circle(new Vec2(0f, 0f), 14f) });
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), new Vec2(-80f, 60f), 0f), (Stage.L(new HarmlessRule()), new Vec2(80f, 60f), 0f));
            m.FireProjectile(m.Balls[0], new Vec2(-40f, 0f), new Vec2(5f, 0f), new ProjectileSpec { Radius = 2f, Damage = 1f, Bounces = 1 });
            for (var i = 0; i < 8; i++) m.Step();
            var p = m.Projectiles.Get(0);
            Assert.That(p.Active, Is.True);
            Assert.That(p.Vel.X, Is.LessThan(0f));
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
