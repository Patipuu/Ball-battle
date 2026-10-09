using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class TraitHookTests
    {
        static readonly Vec2 PosA = new Vec2(0f, 0f);
        static readonly Vec2 PosB = new Vec2(45f, 0f); // A's still blade (12..37 along +x) touches B

        static MatchSim BladeVsTarget(BallLoadout attacker, BallLoadout target)
            => Stage.Build(null, (attacker, PosA, 0f), (target, PosB, 0f));

        [Test]
        public void HooksRunInFixedOrder()
        {
            var log = new List<string>();
            var m = BladeVsTarget(
                Stage.L(new FixedBladeRule(5f, 25f, 0f), new RecordingTrait("a0", log), new RecordingTrait("a1", log)),
                Stage.L(new HarmlessRule(), new RecordingTrait("b0", log), new RecordingTrait("b1", log)));
            m.Step();

            Assert.That(log, Is.EqualTo(new[]
            {
                "a0:spawn", "a1:spawn", "b0:spawn", "b1:spawn",
                "a0:out", "a1:out", "b0:in", "b1:in",
                "a0:dealt", "a1:dealt", "b0:taken", "b1:taken"
            }));
        }

        [Test]
        public void PipelineAppliesBonusThenOutgoingThenIncoming()
        {
            var attacker = Stage.L(new FixedBladeRule(10f, 25f, 0f), new ScaleTrait(2f, 1f));
            attacker.Bonus = new StatBonus { DamagePct = 0.5f };
            var m = BladeVsTarget(attacker, Stage.L(new HarmlessRule(), new ScaleTrait(1f, 0.25f)));
            m.Step();

            var hit = m.Events.Single(e => e.Type == SimEventType.Hit);
            Assert.That(hit.Value, Is.EqualTo(10f * 1.5f * 2f * 0.25f).Within(1e-4f));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f - hit.Value).Within(1e-4f));
        }

        [Test]
        public void ReflectDamageCanKillAttackerWithoutLooping()
        {
            var attacker = Stage.L(new FixedBladeRule(10f, 25f, 0f), new ReflectTrait(1f));
            attacker.Hp = 5f;
            var m = BladeVsTarget(attacker, Stage.L(new HarmlessRule(), new ReflectTrait(1f)));
            m.Step();

            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Win));
            Assert.That(m.WinnerIndex, Is.EqualTo(1));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(90f));
            var death = m.Events.Single(e => e.Type == SimEventType.Death);
            Assert.That(death.A, Is.EqualTo(0));
            Assert.That(death.B, Is.EqualTo(1), "reflector is credited with the kill");
            Assert.That(m.Events.Count(e => e.Type == SimEventType.Damage), Is.EqualTo(1), "reflected damage is not reflected again");
        }

        [Test]
        public void MutualLethalTradeIsADrawWhateverTheOrder()
        {
            foreach (var flip in new[] { false, true })
            {
                var a = Stage.L(new FixedBodyRule(12f), new LifestealTrait());
                var b = Stage.L(new FixedBodyRule(12f), new LifestealTrait());
                a.Hp = 10f;
                b.Hp = 10f;
                var m = Stage.Build(null, (flip ? b : a, new Vec2(0f, 0f), 0f), (flip ? a : b, new Vec2(32.2f, 0f), 0f));
                m.Step();

                Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Draw), "lifesteal must not let the lower index survive");
                Assert.That(m.Events.Count(e => e.Type == SimEventType.Heal), Is.EqualTo(0));
            }
        }

        [Test]
        public void NonLethalLifestealTradeIsOrderIndependent()
        {
            float[] Run(bool flip)
            {
                var a = Stage.L(new FixedBodyRule(12f), new LifestealTrait());
                var b = Stage.L(new FixedBodyRule(5f), new LifestealTrait());
                a.Hp = 50f;
                b.Hp = 40f;
                var m = Stage.Build(null, (flip ? b : a, new Vec2(0f, 0f), 0f), (flip ? a : b, new Vec2(32.2f, 0f), 0f));
                m.Step();
                var ia = flip ? 1 : 0;
                return new[] { m.Balls[ia].Hp, m.Balls[1 - ia].Hp };
            }

            var straight = Run(false);
            Assert.That(Run(true), Is.EqualTo(straight));
            Assert.That(straight, Is.EqualTo(new[] { 50f - 5f + 12f, 40f - 12f + 5f }));
        }

        [Test]
        public void TraitCanPreventDeathOnce()
        {
            var m = BladeVsTarget(Stage.L(new FixedBladeRule(200f, 25f, 0f)), Stage.L(new HarmlessRule(), new LastStandTrait()));
            m.Step();
            Assert.That(m.Outcome, Is.EqualTo(MatchOutcome.Ongoing));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(25f));
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Heal && e.A == 1), Is.True);

            for (var i = 0; i < 100 && m.Outcome == MatchOutcome.Ongoing; i++)
            {
                m.Step();
                Stage.Park(m, PosA, PosB);
            }
            Assert.That(m.WinnerIndex, Is.EqualTo(0), "second lethal blow kills");
        }

        [Test]
        public void SpawnHookEventsReachFirstStep()
        {
            var m = BladeVsTarget(Stage.L(new HarmlessRule(), new StartShieldTrait()), Stage.L(new HarmlessRule()));
            Assert.That(m.Balls[0].Status.ShieldCharges, Is.EqualTo(0), "spawn hooks run on the first Step");
            m.Step();
            Assert.That(m.Balls[0].Status.ShieldCharges, Is.EqualTo(1));
            Assert.That(m.Events.Any(e => e.Type == SimEventType.StatusApplied && e.A == 0), Is.True);
        }

        [Test]
        public void TickHooksRunOncePerActiveTickOnly()
        {
            var log = new List<string>();
            var rec = new RecordingTrait("a", log);
            var m = BladeVsTarget(Stage.L(new FixedBladeRule(1f, 25f, 0f), rec), Stage.L(new HarmlessRule()));
            for (var i = 0; i < 100; i++) m.Step();

            Assert.That(rec.Ticks, Is.EqualTo(m.ActiveTick));
            Assert.That(m.ActiveTick, Is.LessThan(m.Tick), "hit hitstop froze some ticks");
        }

        [Test]
        public void TraitInstanceCannotBeShared()
        {
            var shared = new ScaleTrait(1f, 1f);
            Assert.Throws<InvalidOperationException>(() => new MatchSim(new MatchConfig(), 1, new[]
            {
                Stage.L(new FixedBladeRule(), shared),
                Stage.L(new FixedBladeRule(), shared)
            }));
        }

        [Test]
        public void TraitLevelMustBeOneOrTwo()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new LevelTrait(3));
        }

        [Test]
        public void MatchesWithTraitsAreDeterministicAndTraitStateIsHashed()
        {
            ulong Run(int extraTicks)
            {
                var log = new List<string>();
                var m = new MatchSim(new MatchConfig(), 99, new[]
                {
                    Stage.L(new FixedBladeRule(2f), new RecordingTrait("a", log), new PoisonOnHitTrait()),
                    Stage.L(new FixedBladeRule(2f), new ReflectTrait(0.25f))
                });
                for (var i = 0; i < 3000; i++) m.Step();
                ((RecordingTrait)m.Balls[0].Traits[0]).Ticks += extraTicks;
                return m.ComputeHash();
            }

            Assert.That(Run(0), Is.EqualTo(Run(0)));
            Assert.That(Run(1), Is.Not.EqualTo(Run(0)));
        }

        sealed class LevelTrait : TraitRule
        {
            public LevelTrait(int level) : base(level) { }
            public override string Id => "test-level";
            protected override ulong HashState(ulong h) => h;
        }
    }
}
