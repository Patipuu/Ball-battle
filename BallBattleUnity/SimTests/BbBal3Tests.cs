using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-BAL3 (Rules 13): the Run DAMAGE card (StatBonus.DamagePct) also scales SPORE's own poison and MIRROR's reflects.
    /// With no card (DamagePct 0, every Versus match) the numbers are exactly the old ones.</summary>
    public class BbBal3Tests
    {
        static MatchSim Match(string a, string b, Fix pct)
        {
            var cfg = new MatchConfig { Gravity = Fix.Zero, MinHorizontalSpeed = Fix.Zero, ShrinkStartTick = int.MaxValue, CapTicks = int.MaxValue };
            return new MatchSim(cfg, 3, new[] {
                new BallLoadout(WeaponRegistry.Create(a)) { MaxHp = 9999, Bonus = new StatBonus { DamagePct = pct } },
                new BallLoadout(WeaponRegistry.Create(b)) { MaxHp = 9999 } });
        }

        static Fix Poison(Fix pct)
        {
            var m = Match(VenomRule.WeaponId, BrawlerRule.WeaponId, pct);
            m.Balls[0].Weapon.OnHitDealt(m.Balls[1], Fix.One, DamageKind.Weapon);
            Assert.That(m.Balls[1].Status.PoisonStackCount, Is.EqualTo(1));
            for (var i = 0; i < StatusEffects.MaxPoisonStacks; i++) if (m.Balls[1].Status.Poison[i].TicksLeft > 0) return m.Balls[1].Status.Poison[i].Dps;
            return Fix.Zero;
        }

        [Test]
        public void SporePoisonScalesWithTheDamageCard()
        {
            Assert.That(Poison(Fix.Zero), Is.EqualTo(WeaponTuning.VenomDps), "no card: unchanged");
            var pct = Fix.Ratio(3, 10);   // two DAMAGE cards
            Assert.That(Poison(pct), Is.EqualTo(WeaponTuning.VenomDps * (Fix.One + pct)));
        }

        static Fix BodyReflect(Fix pct)
        {
            var m = Match(AegisRule.WeaponId, BrawlerRule.WeaponId, pct);
            var before = m.Balls[1].Hp;
            m.Balls[0].Weapon.OnHitTaken(m.Balls[1], Fix.FromInt(10), DamageKind.Weapon);   // a body blow: part of it is sent back
            return before - m.Balls[1].Hp;
        }

        static Fix ParryReflect(Fix pct)
        {
            var m = Match(AegisRule.WeaponId, BladeRule.WeaponId, pct);
            var before = m.Balls[1].Hp;
            m.Balls[0].Weapon.OnParry(m.Balls[1]);
            return before - m.Balls[1].Hp;
        }

        [Test]
        public void MirrorReflectsScaleWithTheDamageCard()
        {
            var pct = Fix.Ratio(3, 20);
            Assert.That(BodyReflect(Fix.Zero), Is.EqualTo(Fix.FromInt(10) * WeaponTuning.AegisBodyReflectPct), "no card: unchanged");
            Assert.That(BodyReflect(pct), Is.EqualTo(Fix.FromInt(10) * WeaponTuning.AegisBodyReflectPct * (Fix.One + pct)));
            var parry0 = ParryReflect(Fix.Zero);
            Assert.That(parry0, Is.GreaterThan(Fix.Zero));
            Assert.That(ParryReflect(pct).ToFloat(), Is.EqualTo((parry0 * (Fix.One + pct)).ToFloat()).Within(0.001f));
        }

        [Test]
        public void WithoutCardsAWholeMatchIsUnchanged()
        {
            // DamagePct 0 multiplies by exactly 1: the SPORE vs MIRROR match is bit-identical to one built without a bonus at all.
            ulong Play(bool explicitZero)
            {
                var a = new BallLoadout(WeaponRegistry.Create(VenomRule.WeaponId));
                var b = new BallLoadout(WeaponRegistry.Create(AegisRule.WeaponId));
                if (explicitZero) { a.Bonus = new StatBonus { DamagePct = Fix.Zero }; b.Bonus = new StatBonus { DamagePct = Fix.Zero }; }
                var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 77, new[] { a, b });
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                return m.ComputeHash();
            }
            Assert.That(Play(true), Is.EqualTo(Play(false)));
        }
    }
}
