using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    [TestFixture]
    public sealed class Boss8Tests
    {
        static MatchSim Fight(string id, uint seed = 13)
        {
            var a = new BallLoadout(new BossCoreRule()) { MaxHp = 10000 };
            var b = new BallLoadout(new BossCoreRule()) { MaxHp = 10000, Boss = BossRegistry.Create(id) };
            return new MatchSim(new MatchConfig { CapTicks = 4000 }, seed, new[] { a, b }, true);
        }

        [Test]
        public void EightBossesHaveAuthoredLoadoutsAndValidSlots()
        {
            Assert.That(BossIds.All.Length, Is.EqualTo(8));
            Assert.That(BossIds.Slots[0].Length, Is.EqualTo(4));
            Assert.That(BossIds.Slots[1].Length, Is.EqualTo(4));
            Assert.That(BossIds.All.Distinct().Count(), Is.EqualTo(8));
            foreach (var id in BossIds.All)
            {
                var slot = BossIds.SlotOf(id);
                Assert.That(slot, Is.InRange(0, 1));
                var enemy = EnemyGenerator.Generate(72, RunTuning.BossFights[slot], id);
                Assert.That(enemy.BossId, Is.EqualTo(id));
                Assert.That(enemy.Traits, Is.Empty);
                if (id != BossIds.Mimic) Assert.That(enemy.WeaponId, Is.EqualTo(BossCoreRule.WeaponId));
                Assert.That(enemy.ToLoadout().Boss.Id, Is.EqualTo(id));
            }
        }

        [TestCase(BossIds.Hunter, BossEvent.HunterCharge)]
        [TestCase(BossIds.Bell, BossEvent.BellWave)]
        [TestCase(BossIds.Ember, BossEvent.EmberIgnite)]
        [TestCase(BossIds.Prism, BossEvent.PrismBeam)]
        [TestCase(BossIds.Mimic, BossEvent.MimicEchoStrike)]
        [TestCase(BossIds.Warden, BossEvent.WardenCrossFire)]
        public void NewBossAttackHasAWarningAndARelease(string id, BossEvent release)
        {
            var sim = Fight(id);
            var events = new List<BossEvent>();
            for (var i = 0; i < 270; i++)
            {
                sim.Step();
                foreach (var e in sim.Events)
                    if (e.Type == SimEventType.Boss) events.Add((BossEvent)e.Variant);
            }
            Assert.That(events, Does.Contain(release));
            Assert.That(events.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(events.IndexOf(release), Is.GreaterThan(0));
        }

        [Test]
        public void MimicAdaptsToTheDamageTypeItActuallyTook()
        {
            var sim = Fight(BossIds.Mimic);
            var source = sim.Balls[0];
            var boss = sim.Balls[1];
            Assert.That(sim.DealDamage(source, boss, 1000, DamageKind.Ability), Is.EqualTo((Fix)1000));
            while (sim.ActiveTick < 900) sim.Step();
            var rule = (MimicRule)sim.Boss;
            Assert.That(rule.Adapted, Is.True);
            Assert.That(rule.AdaptedKind, Is.EqualTo(DamageKind.Ability));
            Assert.That(sim.DealDamage(source, boss, 10, DamageKind.Ability), Is.EqualTo((Fix)10 * Fix.Ratio(7, 10)));
        }

        [Test]
        public void EveryBossCanDamageThePlayerWithAnAuthoredAttack()
        {
            foreach (var id in BossIds.All)
            {
                var player = new BallLoadout(new BossCoreRule()) { MaxHp = 10000, Hp = 10000 };
                var boss = new BallLoadout(new BossCoreRule(id)) { MaxHp = 10000, Hp = 10000, Boss = BossRegistry.Create(id) };
                var sim = Stage.Build(Stage.StillConfig(), (player, FT.V(40, 0), 0f), (boss, FT.V(0, 0), 0f));
                var abilityDamage = Fix.Zero;
                while (sim.ActiveTick < 1000 && sim.Outcome == MatchOutcome.Ongoing)
                {
                    Stage.Park(sim, FT.V(40, 0), FT.V(0, 0));
                    sim.Step();
                    foreach (var e in sim.Events)
                        if (e.Type == SimEventType.Damage && e.A == 1 && e.B == 0 &&
                            e.Variant == (byte)DamageKind.Ability) abilityDamage += e.Value;
                }
                Assert.That(abilityDamage, Is.GreaterThan(Fix.Zero), id + " never dealt special-attack damage");
            }
        }

        [TestCase(BossIds.Mimic, BossEvent.MimicEchoWarn, BossEvent.MimicEchoStrike)]
        [TestCase(BossIds.Warden, BossEvent.WardenCrossWarn, BossEvent.WardenCrossFire)]
        public void MarkedBossAttackCanBeDodgedDuringItsWarning(string id, BossEvent warning, BossEvent release)
        {
            var player = new BallLoadout(new BossCoreRule()) { MaxHp = 10000, Hp = 10000 };
            var boss = new BallLoadout(new BossCoreRule(id)) { MaxHp = 10000, Hp = 10000, Boss = BossRegistry.Create(id) };
            var sim = Stage.Build(Stage.StillConfig(), (player, FT.V(40, 0), 0f), (boss, FT.V(0, 0), 0f));
            var warned = false;
            var released = false;
            var abilityDamage = Fix.Zero;
            while (!released && sim.ActiveTick < 300)
            {
                Stage.Park(sim, warned ? FT.V(90, 60) : FT.V(40, 0), FT.V(0, 0));
                sim.Step();
                foreach (var e in sim.Events)
                {
                    if (e.Type == SimEventType.Boss && e.Variant == (byte)warning) warned = true;
                    if (e.Type == SimEventType.Boss && e.Variant == (byte)release) released = true;
                    if (warned && e.Type == SimEventType.Damage && e.A == 1 && e.B == 0 &&
                        e.Variant == (byte)DamageKind.Ability) abilityDamage += e.Value;
                }
            }
            Assert.That(warned && released, Is.True, id + " did not complete its warned attack");
            Assert.That(abilityDamage, Is.EqualTo(Fix.Zero), id + " hit after the player left the marked area");
        }

        [Test]
        public void BossEventsAndHashesReplayIdentically()
        {
            foreach (var id in BossIds.All)
            {
                var a = Fight(id);
                var b = Fight(id);
                for (var tick = 0; tick < 500; tick++)
                {
                    a.Step(); b.Step();
                    Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()), $"{id} tick {tick}");
                }
            }
        }
    }
}
