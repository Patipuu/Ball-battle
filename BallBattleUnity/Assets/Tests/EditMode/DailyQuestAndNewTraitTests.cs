using System;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>Daily quests (chests instead of unlock goals), every ball open, the five newer traits and the coin glyph.</summary>
    public class DailyQuestAndNewTraitTests
    {
        const int Day = 9790;   // any fixed day

        static RunState RunWith(params bool[] wins)
        {
            var run = new RunState();
            run.Build.WeaponId = "blade";
            for (var i = 0; i < wins.Length; i++) run.History.Add(new FightRecord { FightIndex = i, Won = wins[i] });
            return run;
        }

        [Test]
        public void SameDayRollsTheSameThreeQuestsAndANewDayRerolls()
        {
            var a = DailyQuests.Roll(Day).Select(s => s.Id + s.Ball).ToArray();
            var b = DailyQuests.Roll(Day).Select(s => s.Id + s.Ball).ToArray();
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Length, Is.EqualTo(DailyQuests.PerDay));
            var p = new ProgressData();
            var today = DailyQuests.Today(p, Day);
            today.Slots[0].Progress = 1;
            Assert.That(DailyQuests.Today(p, Day).Slots[0].Progress, Is.EqualTo(1), "kept within the day");
            Assert.That(DailyQuests.Today(p, Day + 1).Day, Is.EqualTo(Day + 1));
            Assert.That(p.Daily.Slots.All(s => s.Progress == 0 && !s.Done), Is.True, "a new day starts fresh");
        }

        [Test]
        public void EveryDayHasOneEasyQuestAndNoTwoOfTheSameKind()
        {
            for (var d = 0; d < 400; d++)
            {
                var defs = DailyQuests.Roll(d).Select(s => DailyQuests.Def(s.Id)).ToList();
                Assert.That(defs[0].Tier, Is.EqualTo(QuestTier.Easy), $"day {d}");
                Assert.That(defs.Select(x => x.Kind).Distinct().Count(), Is.EqualTo(DailyQuests.PerDay), $"day {d}");
            }
        }

        [Test]
        public void FinishingAQuestDropsItsChestAndAllThreeAddTheBonus()
        {
            var p = new ProgressData();
            var q = DailyQuests.Today(p, Day);
            q.Slots = new System.Collections.Generic.List<QuestSlot>
            {
                new QuestSlot { Id = "win3" }, new QuestSlot { Id = "runs1" }, new QuestSlot { Id = "ball2", Ball = "blade" },
            };
            var run = RunWith(true);
            DailyQuests.Record(p, run, Day);
            run = RunWith(true, true);
            var done = DailyQuests.Record(p, run, Day);
            Assert.That(done.Select(s => s.Id), Is.EqualTo(new[] { "ball2" }), "2 wins with EMBER");
            Assert.That(p.PendingChests, Is.EqualTo(new[] { ChestTier.Wood }));
            Assert.That(run.Achievements, Does.Contain(DailyQuests.QuestMark + "ball2"));
            run = RunWith(true, true, false);
            run.Over = true;
            done = DailyQuests.Record(p, run, Day);
            Assert.That(done.Select(s => s.Id), Is.EquivalentTo(new[] { "runs1" }));
            Assert.That(q.Slots[0].Progress, Is.EqualTo(2), "the lost fight is no win");
            run = RunWith(true);
            DailyQuests.Record(p, run, Day);
            Assert.That(DailyQuests.AllDone(q), Is.True);
            Assert.That(p.PendingChests, Is.EqualTo(new[] { ChestTier.Wood, ChestTier.Wood, ChestTier.Wood, DailyQuests.BonusChest }));
            DailyQuests.Record(p, RunWith(true), Day);
            Assert.That(p.PendingChests.Count, Is.EqualTo(4), "nothing more once done");
        }

        [Test]
        public void EveryBallIsOpenFromTheStart()
        {
            var open = UnlockRules.Weapons(new ProgressData());
            foreach (var w in BallBattle.Sim.Weapons.WeaponRegistry.All) Assert.That(open, Does.Contain(w.Id));
        }

        [Test]
        public void NewTraitsAreOfferedToThePlayerButNotRolledForEnemies()
        {
            var ids = new[] { BerserkerTrait.TraitId, IronSkinTrait.TraitId, RegenTrait.TraitId, ExecutionerTrait.TraitId, BankShotTrait.TraitId };
            var player = UnlockRules.Traits(new ProgressData());
            foreach (var id in ids)
            {
                Assert.That(TraitRegistry.Exists(id), Is.True, id);
                Assert.That(player, Does.Contain(id), id);
                Assert.That(TraitRegistry.Get(id).PlayerOnly, Is.True, id);
            }
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
            Assert.That(run.EnemyTraits.Intersect(ids), Is.Empty);
        }

        [Test]
        public void NewTraitsChangeDamageAsDescribed()
        {
            var berserk = new BerserkerTrait(1);
            var exec = new ExecutionerTrait(1);
            var bank = new BankShotTrait(1);
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 1, new[]
            {
                new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("blade")).With(berserk, exec, bank),
                new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("blade")),
            });
            var a = sim.Balls[0];
            var b = sim.Balls[1];
            Fix ten = 10;

            Assert.That(berserk.ModifyOutgoingDamage(b, ten, DamageKind.Weapon), Is.EqualTo(ten), "full HP: no bonus");
            a.Hp = a.MaxHp * Fix.Half;
            Assert.That(berserk.ModifyOutgoingDamage(b, ten, DamageKind.Weapon).ToFloat(), Is.EqualTo(12.5f).Within(0.01f), "half HP: +25%");

            var iron = new IronSkinTrait(1);
            Assert.That(iron.ModifyIncomingDamage(b, ten, DamageKind.Weapon).ToFloat(), Is.EqualTo(8.5f).Within(0.01f));
            Assert.That(iron.ModifyIncomingDamage(b, ten, DamageKind.Status), Is.EqualTo(ten), "poison is not reduced");

            Assert.That(exec.ModifyOutgoingDamage(b, ten, DamageKind.Weapon), Is.EqualTo(ten));
            b.Hp = b.MaxHp * Fix.Ratio(1, 4);
            Assert.That(exec.ModifyOutgoingDamage(b, ten, DamageKind.Weapon).ToFloat(), Is.EqualTo(13f).Within(0.01f));

            Assert.That(bank.ModifyOutgoingDamage(b, ten, DamageKind.Weapon), Is.EqualTo(ten));
            bank.OnWall();
            Assert.That(bank.ModifyOutgoingDamage(b, ten, DamageKind.Weapon).ToFloat(), Is.EqualTo(13f).Within(0.01f));
            bank.OnHitDealt(b, ten, DamageKind.Weapon);
            Assert.That(bank.Charged, Is.False, "spent by the hit");
        }

        [Test]
        public void CoinGlyphDrawsInItsOwnColours()
        {
            var font = PixelFont.Default;
            Assert.That(font.Has(PixelFont.Coin), Is.True);
            Assert.That(font.Get(PixelFont.Coin).Colored, Is.True);
            Assert.That(PixelFont.MeasureWidth("¤ 10"), Is.GreaterThan(PixelFont.MeasureWidth("10")));
        }
    }
}
