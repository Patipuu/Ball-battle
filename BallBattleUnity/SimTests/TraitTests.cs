using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class TraitTests
    {
        static MatchSim Duel(BallLoadout attacker, BallLoadout target)
            => Stage.Build(null, (attacker, new Vec2(0f, 0f), 0f), (target, new Vec2(45f, 0f), 0f));

        [Test]
        public void ThornsSendsMeleeShareBack([Values(1, 2)] int level)
        {
            var m = Duel(Stage.L(new FixedBladeRule(10f, 25f, 0f)), Stage.L(new HarmlessRule(), new ThornsTrait(level)));
            m.Step();
            Assert.That(m.Balls[1].Hp, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(100f - 10f * TraitTuning.ThornsReflectPct[level - 1]).Within(1e-4f));
        }

        [Test]
        public void SecondWindHealsOnceWhenLow([Values(1, 2)] int level)
        {
            var target = Stage.L(new HarmlessRule(), new SecondWindTrait(level));
            target.Hp = 25f;
            var m = Duel(Stage.L(new HarmlessRule()), target);
            m.Step();
            var heal = TraitTuning.SecondWindHeal[level - 1];
            Assert.That(m.Balls[1].Hp, Is.EqualTo(25f + heal).Within(1e-4f));
            m.Balls[1].Hp = 10f;
            m.Step();
            Assert.That(m.Balls[1].Hp, Is.EqualTo(10f), "only once");
        }

        [Test]
        public void SecondWindTurnsALethalBlowIntoTheHeal()
        {
            var target = Stage.L(new HarmlessRule(), new SecondWindTrait(1));
            target.Hp = 30f;
            var m = Duel(Stage.L(new FixedBladeRule(50f, 25f, 0f)), target);
            m.Step();
            Assert.That(m.Balls[1].Alive);
            Assert.That(m.Balls[1].Hp, Is.EqualTo(TraitTuning.SecondWindHeal[0]).Within(1e-4f));
        }

        [Test]
        public void GlassCannonHitsHarderAndHasLessHp([Values(1, 2)] int level)
        {
            var m = Duel(Stage.L(new FixedBladeRule(10f, 25f, 0f), new GlassCannonTrait(level)), Stage.L(new HarmlessRule()));
            m.Step();
            var i = level - 1;
            Assert.That(m.Balls[0].MaxHp, Is.EqualTo(100f - TraitTuning.GlassCannonHpLoss[i]).Within(1e-3f));
            Assert.That(m.Balls[0].Hp, Is.EqualTo(m.Balls[0].MaxHp));
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f - 10f * TraitTuning.GlassCannonDamageMul[i]).Within(1e-3f));
        }

        [Test]
        public void GlassCannonKeepsTheCarriedHpFraction()
        {
            var l = Stage.L(new HarmlessRule(), new GlassCannonTrait(1));
            l.Hp = 50f;
            var m = Duel(l, Stage.L(new HarmlessRule()));
            m.Step();
            Assert.That(m.Balls[0].HpFraction, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void ParryMasterGrowsTheWeaponPerParry([Values(1, 2)] int level)
        {
            var m = Duel(Stage.L(new BladeRule(), new ParryMasterTrait(level)), Stage.L(new BladeRule()));
            var before = m.Balls[0].Weapon.StatValue;
            for (var i = 0; i < TraitTuning.ParryMasterEvery[level - 1]; i++) m.Balls[0].Traits[0].OnParry(m.Balls[1]);
            var control = Stage.L(new BladeRule());
            var cm = Duel(control, Stage.L(new BladeRule()));
            cm.Balls[0].Weapon.OnHit(new HitContext(0f, 0f));
            Assert.That(m.Balls[0].Weapon.StatValue, Is.GreaterThan(before));
            Assert.That(m.Balls[0].Weapon.StatValue, Is.EqualTo(cm.Balls[0].Weapon.StatValue).Within(1e-4f));
        }

        [Test]
        public void BubbleGivesOneShieldPerInterval([Values(1, 2)] int level)
        {
            var interval = TraitTuning.BubbleIntervalTicks[level - 1];
            var m = Duel(Stage.L(new HarmlessRule()), Stage.L(new HarmlessRule(), new BubbleTrait(level)));
            for (var i = 0; i < interval - 1; i++) m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(0));
            m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(1));
            for (var i = 0; i < interval * 2; i++) m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(1), "never more than one banked");
        }

        [Test]
        public void PoisonTipPoisonsOnHit([Values(1, 2)] int level)
        {
            var m = Duel(Stage.L(new FixedBladeRule(5f, 25f, 0f), new PoisonTipTrait(level)), Stage.L(new HarmlessRule()));
            m.Step();
            Assert.That(m.Balls[1].Status.PoisonStackCount, Is.EqualTo(1));
            var s = m.Balls[1].Status.Poison.First(p => p.TicksLeft > 0);
            Assert.That(s.Dps, Is.EqualTo(TraitTuning.PoisonTipDps[level - 1]));
            Assert.That(s.TicksLeft, Is.EqualTo(TraitTuning.PoisonTipSeconds[level - 1] * MatchConfig.TicksPerSecond).Within(2));
        }

        [Test]
        public void ParryMasterIsOnlyEligibleWithABlade()
        {
            Assert.That(TraitRegistry.IsEligible("parry-master", "blade"));
            Assert.That(TraitRegistry.IsEligible("parry-master", "brawler"), Is.False);
            Assert.That(TraitRegistry.IsEligible("poison-tip", "brawler"), Is.False);
            Assert.That(TraitRegistry.IsEligible("glass-cannon", "brawler"), Is.False);
            Assert.That(TraitRegistry.IsEligible("glass-cannon", "blade"));
            Assert.That(TraitRegistry.IsEligible("thorns", "brawler"));
        }

        [Test]
        public void BubbleWaitsAFullIntervalAfterTheShieldIsUsed()
        {
            var interval = TraitTuning.BubbleIntervalTicks[0];
            var m = Duel(Stage.L(new HarmlessRule()), Stage.L(new HarmlessRule(), new BubbleTrait(1)));
            for (var i = 0; i < interval + 30; i++) m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(1));
            m.Balls[1].Status.ShieldCharges = 0;
            for (var i = 0; i < interval - 1; i++) m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(0));
            m.Step();
            Assert.That(m.Balls[1].Status.ShieldCharges, Is.EqualTo(1));
        }

        [Test]
        public void EnemiesNeverRollIneligibleTraits()
        {
            for (uint seed = 1; seed <= 300; seed++)
                for (var fight = 0; fight < 8; fight++)
                {
                    var e = BallBattle.Sim.Run.EnemyGenerator.Generate(seed, fight);
                    foreach (var slot in e.Traits)
                        Assert.That(TraitRegistry.IsEligible(slot.Id, e.WeaponId), $"seed {seed} fight {fight}: {slot.Id} on {e.WeaponId}");
                }
        }

        [Test]
        public void WeaponSwapOffersKeepHeldTraitsUseful()
        {
            var weapons = WeaponRegistry.All.Select(w => w.Id).ToList();
            var traits = TraitRegistry.All.Select(x => x.Id).ToList();
            var build = new BallBattle.Sim.Run.RunBuild { WeaponId = "blade" };
            build.Traits.Add(new BallBattle.Sim.Run.TraitSlot("parry-master", 1));
            for (uint seed = 1; seed <= 200; seed++)
                foreach (var card in BallBattle.Sim.Run.CardOffer.Generate(seed, 0, 0, build, weapons, traits))
                    Assert.That(card.Kind == BallBattle.Sim.Run.CardKind.SwapWeapon && card.Id == "brawler", Is.False);
        }
    }
}