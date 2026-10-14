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
            var m = Stage.Build(null, (Stage.L(a), FT.V(-80f, 0f), 90f), (Stage.L(b), FT.V(80f, 0f), 90f));
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
            venom.OnHitDealt(target, FT.X(1f), DamageKind.Weapon);
            venom.OnHitDealt(target, FT.X(1f), DamageKind.Weapon);
            Assert.That(target.Status.PoisonStackCount, Is.EqualTo(1), "allowance 1");
            venom.OnHit(default);
            venom.OnHitDealt(target, FT.X(1f), DamageKind.Weapon);
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
            Assert.That((before - blade.Hp).F(), Is.EqualTo((new BladeRule().Damage(new HitContext(FT.X(0f), FT.X(0f))) * WeaponTuning.AegisReflectPct).F()).Within(1e-4f));
            var width = aegis.BladeThickness;
            aegis.OnHit(default);
            Assert.That(aegis.BladeThickness.F(), Is.EqualTo((width + WeaponTuning.AegisWidthPerHit).F()).Within(1e-4f));
        }

        [Test]
        public void AegisIgnoresBladelessAttackersOnParry()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new BrawlerRule());
            aegis.OnParry(m.Balls[1]);
            Assert.That(m.Balls[1].Hp.F(), Is.EqualTo(100f));
        }

        [Test]
        public void RigDropsTurretsReplacesTheOldestAndShoots()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            for (var i = 0; i < WeaponTuning.RigMaxTurrets + 2; i++) { m.Balls[0].Pos = FT.V(-80f + i, 0f); rig.OnHit(default); }
            Assert.That(rig.TurretCount, Is.EqualTo(WeaponTuning.RigMaxTurrets));
            Assert.That(Enumerable.Range(0, rig.TurretCount).Select(rig.GetTurret).Any(p => p.X == -FT.X(80f)), Is.False, "oldest turrets were replaced");
            for (var i = 0; i < WeaponTuning.RigFireIntervalTicks; i++) m.Step();
            Assert.That(m.Projectiles.ActiveCount, Is.GreaterThanOrEqualTo(WeaponTuning.RigMaxTurrets));
        }

        [Test]
        public void RigTurretsOutsideTheArenaAreLost()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            m.Balls[0].Pos = FT.V(10000f, 0f);
            rig.OnHit(default);
            Assert.That(rig.TurretCount, Is.EqualTo(1));
            m.Balls[0].Pos = FT.V(-80f, 0f);
            m.Step();
            Assert.That(rig.TurretCount, Is.EqualTo(0));
        }

        [Test]
        public void AegisSendsBodyBlowsBackAndAbsorbsShots()
        {
            var aegis = new AegisRule();
            var m = Duel(aegis, new BrawlerRule());
            var brawler = m.Balls[1];
            m.Balls[0].Hp = FT.X(50f);
            aegis.OnHitTaken(brawler, FT.X(10f), DamageKind.Weapon);
            Assert.That(brawler.Hp.F(), Is.EqualTo((FT.X(100f) - FT.X(10f) * WeaponTuning.AegisBodyReflectPct).F()).Within(1e-4f));
            aegis.OnHitTaken(brawler, FT.X(10f), DamageKind.Projectile);
            // BB-6d: a body blow is a guard (AegisGuardBody points of mend), the shot a smaller one (AegisGuardShot) plus the absorb.
            Assert.That(m.Balls[0].Hp.F(), Is.EqualTo((FT.X(50f) + WeaponTuning.AegisMendPerGuard * (WeaponTuning.AegisGuardBody + WeaponTuning.AegisGuardShot)
                                                        + FT.X(10f) * WeaponTuning.AegisShotAbsorbPct).F()).Within(1e-4f));
            m.Balls[0].Hp = FT.X(0f); // pending dead: no reflect
            var before = brawler.Hp;
            aegis.OnHitTaken(brawler, FT.X(10f), DamageKind.Weapon);
            Assert.That(brawler.Hp, Is.EqualTo(before));
        }

        [Test]
        public void AegisHitsHarderAfterEnoughGuards()
        {
            // BB-6d: rage follows guards (parries + hits taken), not time.
            var aegis = new AegisRule();
            var m = Duel(aegis, new BladeRule());
            var blade = m.Balls[1];
            var early = aegis.Damage(default);
            for (var i = 0; i < 5000; i++) aegis.OnTick();
            Assert.That(aegis.Damage(default), Is.EqualTo(early), "time alone changes nothing");
            m.Balls[0].Hp = FT.X(50f);
            var parries = 0;
            while (aegis.Guards + WeaponTuning.AegisGuardMelee <= WeaponTuning.AegisRageStartGuards) { aegis.OnParry(blade); parries++; }
            Assert.That(aegis.Damage(default), Is.EqualTo(early), "no rage up to AegisRageStartGuards points");
            Assert.That(m.Balls[0].Hp, Is.EqualTo(FT.X(50f) + WeaponTuning.AegisMendPerGuard * (parries * WeaponTuning.AegisGuardMelee)), "each guard point mends");
            var g0 = aegis.Guards;
            var hits = 0;
            while (aegis.Guards < WeaponTuning.AegisRageStartGuards + WeaponTuning.AegisRageRampGuards) { aegis.OnHitTaken(blade, FT.X(1f), DamageKind.Weapon); hits++; }
            Assert.That(aegis.Guards - g0, Is.EqualTo(hits * WeaponTuning.AegisGuardMelee), "a blade hit taken = AegisGuardMelee points");
            var rage = aegis.Guards - WeaponTuning.AegisRageStartGuards;
            Assert.That(aegis.Damage(default), Is.EqualTo(early + early * rage / WeaponTuning.AegisRageRampGuards), "+1/ramp per point past the start");
        }

        [Test]
        public void RigKeepsTheOldestFirstWhenATurretIsLost()
        {
            var rig = new RigRule();
            var m = Duel(rig, new HarmlessRule());
            for (var i = 0; i < WeaponTuning.RigMaxTurrets + 2; i++) { m.Balls[0].Pos = FT.V(-80f + i, 0f); rig.OnHit(default); }
            // slots now hold x = -74..-79 in ring order, oldest (-74) at slot 2. Lose one by moving it out.
            m.Balls[0].Pos = FT.V(10000f, 0f);
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
