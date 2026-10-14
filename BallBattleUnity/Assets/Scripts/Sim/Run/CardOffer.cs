using BallBattle.Sim.Traits;
using System.Collections.Generic;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Seeded card offers. Same run seed + fight + reroll count + build + unlocks → same cards.
    /// Candidates are listed in a fixed order, then drawn by weight without replacement.
    /// </summary>
    public static class CardOffer
    {
        /// <summary>Whole trait category (split between offered traits), so many unlocked traits never crowd out the rest.</summary>
        static readonly Fix TraitWeightTotal = Fix.Ratio(15, 2);
        static readonly Fix StatWeight = 2;
        static readonly Fix SwapWeightTotal = Fix.Ratio(3, 2);
        static readonly Fix MaxHpWeight = 1;

        /// <summary>A weapon swap must not leave a held trait useless or banned on the new weapon.</summary>
        static bool KeepsAllTraits(RunBuild build, string weaponId)
        {
            foreach (var slot in build.Traits)
                if (!TraitRegistry.IsEligible(slot.Id, weaponId)) return false;
            return true;
        }

        public static Card[] Generate(uint runSeed, int fightIndex, int reroll, RunBuild build,
            IReadOnlyList<string> weapons, IReadOnlyList<string> traits, int? skillLevelOverride = null)
        {
            var cards = new List<Card>();
            var weights = new List<Fix>();
            void Add(Card c, Fix w) { cards.Add(c); weights.Add(w); }

            var traitCards = new List<Card>();
            foreach (var id in traits)
            {
                if (!TraitRegistry.IsEligible(id, build.WeaponId)) continue;
                var level = build.TraitLevel(id);
                if (level == 0 && build.Traits.Count < RunTuning.MaxTraits) traitCards.Add(new Card(CardKind.Trait, id, 1));
                else if (level > 0 && level < RunTuning.MaxTraitLevel) traitCards.Add(new Card(CardKind.Trait, id, level + 1));
            }
            foreach (var c in traitCards) Add(c, TraitWeightTotal / traitCards.Count);

            Add(new Card(CardKind.DamageUp), StatWeight);
            Add(new Card(CardKind.SpeedUp), StatWeight);

            var skillLevel = skillLevelOverride ?? build.SkillLevel;
            if (skillLevel < 3) Add(new Card(CardKind.SkillUp, build.WeaponId, skillLevel + 1), SwapWeightTotal);

            if (build.Hurt) Add(new Card(CardKind.Heal), build.Hp < FixMath.MulDiv(build.MaxHp, 6, 10) ? 3 : 1);
            Add(new Card(CardKind.MaxHp), MaxHpWeight);

            var rng = new SimRandom(RunTuning.Derive(runSeed, 1000 + fightIndex, reroll));
            var count = cards.Count < RunTuning.OfferSize ? cards.Count : RunTuning.OfferSize;
            var offer = new Card[count];
            for (var n = 0; n < count; n++)
            {
                var total = Fix.Zero;
                foreach (var w in weights) total += w;
                var r = rng.NextFix01() * total;
                var pick = weights.Count - 1;
                for (var i = 0; i < weights.Count; i++)
                {
                    r -= weights[i];
                    if (r < Fix.Zero) { pick = i; break; }
                }
                offer[n] = cards[pick];
                cards.RemoveAt(pick);
                weights.RemoveAt(pick);
            }
            return offer;
        }
    }
}
