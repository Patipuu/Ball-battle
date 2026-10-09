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
        const float TraitWeightTotal = 7.5f;
        const float StatWeight = 2f;
        const float SwapWeightTotal = 1.5f;
        const float MaxHpWeight = 1f;

        /// <summary>A weapon swap must not leave a held trait useless or banned on the new weapon.</summary>
        static bool KeepsAllTraits(RunBuild build, string weaponId)
        {
            foreach (var slot in build.Traits)
                if (!TraitRegistry.IsEligible(slot.Id, weaponId)) return false;
            return true;
        }

        public static Card[] Generate(uint runSeed, int fightIndex, int reroll, RunBuild build,
            IReadOnlyList<string> weapons, IReadOnlyList<string> traits)
        {
            var cards = new List<Card>();
            var weights = new List<float>();
            void Add(Card c, float w) { cards.Add(c); weights.Add(w); }

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

            var swaps = new List<string>();
            foreach (var w in weapons)
                if (w != build.WeaponId && KeepsAllTraits(build, w)) swaps.Add(w);
            foreach (var w in swaps) Add(new Card(CardKind.SwapWeapon, w), SwapWeightTotal / swaps.Count);

            if (build.Hurt) Add(new Card(CardKind.Heal), build.Hp < build.MaxHp * 0.6f ? 3f : 1f);
            Add(new Card(CardKind.MaxHp), MaxHpWeight);

            var rng = new SimRandom(RunTuning.Derive(runSeed, 1000 + fightIndex, reroll));
            var count = cards.Count < RunTuning.OfferSize ? cards.Count : RunTuning.OfferSize;
            var offer = new Card[count];
            for (var n = 0; n < count; n++)
            {
                var total = 0f;
                foreach (var w in weights) total += w;
                var r = rng.NextFloat01() * total;
                var pick = weights.Count - 1;
                for (var i = 0; i < weights.Count; i++)
                {
                    r -= weights[i];
                    if (r < 0f) { pick = i; break; }
                }
                offer[n] = cards[pick];
                cards.RemoveAt(pick);
                weights.RemoveAt(pick);
            }
            return offer;
        }
    }
}
