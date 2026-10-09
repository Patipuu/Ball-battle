using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class StatusTests
    {
        static readonly Vec2 PosA = new Vec2(-60f, 0f);
        static readonly Vec2 PosB = new Vec2(60f, 0f);

        static MatchSim TwoIdleBalls(float hpB = -1f)
        {
            var b = Stage.L(new HarmlessRule());
            b.Hp = hpB;
            return Stage.Build(null, (Stage.L(new HarmlessRule()), PosA, 0f), (b, PosB, 0f));
        }

        [Test]
        public void PoisonStacksAndPulsesOncePerSecond()
        {
            var m = TwoIdleBalls();
            var target = m.Balls[1];
            m.ApplyPoison(target, m.Balls[0], 1f, 180);
            m.ApplyPoison(target, m.Balls[0], 1f, 180);
            Assert.That(target.Status.PoisonStackCount, Is.EqualTo(2));

            var pulses = 0;
            for (var i = 0; i < 400; i++)
            {
                m.Step();
                pulses += m.Events.Count(e => e.Type == SimEventType.StatusTick);
            }

            Assert.That(pulses, Is.EqualTo(3), "3 s of poison = 3 pulses");
            Assert.That(target.Hp, Is.EqualTo(100f - 2f * 3f).Within(1e-4f));
            Assert.That(target.Status.PoisonStackCount, Is.EqualTo(0));
        }

        [Test]
        public void PoisonAmountDoesNotDependOnWhenItLands()
        {
            var m = TwoIdleBalls();
            var target = m.Balls[1];
            m.ApplyPoison(target, m.Balls[0], 1f, 120);
            for (var i = 0; i < 37; i++) m.Step();
            m.ApplyPoison(target, m.Balls[0], 1f, 120); // lands mid-second
            m.ApplyPoison(target, m.Balls[0], 1f, 59);  // rounded up to 1 s: pulses once, together with the stack above

            var pulses = 0;
            for (var i = 0; i < 300; i++)
            {
                m.Step();
                pulses += m.Events.Count(e => e.Type == SimEventType.StatusTick);
            }

            Assert.That(target.Hp, Is.EqualTo(100f - 5f).Within(1e-4f), "2 s stacks pulse exactly twice, the 1 s stack once");
            Assert.That(pulses, Is.EqualTo(4), "stacks pulsing on the same tick share one event");
        }

        [Test]
        public void HealNeverRevivesPendingDeadOrLowersHp()
        {
            var m = TwoIdleBalls();
            var b = m.Balls[1];
            b.Hp = 0f; // pending death (still Alive until the sim resolves deaths)
            Assert.That(m.Heal(b, 50f), Is.EqualTo(0f));
            b.Hp = 100f;
            b.MaxHp = 80f; // e.g. a trait lowered MaxHp
            Assert.That(m.Heal(b, 10f), Is.EqualTo(0f));
            Assert.That(b.Hp, Is.EqualTo(100f), "heal never clamps HP down");
        }

        [Test]
        public void FullPoisonReplacesStackWithFewestTicksLeft()
        {
            var s = new StatusEffects();
            for (var i = 0; i < StatusEffects.MaxPoisonStacks; i++) s.AddPoison(1f, 100 + i, 0);
            s.AddPoison(5f, 500, 1);

            Assert.That(s.PoisonStackCount, Is.EqualTo(StatusEffects.MaxPoisonStacks));
            Assert.That(s.Poison[0].Dps, Is.EqualTo(5f), "slot 0 had the fewest ticks left");
            Assert.That(s.Poison[0].Source, Is.EqualTo(1));
        }

        [Test]
        public void PoisonKillCreditsSource()
        {
            var m = TwoIdleBalls(hpB: 1f);
            m.ApplyPoison(m.Balls[1], m.Balls[0], 3f, 120);
            for (var i = 0; i < 120 && m.Outcome == MatchOutcome.Ongoing; i++) m.Step();

            Assert.That(m.WinnerIndex, Is.EqualTo(0));
            Assert.That(m.Balls[1].LastAttacker, Is.EqualTo(0));
        }

        [Test]
        public void ShieldBlocksExactlyItsCharges()
        {
            var posA = new Vec2(0f, 0f);
            var posB = new Vec2(45f, 0f);
            var m = Stage.Build(null, (Stage.L(new FixedBladeRule(10f, 25f, 0f)), posA, 0f), (Stage.L(new HarmlessRule()), posB, 0f));
            m.AddShield(m.Balls[1], 2);

            int blocked = 0, hits = 0;
            for (var i = 0; i < 200 && hits == 0; i++)
            {
                m.Step();
                Stage.Park(m, posA, posB);
                blocked += m.Events.Count(e => e.Type == SimEventType.ShieldBlocked);
                hits += m.Events.Count(e => e.Type == SimEventType.Hit);
            }

            Assert.That(blocked, Is.EqualTo(2));
            Assert.That(hits, Is.EqualTo(1));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(90f));
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(0));
            Assert.That(m.Balls[0].HitCount, Is.EqualTo(1), "blocked hits do not count for weapon growth");
        }

        [Test]
        public void RegenHealsEverySecondUpToMaxHp()
        {
            var m = TwoIdleBalls(hpB: 95f);
            m.ApplyRegen(m.Balls[1], 2f, 300);
            var healed = 0f;
            for (var i = 0; i < 400; i++)
            {
                m.Step();
                healed += m.Events.Where(e => e.Type == SimEventType.Heal).Sum(e => e.Value);
            }

            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f));
            Assert.That(healed, Is.EqualTo(5f).Within(1e-4f), "never heals past MaxHp");
        }

        [Test]
        public void StatusChangesTheHash()
        {
            var a = TwoIdleBalls();
            var b = TwoIdleBalls();
            b.ApplyPoison(b.Balls[1], null, 1f, 60);
            Assert.That(a.ComputeHash(), Is.Not.EqualTo(b.ComputeHash()));
        }
    }
}
