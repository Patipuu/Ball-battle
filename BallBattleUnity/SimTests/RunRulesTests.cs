using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using NUnit.Framework;
using static BallBattle.SimTests.RunFixtures;

namespace BallBattle.SimTests
{
    /// <summary>Run choices: picks, rerolls, offers, cards, achievements, carried state.</summary>
    public class RunRulesTests
    {
        [Test]
        public void PicksCostNothingThenThreeCoinsAndStopAtTwo()
        {
            var run = NewRun();
            var offer = Enumerable.Range(0, run.OfferCount).Select(run.OfferCard).ToArray();
            Assert.That(offer.Length, Is.EqualTo(RunTuning.OfferSize));
            Assert.That(offer.Distinct().Count(), Is.EqualTo(offer.Length));

            run.Coins = 10;
            run.Pick(0);
            Assert.That(run.Coins, Is.EqualTo(10));
            Assert.That(Enumerable.Range(0, run.OfferCount).Select(run.OfferCard), Is.EqualTo(offer), "picking does not reshuffle the offer");
            Assert.That(run.CanPick(0), Is.False, "same card twice");
            Assert.That(run.CanReroll, Is.False, "no reroll after a pick");

            var second = Enumerable.Range(1, 2).First(run.CanPick);
            run.Pick(second);
            Assert.That(run.Coins, Is.EqualTo(10 - RunTuning.ExtraPickCost));
            Assert.That(run.NextPickCost, Is.EqualTo(-1));
            Assert.That(Enumerable.Range(0, run.OfferCount).Any(run.CanPick), Is.False);
        }

        [Test]
        public void RerollCostsACoinAndDealsNewCards()
        {
            var run = NewRun(31);
            var first = Enumerable.Range(0, run.OfferCount).Select(run.OfferCard).ToArray();
            var changed = false;
            run.Coins = 20;
            for (var i = 0; i < 5; i++)
            {
                run.Reroll();
                changed |= !Enumerable.Range(0, run.OfferCount).Select(run.OfferCard).SequenceEqual(first);
            }
            Assert.That(run.Coins, Is.EqualTo(15));
            Assert.That(changed, Is.True);
            run.Coins = 0;
            Assert.That(run.CanReroll, Is.False);
        }

        [Test]
        public void OffersRespectTraitSlotsAndMaxLevel()
        {
            var b = new RunBuild { WeaponId = "blade", Hp = 100f };
            var all = BallBattle.Sim.Traits.TraitRegistry.All.Select(t => t.Id).ToList();
            foreach (var id in all) b.Traits.Add(new TraitSlot(id, 2));
            for (var reroll = 0; reroll < 50; reroll++)
            {
                var offer = CardOffer.Generate(3, 0, reroll, b, UnlockRules.Weapons(Fresh), all);
                Assert.That(offer.Any(c => c.Kind == CardKind.Trait), Is.False, "all traits maxed");
                Assert.That(offer.Any(c => c.Kind == CardKind.Heal), Is.False, "full HP");
                Assert.That(offer.Any(c => c.Kind == CardKind.SwapWeapon && c.Id == "blade"), Is.False);
            }
        }

        [Test]
        public void SecondTraitPickIsBlockedWhenFirstUsedTheLastSlot()
        {
            var run = NewRun();
            run.Build.Traits.Add(new TraitSlot("a", 1));
            run.Build.Traits.Add(new TraitSlot("b", 1));
            run.OfferCards.Clear();
            run.OfferCards.Add(OfferSlot.From(new Card(CardKind.Trait, "heavy", 1)));
            run.OfferCards.Add(OfferSlot.From(new Card(CardKind.Trait, "spiky", 1)));
            run.OfferCards.Add(OfferSlot.From(new Card(CardKind.MaxHp)));
            run.Coins = 10;
            run.Pick(0);
            Assert.That(run.CanPick(1), Is.False, "3 trait slots max");
            Assert.That(run.CanPick(2), Is.True);
        }

