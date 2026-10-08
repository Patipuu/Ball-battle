using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class MatchSimTests
    {
        static MatchSim NewMatch(uint seed, WeaponRule a, WeaponRule b, MatchConfig cfg = null)
            => new MatchSim(cfg ?? new MatchConfig(), seed, new[] { a, b });

        /// <summary>Zero-gravity match with both balls parked at given spots, for staged contact tests.</summary>
        static MatchSim Staged(WeaponRule a, WeaponRule b, Vec2 posA, float angleA, Vec2 posB, float angleB)
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var m = NewMatch(1, a, b, cfg);
            m.Balls[0].Pos = posA; m.Balls[0].Vel = Vec2.Zero; m.Balls[0].WeaponAngleDeg = angleA; m.Balls[0].SpinDir = 1;
            m.Balls[1].Pos = posB; m.Balls[1].Vel = Vec2.Zero; m.Balls[1].WeaponAngleDeg = angleB; m.Balls[1].SpinDir = 1;
            return m;
        }

        static ulong RunAndHash(uint seed, int ticks)
        {
            var m = NewMatch(seed, new FixedBladeRule(1f), new FixedBladeRule(1f));
            for (var i = 0; i < ticks; i++) m.Step();
            return m.ComputeHash();
        }

        [Test]
        public void SameSeedGivesSameHash()
        {
            Assert.That(RunAndHash(1234, 9000), Is.EqualTo(RunAndHash(1234, 9000)));
        }

        [Test]
        public void DifferentSeedsGiveDifferentHashes()
        {
            var hashes = Enumerable.Range(1, 20).Select(s => RunAndHash((uint)s, 9000)).Distinct().Count();
            Assert.That(hashes, Is.EqualTo(20));
        }

        [Test]
        public void ArenaShrinksOnSchedule()
        {
            var c = new MatchConfig();
            Assert.That(c.ArenaAt(0).Width, Is.EqualTo(230f));
            Assert.That(c.ArenaAt(c.ShrinkStartTick).Width, Is.EqualTo(230f));
            Assert.That(c.ArenaAt(c.ShrinkStartTick + c.ShrinkDurationTicks / 2).Width, Is.EqualTo(170f).Within(0.01f));
            Assert.That(c.ArenaAt(c.ShrinkStartTick + c.ShrinkDurationTicks).Height, Is.EqualTo(110f));
            Assert.That(c.ArenaAt(c.CapTicks + 999).Width, Is.EqualTo(110f));
        }

        [Test]
        public void BallsNeverLeaveArenaOverOneMillionTicks()
        {
            var cfg = new MatchConfig { CapTicks = int.MaxValue };
            var m = NewMatch(7, new HarmlessRule(), new HarmlessRule(), cfg);
            m.Balls[0].Vel = new Vec2(cfg.MaxSpeed * 0.7f, cfg.MaxSpeed * 0.7f);
            m.Balls[1].Vel = new Vec2(-cfg.MaxSpeed * 0.7f, cfg.MaxSpeed * 0.7f);
            const float eps = 1e-3f;

            for (var t = 0; t < 1_000_000; t++)
            {
                m.Step();
                var a = m.Arena;
                foreach (var b in m.Balls)
                {
                    if (b.Pos.X - b.Radius < a.Left - eps || b.Pos.X + b.Radius > a.Right + eps ||
                        b.Pos.Y - b.Radius < a.Bottom - eps || b.Pos.Y + b.Radius > a.Top + eps)
                        Assert.Fail($"Ball {b.Index} outside arena {a} at tick {m.Tick}: pos {b.Pos}");
                }
            }

            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Ongoing));
            Assert.That(m.Arena.Width, Is.EqualTo(cfg.MinArenaWidth), "arena reached minimum size during the run");
        }

        [Test]
        public void BallsKeepBouncingAndMeet()
        {
            var m = NewMatch(3, new HarmlessRule(), new HarmlessRule());
            var ballBounces = 0;
            for (var t = 0; t < 60 * 60; t++)
            {
                m.Step();
                ballBounces += m.Events.Count(e => e.Type == SimEventType.BallBounce);
            }
            Assert.That(ballBounces, Is.GreaterThan(0), "balls should collide within a minute");
            foreach (var b in m.Balls) Assert.That(b.Vel.Length, Is.GreaterThan(0.4f));
        }

        [Test]
        public void ShrinkingWallDoesNotSpamWallEvents()
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f, ShrinkStartTick = 0, ShrinkDurationTicks = 600, CapTicks = int.MaxValue };
            var a = new HarmlessRule();
            var m = NewMatch(1, a, new HarmlessRule(), cfg);
            m.Balls[0].Pos = new Vec2(-100f, 0f); m.Balls[0].Vel = Vec2.Zero;
            m.Balls[1].Pos = new Vec2(100f, 0f); m.Balls[1].Vel = Vec2.Zero;
            for (var t = 0; t < 600; t++) m.Step();
            // Ball 0 is pushed by the left wall for hundreds of substeps, but it reflects off it only a few times.
            Assert.That(a.Walls, Is.LessThan(20));
        }

        [Test]
        public void CrossedBladesParry()
        {
            var a = new FixedBladeRule(5f, 25f, 0f);
            var b = new FixedBladeRule(5f, 25f, 0f);
            var m = Staged(a, b, new Vec2(-20, 0), 0f, new Vec2(20, 0), 180f);

            m.Step();

            Assert.That(m.Events.Any(e => e.Type == SimEventType.Parry), Is.True);
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Hit), Is.False, "parry consumes the contact");
            Assert.That(m.Balls[0].SpinDir, Is.EqualTo(-1));
            Assert.That(m.Balls[1].SpinDir, Is.EqualTo(-1));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f));
            Assert.That(m.HitstopRemaining, Is.EqualTo(m.Config.ParryHitstopTicks));
            Assert.That(a.Parries, Is.EqualTo(1));
            Assert.That(m.Balls[0].Vel.X, Is.LessThan(0f), "pushed apart");
            Assert.That(m.Balls[1].Vel.X, Is.GreaterThan(0f));
        }

        [Test]
        public void ParriedBladesDoNotStayLocked()
        {
            var m = Staged(new FixedBladeRule(0f), new FixedBladeRule(0f), new Vec2(-20, 0), 0f, new Vec2(20, 0), 180f);
            var lockedTicks = 0;
            for (var t = 0; t < 60; t++)
            {
                m.Step();
                if (m.HitstopRemaining > 0) continue;
                var a = m.Balls[0];
                var b = m.Balls[1];
                var d = Geometry.SegmentSegmentClosest(a.BladeStart, a.BladeEnd, b.BladeStart, b.BladeEnd, out _, out _);
                if (d <= 9f) lockedTicks++;
            }
            Assert.That(lockedTicks, Is.LessThanOrEqualTo(10));
        }

        [Test]
        public void BladeHitDamagesOncePerCooldownAndFreezesWorld()
        {
            var a = new FixedBladeRule(7f, 25f, 0f);
            var m = Staged(a, new FixedBladeRule(7f, 25f, 0f), new Vec2(0, 0), 0f, new Vec2(36, 0), 90f);

            m.Step();
            Assert.That(m.Balls[1].Hp, Is.EqualTo(93f));
            Assert.That(m.Balls[0].HitCount, Is.EqualTo(1));
            var hit = m.Events.Single(e => e.Type == SimEventType.Hit);
            Assert.That(hit.A, Is.EqualTo(0));
            Assert.That(hit.B, Is.EqualTo(1));
            Assert.That(hit.Value, Is.EqualTo(7f));
            Assert.That(m.Events.Any(e => e.Type == SimEventType.StatChanged && e.A == 0), Is.True);
            Assert.That(m.HitstopRemaining, Is.EqualTo(m.Config.HitHitstopTicks));

            // Hitstop: nothing moves.
            var frozen = m.Balls[1].Pos;
            for (var i = 0; i < m.Config.HitHitstopTicks; i++)
            {
                m.Step();
                Assert.That(m.Balls[1].Pos.X, Is.EqualTo(frozen.X));
            }
            m.Step();
            Assert.That(m.Balls[1].Pos.X, Is.GreaterThan(frozen.X), "knockback resumes after hitstop");

            var hits = 0;
            for (var i = 0; i < 30; i++) { m.Step(); hits += m.Events.Count(e => e.Type == SimEventType.Hit); }
            Assert.That(m.Balls[1].Hp, Is.EqualTo(93f - 7f * hits));
            Assert.That(hits, Is.LessThanOrEqualTo(1), "cooldown blocks a hit every substep");
        }

        [Test]
        public void BodyAttackerHitsOnContact()
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var m = NewMatch(1, new FixedBodyRule(10f), new HarmlessRule(), cfg);
            m.Balls[0].Pos = new Vec2(0, 0); m.Balls[0].Vel = new Vec2(5, 0);
            m.Balls[1].Pos = new Vec2(34, 0); m.Balls[1].Vel = Vec2.Zero;

            m.Step();

            Assert.That(m.Balls[1].Hp, Is.EqualTo(90f));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f), "harmless body deals nothing back");
        }

        [Test]
        public void KillEndsMatchWithWinner()
        {
            var m = Staged(new FixedBladeRule(7f, 25f, 0f), new FixedBladeRule(7f, 25f, 0f), new Vec2(0, 0), 0f, new Vec2(36, 0), 90f);
            m.Balls[1].Hp = 5f;

            m.Step();

            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Win));
            Assert.That(m.WinnerIndex, Is.EqualTo(0));
            Assert.That(m.EndReason, Is.EqualTo(MatchEndReason.Knockout));
            Assert.That(m.Balls[1].Alive, Is.False);
            Assert.That(m.Balls[1].Hp, Is.EqualTo(0f));
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Death && e.A == 1 && e.B == 0), Is.True);
            Assert.That(m.Events.Any(e => e.Type == SimEventType.MatchEnd && e.A == 0), Is.True);

            var tick = m.Tick;
            m.Step();
            Assert.That(m.Tick, Is.EqualTo(tick), "finished match does not advance");
            Assert.That(m.Events, Is.Empty);
        }

        [Test]
        public void TimeCapWithEqualHpIsDraw()
        {
            var m = NewMatch(5, new HarmlessRule(), new HarmlessRule(), new MatchConfig { CapTicks = 120 });
            while (m.Outcome == MatchOutcome.Ongoing) m.Step();
            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Draw));
            Assert.That(m.EndReason, Is.EqualTo(MatchEndReason.TimeCap));
            Assert.That(m.ActiveTick, Is.EqualTo(120));
            Assert.That(m.WinnerIndex, Is.EqualTo(-1));
        }

        [Test]
        public void TimeCapGoesToHigherHpPercent()
        {
            var m = NewMatch(5, new HarmlessRule(), new HarmlessRule(), new MatchConfig { CapTicks = 120 });
            m.Balls[1].Hp = 50f;
            while (m.Outcome == MatchOutcome.Ongoing) m.Step();
            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Win));
            Assert.That(m.WinnerIndex, Is.EqualTo(0));
            Assert.That(m.EndReason, Is.EqualTo(MatchEndReason.TimeCap));
        }

        [Test]
        public void ScalingBladeMatchesEndByKnockoutInTargetTime()
        {
            var finished = 0;
            var seconds = new System.Collections.Generic.List<float>();
            for (uint seed = 1; seed <= 100; seed++)
            {
                var m = NewMatch(seed, new ScalingBladeRule(), new ScalingBladeRule());
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                if (m.EndReason == MatchEndReason.Knockout) finished++;
                seconds.Add(m.ActiveTick / 60f);
            }
            seconds.Sort();
            Assert.That(finished, Is.GreaterThanOrEqualTo(99), "almost every match ends by knockout, not the time cap");
            Assert.That(seconds[50], Is.InRange(20f, 90f), "median match length");
        }

        [Test]
        public void WeaponInstanceCannotBeShared()
        {
            var w = new FixedBladeRule();
            Assert.Throws<System.InvalidOperationException>(() => NewMatch(1, w, w), "same instance on two balls");

            var a = new FixedBladeRule();
            var b = new FixedBladeRule();
            NewMatch(1, a, b);
            Assert.Throws<System.InvalidOperationException>(() => NewMatch(2, a, new FixedBladeRule()), "reused across matches");
        }

        [Test]
        public void SubclassStateIsHashed()
        {
            var m1 = NewMatch(9, new ScalingBladeRule(), new ScalingBladeRule());
            var m2 = NewMatch(9, new ScalingBladeRule(), new ScalingBladeRule());
            Assert.That(m1.ComputeHash(), Is.EqualTo(m2.ComputeHash()));
            m2.Balls[0].Weapon.OnHit(default);
            Assert.That(m1.ComputeHash(), Is.Not.EqualTo(m2.ComputeHash()), "scaling damage must change the hash");
        }

        /// <summary>A's blade along +x into B; B's blade angled down-left into A; blades never touch.</summary>
        static MatchSim TradeSetup(float hp)
        {
            var m = Staged(new FixedBladeRule(7f, 25f, 0f), new FixedBladeRule(7f, 25f, 0f), new Vec2(0, 0), 0f, new Vec2(34, 0), 200f);
            m.Balls[0].Hp = hp;
            m.Balls[1].Hp = hp;
            return m;
        }

        [Test]
        public void SimultaneousHitsBothLand()
        {
            var m = TradeSetup(100f);
            m.Step();
            Assert.That(m.Events.Count(e => e.Type == SimEventType.Hit), Is.EqualTo(2));
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Parry), Is.False);
            Assert.That(m.Balls[0].Hp, Is.EqualTo(93f));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(93f));
        }

        [Test]
        public void MutualKillIsDraw()
        {
            var m = TradeSetup(5f);
            m.Step();
            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Draw));
            Assert.That(m.EndReason, Is.EqualTo(MatchEndReason.Knockout));
            Assert.That(m.Events.Count(e => e.Type == SimEventType.Death), Is.EqualTo(2));
        }

        [Test]
        public void SpeedCapHoldsAfterHorizontalFloor()
        {
            var m = NewMatch(4, new HarmlessRule(), new HarmlessRule(), new MatchConfig { Gravity = 0f });
            m.Balls[0].Vel = new Vec2(0f, 50f);
            m.Step();
            Assert.That(m.Balls[0].Vel.Length, Is.LessThanOrEqualTo(m.Config.MaxSpeed + 1e-4f));
            Assert.That(System.Math.Abs(m.Balls[0].Vel.X), Is.GreaterThan(0f));
        }

        [Test]
        public void FastLongBladesStillParry()
        {
            // Two long, fast blades sweeping toward each other: substeps must be fine enough to catch the contact.
            // A points just left of straight up (100 deg) and turns clockwise toward B; B points just right of up (80 deg)
            // and turns counter-clockwise toward A. Tips move ~43 px/tick against a 3 px parry reach.
            var m = FastBladesSetup(maxSubsteps: 32);
            var parried = false;
            for (var t = 0; t < 10 && !parried; t++)
            {
                m.Step();
                parried = m.Events.Any(e => e.Type == SimEventType.Parry);
            }
            Assert.That(parried, Is.True);
        }

        static MatchSim FastBladesSetup(int maxSubsteps)
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f, MaxSubsteps = maxSubsteps };
            var m = NewMatch(1, new FixedBladeRule(1f, 70f, 30f), new FixedBladeRule(1f, 70f, 30f), cfg);
            m.Balls[0].Pos = new Vec2(-45, 0); m.Balls[0].Vel = Vec2.Zero; m.Balls[0].WeaponAngleDeg = 100f; m.Balls[0].SpinDir = -1;
            m.Balls[1].Pos = new Vec2(45, 0); m.Balls[1].Vel = Vec2.Zero; m.Balls[1].WeaponAngleDeg = 80f; m.Balls[1].SpinDir = 1;
            return m;
        }
    }
}