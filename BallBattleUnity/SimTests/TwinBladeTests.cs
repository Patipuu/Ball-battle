using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class TwinBladeTests
    {
        [Test]
        public void SecondBladeHitsABallBehind([Values(1, 2)] int level)
        {
            MatchSim Build(bool twin) => Stage.Build(null,
                (twin ? Stage.L(new FixedBladeRule(10f, 25f, 0f), new TwinBladeTrait(level)) : Stage.L(new FixedBladeRule(10f, 25f, 0f)), new Vec2(0f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(-34f, 0f), 0f));
            var plain = Build(false);
            plain.Step();
            Assert.That(plain.Balls[1].Hp, Is.EqualTo(100f), "no twin, nothing behind is hit");
            var m = Build(true);
            m.Step();
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f - 10f * TraitTuning.TwinBladeDamageMul[level - 1]).Within(1e-3f), "twin hits are weaker");
            Assert.That(m.Balls[0].TwinBladeScale, Is.EqualTo(TraitTuning.TwinBladeScale[level - 1]));
        }

        [Test]
        public void SecondBladeParries()
        {
            var m = Stage.Build(null,
                (Stage.L(new FixedBladeRule(10f, 25f, 0f), new TwinBladeTrait(1)), new Vec2(0f, 0f), 0f),
                (Stage.L(new FixedBladeRule(10f, 25f, 0f)), new Vec2(-50f, 0f), 0f));
            m.Step();
            Assert.That(m.Events.Count(e => e.Type == SimEventType.Parry), Is.EqualTo(1));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f));
        }

        [Test]
        public void SecondBladeDeflectsShots()
        {
            var m = Stage.Build(null,
                (Stage.L(new ShooterRule(1, 5f, 0)), new Vec2(-100f, 0f), 0f),
                (Stage.L(new FixedBladeRule(1f, 25f, 0f), new TwinBladeTrait(2)), new Vec2(0f, 0f), 0f));
            var deflected = false;
            for (var i = 0; i < 60 && !deflected; i++)
            {
                m.Step();
                deflected = m.Events.Any(e => e.Type == SimEventType.ProjectileDeflected);
            }
            Assert.That(deflected, "a shot flying at the twin blade's side is knocked back");
        }
    }
}