        [Test]
        public void CardsChangeTheBuild()
        {
            var b = new RunBuild { WeaponId = "blade", Hp = 40f };
            new Card(CardKind.Heal).ApplyTo(b);
            Assert.That(b.Hp, Is.EqualTo(90f));
            new Card(CardKind.MaxHp).ApplyTo(b);
            Assert.That((b.MaxHp, b.Hp), Is.EqualTo((115f, 105f)));
            new Card(CardKind.Trait, "vampire", 1).ApplyTo(b);
            new Card(CardKind.Trait, "vampire", 2).ApplyTo(b);
            Assert.That(b.TraitLevel("vampire"), Is.EqualTo(2));
            Assert.That(b.Traits.Count, Is.EqualTo(1));
            new Card(CardKind.SwapWeapon, "pike").ApplyTo(b);
            new Card(CardKind.DamageUp).ApplyTo(b);
            Assert.That((b.WeaponId, b.DamagePct), Is.EqualTo(("pike", RunTuning.DamageCardPct)));
        }

        [Test]
        public void BossKillsAndRunWinsEarnAchievements()
        {
            var progress = new ProgressData();
            var run = RunState.Start(77, RunState.StartChoices(77, UnlockRules.Weapons(progress))[0], progress);
            var earned = new List<string>();
            for (var i = 0; i < RunTuning.Fights; i++)
            {
                run.ApplyResult(Forced(run, true));
                earned.AddRange(UnlockRules.Record(progress, run));
            }

            Assert.That(earned, Is.EqualTo(new[] { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Boss3, UnlockRules.Win1 }));
            Assert.That((progress.RunsPlayed, progress.RunsWon, progress.BestFight), Is.EqualTo((1, 1, 8)));
            Assert.That(run.Weapons, Is.EqualTo(UnlockRules.Weapons(new ProgressData())), "this run keeps its start snapshot");
        }

        [Test]
        public void HeavyKeepsTheHpFractionSoNoFreeHpCarriesOver()
        {
            var run = NewRun();
            run.Build.Hp = 30f;
            run.Build.Traits.Add(new TraitSlot("heavy", 2));
            var m = run.CreateMatch();
            Assert.That(m.Balls[0].MaxHp, Is.GreaterThan(100f), "spawn hooks already applied for the countdown");
            Assert.That(m.Balls[0].HpFraction, Is.EqualTo(0.3f).Within(1e-5f));
        }

        [Test]
        public void HpIsWholePointsAndHealIsOfferedOnlyWhenAPointIsMissing()
        {
            var run = NewRun();
            run.ApplyResult(Forced(run, true, 0.69999f));
            Assert.That(run.Build.Hp, Is.EqualTo(run.Build.MaxHp), "69.999 + 30 rounds to full, as the screen shows");
            Assert.That(run.Build.Hurt, Is.False);

            var b = new RunBuild { WeaponId = "blade", Hp = 99.5f };
            Assert.That(b.Hurt, Is.False);
            for (var reroll = 0; reroll < 50; reroll++)
                Assert.That(CardOffer.Generate(3, 1, reroll, b, UnlockRules.Weapons(Fresh), UnlockRules.Traits(Fresh)).Any(c => c.Kind == CardKind.Heal), Is.False);
        }

        [Test]
        public void SavedRunsNamingMissingContentAreInvalid()
        {
            Assert.That(RunValidation.IsValid(NewRun()), Is.True);
            var badWeapon = NewRun();
            badWeapon.Build.WeaponId = "removed-weapon";
            Assert.That(RunValidation.IsValid(badWeapon), Is.False);
            var badTrait = NewRun();
            badTrait.Build.Traits.Add(new TraitSlot("removed-trait", 1));
            Assert.That(RunValidation.IsValid(badTrait), Is.False);
            var badPool = NewRun();
            badPool.EnemyTraits.Add("removed-trait");
            Assert.That(RunValidation.IsValid(badPool), Is.False);
        }

        [Test]
        public void PlayerCarriedHpAndTraitsReachTheMatch()
        {
            var run = NewRun();
            run.Build.Hp = 60f;
            run.Build.Traits.Add(new TraitSlot("vampire", 2));
            var m = run.CreateMatch();
            Assert.That(m.Balls[0].Hp, Is.EqualTo(60f));
            Assert.That(m.Balls[0].Traits.Single().Level, Is.EqualTo(2));
        }
    }
}
