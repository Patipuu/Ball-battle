using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class NewWeaponTests
    {
        static MatchSim Duel(WeaponRule a, WeaponRule b)
        {
            var m = Stage.Build(null, (Stage.L(a), new Vec2(-80f, 0f), 90f), (Stage.L(b), new Vec2(80f, 0f), 90f));
            m.Step(); // binds weapons to their balls
            return m;
        }

        [Test]
        public void VolleyFiresItsArrowsEveryInterval()
        {
            var volley = new VolleyRule();
            var m = Duel(volley, new HarmlessRule());
            for (var i = 0; i < WeaponTuning.VolleyIntervalTicks - 2; i++) m.Step();
            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(0));
            m.Step();
            Assert.That(m.Projectiles.ActiveCount, Is.EqualTo(1));
            volley.OnHit(default);
            volley.OnHit(default);
            Assert.That(volley.Arrows, Is.EqualTo(3));
            for (var i = 0; i < WeaponTuning.VolleyIntervalTicks; i++) m.Step();
            Assert.That(m.Projectiles.ActiveCount, Is.GreaterThanOrEqualTo(3), "the bigger volley is in the air");
        }

        [Test]
        public void VolleyArrowsAreCapped()
        {
            var volley = new VolleyRule();
            for (var i = 0; i < 20; i++) volley.OnHit(default);
            Assert.That(volley.Arrows, Is.EqualTo(WeaponTuning.VolleyMaxArrows));
        }

        [Test]
        public void VenomPoisonsUpToItsAllowanceWhichGrowsPerHit()
        {
            var venom = new VenomRule();
            var m = Duel(venom, new HarmlessRule());
            var target = m.Balls[1];
            venom.OnHitDealt(target, 1f, DamageKind.Weapon);
            venom.OnHitDealt(target, 1f, DamageKind.Weapon);
            Assert.That(target.Status.PoisonStackCount, Is.EqualTo(1), "allowance 1");
            venom.OnHit(default);
            venom.OnHitDealt(target, 1f, DamageKind.Weapon);
            Assert.That(target.Status.PoisonStackCount, Is.EqualTo(2));
        }

        [Test]
        public void AegisReflectsTheParriedBlowAndWidensOnHits()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new BladeRule());
            var blade = m.Balls[1];
            var before = blade.Hp;
            aegis.OnParry(blade);
            Assert.That(before - blade.Hp, Is.EqualTo(new BladeRule().Damage(new HitContext(0f, 0f)) * WeaponTuning.AegisReflectPct).Within(1e-4f));
            var width = aegis.BladeThickness;
            aegis.OnHit(default);
            Assert.That(aegis.BladeThickness, Is.EqualTo(width + WeaponTuning.AegisWidthPerHit).Within(1e-4f));
        }

        [Test]
        public void AegisIgnoresBladelessAttackersOnParry()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new BrawlerRule());
            aegis.OnParry(m.Balls[1]);
            Assert.That(m.Balls[1].Hp, Is.EqualTo(100f));
        }

        [Test]
        public void RigDropsTurretsReplacesTheOldestAndShoots()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            for (var i = 0; i < WeaponTuning.RigMaxTurrets + 2; i++) { m.Balls[0].Pos = new Vec2(-80f + i, 0f); rig.OnHit(default); }
            Assert.That(rig.TurretCount, Is.EqualTo(WeaponTuning.RigMaxTurrets));
            Assert.That(Enumerable.Range(0, rig.TurretCount).Select(rig.GetTurret).Any(p => p.X == -80f), Is.False, "oldest turrets were replaced");
            for (var i = 0; i < WeaponTuning.RigFireIntervalTicks; i++) m.Step();
            Assert.That(m.Projectiles.ActiveCount, Is.GreaterThanOrEqualTo(WeaponTuning.RigMaxTurrets));
        }

        [Test]
        public void RigTurretsOutsideTheArenaAreLost()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            m.Balls[0].Pos = new Vec2(10000f, 0f);
            rig.OnHit(default);
            Assert.That(rig.TurretCount, Is.EqualTo(1));
            m.Balls[0].Pos = new Vec2(-80f, 0f);
            m.Step();
            Assert.That(rig.TurretCount, Is.EqualTo(0));
        }

        [Test]
        public void AegisSendsBodyBlowsBackAndAbsorbsShots()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new BrawlerRule());
            var brawler = m.Balls[1];
            m.Balls[0].Hp = 50f;
            aegis.OnHitTaken(brawler, 10f, DamageKind.Weapon);
            Assert.That(brawler.Hp, Is.EqualTo(100f - 10f * WeaponTuning.AegisBodyReflectPct).Within(1e-4f));
            aegis.OnHitTaken(brawler, 10f, DamageKind.Projectile);
            Assert.That(m.Balls[0].Hp, Is.EqualTo(50f + 10f * WeaponTuning.AegisShotAbsorbPct).Within(1e-4f));
            m.Balls[0].Hp = 0f; // pending dead: no reflect
            var before = brawler.Hp;
            aegis.OnHitTaken(brawler, 10f, DamageKind.Weapon);
            Assert.That(brawler.Hp, Is.EqualTo(before));
        }

        [Test]
        public void AegisHitsHarderLateInTheFight()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new HarmlessRule());
            var early = aegis.Damage(default);
            for (var i = 0; i < WeaponTuning.AegisRageStartTicks + WeaponTuning.AegisRageRampTicks; i++) aegis.OnTick();
            Assert.That(aegis.Damage(default), Is.EqualTo(early * 2f).Within(0.01f));
        }

        [Test]
        public void RigKeepsTheOldestFirstWhenATurretIsLost()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            for (var i = 0; i < WeaponTuning.RigMaxTurrets + 2; i++) { m.Balls[0].Pos = new Vec2(-80f + i, 0f); rig.OnHit(default); }
            // slots now hold x = -74..-79 in ring order, oldest (-74) at slot 2. Lose one by moving it out.
            m.Balls[0].Pos = new Vec2(10000f, 0f);
            rig.OnHit(default);
            m.Step();
            Assert.That(rig.TurretCount, Is.EqualTo(WeaponTuning.RigMaxTurrets - 1));
            Assert.That(rig.GetTurret(0).X, Is.LessThan(rig.GetTurret(1).X), "oldest first");
        }

        [Test]
        public void ARealHitDropsATurretAndNewWeaponsAreDeterministic()
        {
            ulong Run(string id)
            {
                var m = new MatchSim(new MatchConfig(), 7, new[] { WeaponRegistry.Create(id), WeaponRegistry.Create("blade") });
                for (var i = 0; i < 1500 && m.Outcome == MatchOutcome.Ongoing; i++) m.Step();
                return m.ComputeHash();
            }
            foreach (var id in new[] { "volley", "venom", "aegis", "rig" })
                Assert.That(Run(id), Is.EqualTo(Run(id)), id);

            var rig = new RigRule();
            var rm = new MatchSim(new MatchConfig(), 3, new WeaponRule[] { rig, new BladeRule() });
            for (var i = 0; i < 3000 && rm.Outcome == MatchOutcome.Ongoing && rig.TurretCount == 0; i++) rm.Step();
            Assert.That(rig.TurretCount, Is.GreaterThan(0));
        }

        [Test]
        public void EveryWeaponShowsAHudStatAndAHashableState()
        {
            foreach (var e in WeaponRegistry.All)
            {
                var w = e.Create();
                Assert.That(w.StatLabel, Is.Not.Empty, e.Id);
                Assert.That(w.HashInto(0UL), Is.Not.EqualTo(e.Create().HashInto(1UL)));
            }
        }
    }
}