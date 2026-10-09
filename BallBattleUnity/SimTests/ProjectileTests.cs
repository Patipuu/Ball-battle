using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class ProjectileTests
    {
        static readonly ProjectileSpec Shot = new ProjectileSpec { Radius = 2f, Damage = 7f };

        static MatchSim Idle(WeaponRule b = null)
            => Stage.Build(null,
                (Stage.L(new HarmlessRule()), new Vec2(-60f, 0f), 0f),
                (Stage.L(b ?? new HarmlessRule()), new Vec2(60f, 0f), 180f));

        static int StepCounting(MatchSim m, int ticks, SimEventType type)
        {
            var n = 0;
            for (var i = 0; i < ticks; i++)
            {
                m.Step();
                n += m.Events.Count(e => e.Type == type);
            }
            return n;
        }

        [Test]
        public void ShotHitsBallOnceAndIsRemoved()
        {
            var m = Idle();
            Assert.That(m.FireProjectile(m.Balls[0], new Vec2(-40f, 0f), new Vec2(6f, 0f), Shot), Is.EqualTo(0));
            var hits = StepCounting(m, 60, SimEventType.ProjectileHit);

            Assert.That(hits, Is.EqualTo(1));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(93f));
            Assert.That(m.Balls[1].LastAttacker, Is.EqualTo(0));
            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void BladeDeflectsShotBackAtShooter()
        {
            var m = Idle(new FixedBladeRule(5f, 25f, 0f)); // blade points at the shooter (angle 180)
            m.FireProjectile(m.Balls[0], new Vec2(-40f, 0f), new Vec2(6f, 0f), Shot);
            var deflects = StepCounting(m, 80, SimEventType.ProjectileDeflected);

            Assert.That(deflects, Is.EqualTo(1));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f), "blade protected its ball");
            Assert.That(m.Balls[0].Hp, Is.EqualTo(93f), "deflected shot now belongs to the deflector");
            Assert.That(m.Balls[0].LastAttacker, Is.EqualTo(1));
        }

        [Test]
        public void ShotBouncesWhileBouncesLastThenIsRemoved()
        {
            var m = Idle();
            var spec = Shot;
            spec.Bounces = 1;
            m.FireProjectile(m.Balls[0], new Vec2(-60f, 25f), new Vec2(0f, 6f), spec); // up, back down through its owner
            for (var i = 0; i < 100; i++) m.Step();

            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(0));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f), "owner is never hit by its own shot");
        }

        [Test]
        public void PiercingShotHitsEachBallOnce()
        {
            var m = Stage.Build(null,
                (Stage.L(new HarmlessRule()), new Vec2(-90f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(0f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(80f, 0f), 0f));
            var spec = Shot;
            spec.Hits = 2;
            m.FireProjectile(m.Balls[0], new Vec2(-70f, 0f), new Vec2(6f, 0f), spec);
            var hits = StepCounting(m, 60, SimEventType.ProjectileHit);

            Assert.That(hits, Is.EqualTo(2));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(93f));
            Assert.That(m.Balls[2].Hp, Is.EqualTo(93f));
            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void ShotsVanishWhenOwnerDies()
        {
            var m = Stage.Build(null,
                (Stage.L(new HarmlessRule()), new Vec2(-80f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(0f, 80f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(80f, 0f), 0f));
            m.FireProjectile(m.Balls[0], new Vec2(-60f, 0f), new Vec2(1f, 0f), Shot);
            m.DealDamage(null, m.Balls[0], 1000f, DamageKind.Hazard);
            Assert.That(m.FireProjectile(m.Balls[0], new Vec2(-60f, 0f), new Vec2(1f, 0f), Shot), Is.EqualTo(-1), "a pending-dead ball cannot fire");
            m.Step();

            Assert.That(m.Balls[0].Alive, Is.False);
            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Ongoing));
            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(0));
        }

        [Test]
        public void FastShotDoesNotTunnelWithoutBladesOrObstacles()
        {
            var m = Idle();
            var spec = Shot;
            spec.Radius = 1f;
            m.FireProjectile(m.Balls[0], new Vec2(-40f, 0f), new Vec2(150f, 0f), spec); // 150 px per tick vs a 34 px reach
            var hits = StepCounting(m, 5, SimEventType.ProjectileHit);
            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void FullPoolDropsShot()
        {
            var m = Idle();
            for (var i = 0; i < ProjectilePool.Capacity; i++)
                Assert.That(m.FireProjectile(m.Balls[0], new Vec2(-60f, 90f), new Vec2(0f, 1f), Shot), Is.EqualTo(i));
            Assert.That(m.FireProjectile(m.Balls[0], new Vec2(-60f, 90f), new Vec2(0f, 1f), Shot), Is.EqualTo(-1));
        }

        [Test]
        public void ShooterMatchesAreDeterministic()
        {
            ulong Run(uint seed)
            {
                var m = new MatchSim(new MatchConfig(), seed, new WeaponRule[] { new ShooterRule(), new FixedBladeRule(1f) });
                for (var i = 0; i < 5000; i++) m.Step();
                return m.ComputeHash();
            }

            Assert.That(Run(5), Is.EqualTo(Run(5)));
            Assert.That(Run(5), Is.Not.EqualTo(Run(6)));
        }

        [Test]
        public void ShooterActuallyLandsAndGetsDeflected()
        {
            int hits = 0, deflects = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var m = new MatchSim(new MatchConfig(), seed, new WeaponRule[] { new ShooterRule(), new FixedBladeRule(1f) });
                for (var i = 0; i < 3000 && m.Outcome == MatchOutcome.Ongoing; i++)
                {
                    m.Step();
                    hits += m.Events.Count(e => e.Type == SimEventType.ProjectileHit);
                    deflects += m.Events.Count(e => e.Type == SimEventType.ProjectileDeflected);
                }
            }

            Assert.That(hits, Is.GreaterThan(0));
            Assert.That(deflects, Is.GreaterThan(0));
        }
    }
}
