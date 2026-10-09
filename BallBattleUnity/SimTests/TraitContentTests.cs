using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class TraitContentTests
    {
        [Test]
        public void HeavyGrowsToughensAndSlows([Values(1, 2)] int level)
        {
            var m = Stage.Build(null,
                (Stage.L(new HarmlessRule(), new HeavyTrait(level)), new Vec2(-60f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(60f, 0f), 0f));
            m.Step();
            var b = m.Balls[0];
            var i = level - 1;
            Assert.That(b.Radius, Is.EqualTo(16f * TraitTuning.HeavyRadiusScale[i]).Within(1e-4f));
            Assert.That(b.BladeShift, Is.EqualTo(b.Radius - 16f).Within(1e-4f));
            Assert.That(b.MaxHp, Is.EqualTo(100f * (1f + TraitTuning.HeavyMaxHpPct[i])).Within(1e-3f));
            Assert.That(b.Hp, Is.EqualTo(b.MaxHp));
            Assert.That(b.Bonus.SpeedPct, Is.EqualTo(TraitTuning.HeavySpeedPct[i]));
            Assert.That(b.KnockbackResist, Is.EqualTo(TraitTuning.HeavyKnockbackResist[i]));
        }

        [Test]
        public void HeavyNeverGrowsABossPastTheCap()
        {
            var boss = new BallLoadout(new HarmlessRule()) { Radius = 24f }.With(new HeavyTrait(2));
            var m = Stage.Build(null, (boss, new Vec2(-60f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(60f, 0f), 0f));
            m.Step();
            Assert.That(m.Balls[0].Radius, Is.EqualTo(TraitTuning.HeavyMaxRadius[1]));
            Assert.That(m.Balls[0].BladeShift, Is.EqualTo(24f - 16f).Within(1e-4f));
        }

        [Test]
        public void FingerprintCoversEveryTuningArray()
        {
            var fields = typeof(TraitTuning).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                .Where(f => f.FieldType == typeof(float[])).ToList();
            Assert.That(fields.Count, Is.GreaterThan(0));
            var baseline = TraitTuning.Fingerprint();
            foreach (var f in fields)
            {
                var arr = (float[])f.GetValue(null);
                for (var i = 0; i < arr.Length; i++)
                {
                    var keep = arr[i];
                    arr[i] = keep + 1f;
                    try { Assert.That(TraitTuning.Fingerprint(), Is.Not.EqualTo(baseline), $"{f.Name}[{i}] is not in TraitTuning.Fingerprint"); }
                    finally { arr[i] = keep; }
                }
            }
        }

        [Test]
        public void HeavyIsPushedLessByHits()
        {
            float Push(bool heavy)
            {
                var target = heavy ? Stage.L(new HarmlessRule(), new HeavyTrait()) : Stage.L(new HarmlessRule());
                var m = Stage.Build(null, (Stage.L(new FixedBladeRule(1f, 40f, 0f)), new Vec2(0f, 0f), 0f), (target, new Vec2(60f, 0f), 0f));
                for (var i = 0; i < 3; i++) m.Step();
                return m.Balls[1].Vel.Length;
            }
            Assert.That(Push(true), Is.LessThan(Push(false) * 0.7f));
        }

        [Test]
        public void VampireHealsShareOfHitDamage()
        {
            var attacker = Stage.L(new FixedBladeRule(10f, 25f, 0f), new VampireTrait());
            attacker.Hp = 50f;
            var m = Stage.Build(null, (attacker, new Vec2(0f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(45f, 0f), 0f));
            m.Step();
            Assert.That(m.Balls[0].Hp, Is.EqualTo(50f + 10f * TraitTuning.VampireHealPct[0]).Within(1e-4f));
        }

        [Test]
        public void SpikyHurtsOnBodyBounce()
        {
            var m = Stage.Build(null,
                (Stage.L(new HarmlessRule(), new SpikyTrait(2)), new Vec2(-40f, 0f), 0f),
                (Stage.L(new HarmlessRule()), new Vec2(40f, 0f), 0f));
            m.Balls[0].Vel = new Vec2(3f, 0f);
            m.Balls[1].Vel = new Vec2(-3f, 0f);
            var damage = 0f;
            for (var i = 0; i < 20; i++)
            {
                m.Step();
                damage += m.Events.Where(e => e.Type == SimEventType.Damage && e.B == 1).Sum(e => e.Value);
            }
            Assert.That(damage, Is.EqualTo(TraitTuning.SpikyContactDamage[1]));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f), "spikes do not hurt their owner");
        }

        [Test]
        public void EveryRegisteredTraitBuildsAtBothLevelsAndHashesItsId()
        {
            var hashes = TraitRegistry.All.Select(e => e.Create(1).HashInto(0UL)).ToList();
            Assert.That(hashes.Distinct().Count(), Is.EqualTo(hashes.Count), "stateless traits still hash differently (id is mixed in)");
            foreach (var e in TraitRegistry.All)
                for (var level = 1; level <= 2; level++)
                {
                    var t = e.Create(level);
                    Assert.That(t.Id, Is.EqualTo(e.Id));
                    Assert.That(t.Level, Is.EqualTo(level));
                }
            Assert.That(TraitRegistry.All.Select(e => e.Id).Distinct().Count(), Is.EqualTo(TraitRegistry.All.Count));
        }
    }
}
