using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class WeaponRuleTests
    {
        static readonly HitContext Ctx = new HitContext(6f, 6f);

        static void Hit(WeaponRule w, int times)
        {
            for (var i = 0; i < times; i++) w.OnHit(Ctx);
        }

        [Test]
        public void BladeGainsOneDamagePerHit()
        {
            var w = new BladeRule();
            Assert.That(w.Damage(Ctx), Is.EqualTo(1f));
            Hit(w, 9);
            Assert.That(w.Damage(Ctx), Is.EqualTo(10f));
            Assert.That(w.StatValue, Is.EqualTo(10f));
        }

        [Test]
        public void FangSpinsFasterWithShrinkingGainAndCap()
        {
            var w = new FangRule();
            Hit(w, 1);
            Assert.That(w.SpinDegPerTick, Is.EqualTo(WeaponTuning.FangStartSpin + WeaponTuning.FangStartSpinGain).Within(1e-4f));
            Assert.That(w.SpinGain, Is.EqualTo(WeaponTuning.FangStartSpinGain * WeaponTuning.FangSpinGainDecay).Within(1e-4f));
            Hit(w, 500);
            Assert.That(w.SpinDegPerTick, Is.EqualTo(WeaponTuning.FangMaxSpin));
            Assert.That(w.Damage(Ctx), Is.EqualTo(WeaponTuning.FangStartDamage + 501 * WeaponTuning.FangDamagePerHit).Within(1e-2f));
            Assert.That(w.HitCooldownTicks, Is.EqualTo(WeaponTuning.FangHitCooldownTicks));
        }

        [Test]
        public void PikeGrowsLengthAndDamageWithCap()
        {
            var w = new PikeRule();
            Hit(w, 4);
            Assert.That(w.BladeLength, Is.EqualTo(WeaponTuning.PikeStartLength + 4 * WeaponTuning.PikeGrowthPerHit).Within(1e-4f));
            Assert.That(w.Damage(Ctx), Is.EqualTo(WeaponTuning.PikeStartDamage + 4 * WeaponTuning.PikeGrowthPerHit).Within(1e-4f));
            Hit(w, 1000);
            Assert.That(w.BladeLength, Is.EqualTo(WeaponTuning.PikeMaxLength));
            Assert.That(w.Damage(Ctx), Is.GreaterThan(400f), "damage keeps growing after length caps");
        }

        [Test]
        public void BrawlerDamageScalesWithSpeedAndCapGrows()
        {
            var w = new BrawlerRule();
            Assert.That(w.HasBlade, Is.False);
            Assert.That(w.BodyAttacks, Is.True);
            Assert.That(w.Damage(new HitContext(10f, 3f)), Is.EqualTo(10f * WeaponTuning.BrawlerStartDamagePerSpeed).Within(1e-4f));
            Assert.That(w.MaxSpeedBonus, Is.EqualTo(WeaponTuning.BrawlerStartSpeedBonus));
            Hit(w, 2);
            w.OnWall();
            Assert.That(w.MaxSpeedBonus, Is.EqualTo(WeaponTuning.BrawlerStartSpeedBonus + 2 * WeaponTuning.BrawlerSpeedBonusPerHit + WeaponTuning.BrawlerSpeedBonusPerWall).Within(1e-4f));
            for (var i = 0; i < 200; i++) w.OnWall();
            Assert.That(w.MaxSpeedBonus, Is.EqualTo(WeaponTuning.BrawlerMaxSpeedBonus));
        }

        [Test]
        public void RegistryCreatesFreshInstancesWithHudStats()
        {
            Assert.That(WeaponRegistry.All.Select(e => e.Id), Is.EquivalentTo(new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" }));
            foreach (var e in WeaponRegistry.All)
            {
                var a = e.Create();
                var b = e.Create();
                Assert.That(a, Is.Not.SameAs(b));
                Assert.That(a.Id, Is.EqualTo(e.Id));
                Assert.That(a.StatLabel, Is.Not.Empty, e.Id);
                Assert.That(e.DisplayName, Is.Not.Empty);
            }
            Assert.Throws<System.ArgumentException>(() => WeaponRegistry.Get("nope"));
        }

        [Test]
        public void BrawlerIsNeverParried()
        {
            // Brawler body overlapping a blade: no parry possible, the blade just hits.
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var m = new MatchSim(cfg, 1, new WeaponRule[] { new BladeRule(), new BrawlerRule() });
            m.Balls[0].Pos = new Vec2(0, 0); m.Balls[0].Vel = Vec2.Zero; m.Balls[0].WeaponAngleDeg = 0f;
            m.Balls[1].Pos = new Vec2(34, 0); m.Balls[1].Vel = Vec2.Zero;
            m.Step();
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Parry), Is.False);
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Hit && e.A == 0), Is.True);
        }

        [Test]
        public void FangParriesBlade()
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var m = new MatchSim(cfg, 1, new WeaponRule[] { new FangRule(), new BladeRule() });
            m.Balls[0].Pos = new Vec2(-20, 0); m.Balls[0].Vel = Vec2.Zero; m.Balls[0].WeaponAngleDeg = 0f;
            m.Balls[1].Pos = new Vec2(20, 0); m.Balls[1].Vel = Vec2.Zero; m.Balls[1].WeaponAngleDeg = 180f;
            m.Step();
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Parry), Is.True);
        }

        [Test]
        public void BrawlerWallBounceAddsSpeedWithinCap()
        {
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var w = new BrawlerRule();
            var m = new MatchSim(cfg, 1, new WeaponRule[] { w, new HarmlessRule() });
            m.Balls[0].Pos = new Vec2(m.Arena.Right - 17f, 0); m.Balls[0].Vel = new Vec2(5f, 0f);
            m.Balls[1].Pos = new Vec2(-80, 0); m.Balls[1].Vel = Vec2.Zero;
            m.Step();
            Assert.That(m.Balls[0].Vel.X, Is.LessThan(-5f), "bounced back faster");
            Assert.That(m.Balls[0].Vel.Length, Is.LessThanOrEqualTo(cfg.MaxSpeed + w.MaxSpeedBonus + 1e-4f));
        }

        [Test]
        public void AllMatchupsAreDeterministic()
        {
            foreach (var a in WeaponRegistry.All)
            foreach (var b in WeaponRegistry.All)
            {
                ulong Run()
                {
                    var m = new MatchSim(new MatchConfig(), 77, new[] { a.Create(), b.Create() });
                    for (var t = 0; t < 3000 && m.Outcome == MatchOutcome.Ongoing; t++) m.Step();
                    return m.ComputeHash();
                }
                Assert.That(Run(), Is.EqualTo(Run()), $"{a.Id} vs {b.Id}");
            }
        }

        [Test]
        public void FangParriesFullLengthPike()
        {
            var pike = new PikeRule();
            for (var i = 0; i < 200; i++) pike.OnHit(Ctx);
            Assert.That(pike.BladeLength, Is.EqualTo(WeaponTuning.PikeMaxLength));
            var cfg = new MatchConfig { Gravity = 0f, MinHorizontalSpeed = 0f };
            var m = new MatchSim(cfg, 1, new WeaponRule[] { new FangRule(), pike });
            // Pike points left from x=60 through Fang's blade, which points straight up from x=0.
            m.Balls[0].Pos = new Vec2(0, -10); m.Balls[0].Vel = Vec2.Zero; m.Balls[0].WeaponAngleDeg = 90f;
            m.Balls[1].Pos = new Vec2(60, 10); m.Balls[1].Vel = Vec2.Zero; m.Balls[1].WeaponAngleDeg = 180f;
            m.Step();
            Assert.That(m.Events.Any(e => e.Type == SimEventType.Parry), Is.True);
        }

        [Test]
        public void EveryWeaponHashChangesWhenItScales()
        {
            foreach (var e in WeaponRegistry.All)
            {
                var a = e.Create();
                var b = e.Create();
                new MatchSim(new MatchConfig(), 1, new[] { a, new HarmlessRule() });
                new MatchSim(new MatchConfig(), 1, new[] { b, new HarmlessRule() });
                Assert.That(a.HashInto(SimHash.Seed), Is.EqualTo(b.HashInto(SimHash.Seed)), e.Id);
                b.OnHit(Ctx);
                Assert.That(a.HashInto(SimHash.Seed), Is.Not.EqualTo(b.HashInto(SimHash.Seed)), $"{e.Id}: scaling state must be hashed");
            }
        }

        [Test]
        public void HudStatsAreReadable()
        {
            var cfg = new MatchConfig();
            var brawler = new BrawlerRule();
            new MatchSim(cfg, 1, new WeaponRule[] { brawler, new HarmlessRule() });
            Assert.That(brawler.StatValue, Is.EqualTo(cfg.MaxSpeed + WeaponTuning.BrawlerStartSpeedBonus));
            Assert.That(brawler.StatValue, Is.GreaterThan(0f));
            Assert.That(new BladeRule().StatText, Is.EqualTo("1"));
            Assert.That(new FangRule().StatLabel, Is.EqualTo("SPIN"));
            var pike = new PikeRule();
            pike.OnHit(Ctx);
            Assert.That(pike.StatText, Is.EqualTo("26").Or.EqualTo("27"), "length shown as whole pixels");
        }
    }
}