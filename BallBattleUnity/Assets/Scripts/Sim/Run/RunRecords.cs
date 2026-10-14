using System;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    [Serializable]
    public sealed class FightRecord
    {
        public int FightIndex;
        public bool Won;
        public bool Draw;
        public bool Retry;
        public string EnemyWeapon;
        public Fix HpAfter;
        /// <summary>BB-04: boss fought ("" or null = not a boss fight).</summary>
        public string BossId;
    }

    /// <summary>Serializable form of a Card (Card itself is an immutable struct).</summary>
    [Serializable]
    public sealed class OfferSlot
    {
        public CardKind Kind;
        public string Id;
        public int Level;

        public Card ToCard() => new Card(Kind, string.IsNullOrEmpty(Id) ? null : Id, Level);
        public static OfferSlot From(Card c) => new OfferSlot { Kind = c.Kind, Id = c.Id, Level = c.Level };
    }

    /// <summary>Checks a loaded run before resuming it (a save from an older build may name removed content).</summary>
    public static class RunValidation
    {
        /// <summary>Every id the run refers to still exists and its numbers are sane. Invalid runs are dropped, not resumed.</summary>
        public static bool IsValid(RunState run)
        {
            if (run == null || run.Build == null || run.Over) return false;
            if (run.FightIndex < 0 || run.FightIndex >= RunTuning.Fights || run.RetryCount < 0 || run.RetryCount > RunTuning.Retries) return false;
            if (!WeaponExists(run.Build.WeaponId) || run.Build.MaxHp <= Fix.Zero || run.Build.Hp <= Fix.Zero) return false;
            if (run.Build.Traits.Count > RunTuning.MaxTraits) return false;
            foreach (var t in run.Build.Traits)
                if (!TraitRegistry.Exists(t.Id) || t.Level < 1 || t.Level > RunTuning.MaxTraitLevel) return false;
            if (run.Weapons.Count == 0 || run.EnemyWeapons.Count == 0 || run.EnemyTraits.Count == 0) return false;
            // BB-04: one valid boss per slot, in its own slot; a forced (debug) boss must exist.
            if (run.BossIds == null || run.BossIds.Count != RunTuning.BossFights.Length) return false;
            for (var s = 0; s < run.BossIds.Count; s++)
                if (Boss.BossIds.SlotOf(run.BossIds[s]) != s) return false;
            if (!string.IsNullOrEmpty(run.ForcedBossId) && !Boss.BossIds.Exists(run.ForcedBossId)) return false;
            foreach (var w in run.Weapons) if (!WeaponExists(w)) return false;
            foreach (var w in run.EnemyWeapons) if (!WeaponExists(w)) return false;
            foreach (var t in run.Traits) if (!TraitRegistry.Exists(t)) return false;
            foreach (var t in run.EnemyTraits) if (!TraitRegistry.Exists(t)) return false;
            foreach (var o in run.OfferCards)
            {
                if (o.Kind == CardKind.Trait && !TraitRegistry.Exists(o.Id)) return false;
                if (o.Kind == CardKind.SwapWeapon && !WeaponExists(o.Id)) return false;
            }
            return true;
        }

        static bool WeaponExists(string id)
        {
            foreach (var w in WeaponRegistry.All)
                if (w.Id == id) return true;
            return false;
        }
    }
}
