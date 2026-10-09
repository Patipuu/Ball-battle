using System;
using System.Collections.Generic;

namespace BallBattle.Sim.Traits
{
    /// <summary>All traits. Adding a trait = one rule class + one entry here (+ tuning).</summary>
    public static class TraitRegistry
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string DisplayName;
            /// <summary>Short card text, 3x5 font friendly (A-Z 0-9 space + - % . / ! ?).</summary>
            public readonly string Blurb;
            /// <summary>True when the trait needs a blade (parry, second blade, poisoned tip); Run offers skip it on bladeless weapons.</summary>
            public readonly bool RequiresBlade;
            /// <summary>Weapon this trait is never offered with (null = none): a measured runaway combo.</summary>
            public readonly string BannedWeapon;
            readonly Func<int, TraitRule> factory;

            public Entry(string id, string displayName, string blurb, Func<int, TraitRule> factory, bool requiresBlade = false, string bannedWeapon = null)
            {
                BannedWeapon = bannedWeapon;
                RequiresBlade = requiresBlade;
                Id = id;
                DisplayName = displayName;
                Blurb = blurb;
                this.factory = factory;
            }

            /// <summary>New instance every call (one per ball per match).</summary>
            public TraitRule Create(int level) => factory(level);
        }

        static readonly Entry[] entries =
        {
            new Entry(HeavyTrait.TraitId, "HEAVY", "BIGGER TOUGHER SLOWER", l => new HeavyTrait(l)),
            new Entry(VampireTrait.TraitId, "VAMPIRE", "HEAL PART OF HITS", l => new VampireTrait(l)),
            new Entry(SpikyTrait.TraitId, "SPIKY", "BODY BUMPS HURT", l => new SpikyTrait(l)),
            new Entry(ThornsTrait.TraitId, "THORNS", "HITS HURT BACK", l => new ThornsTrait(l)),
            new Entry(SecondWindTrait.TraitId, "SECOND WIND", "HEAL ONCE WHEN LOW", l => new SecondWindTrait(l)),
            new Entry(GlassCannonTrait.TraitId, "GLASS CANNON", "MORE DMG LESS HP", l => new GlassCannonTrait(l), bannedWeapon: "brawler"),
            new Entry(ParryMasterTrait.TraitId, "PARRY MASTER", "PARRY GROWS WEAPON", l => new ParryMasterTrait(l), requiresBlade: true),
            new Entry(BubbleTrait.TraitId, "BUBBLE", "SHIELD EVERY FEW SEC", l => new BubbleTrait(l)),
            new Entry(TwinBladeTrait.TraitId, "TWIN BLADE", "SECOND BLADE BEHIND", l => new TwinBladeTrait(l), requiresBlade: true),
            new Entry(PoisonTipTrait.TraitId, "POISON TIP", "HITS POISON", l => new PoisonTipTrait(l), requiresBlade: true),
        };

        public static IReadOnlyList<Entry> All => entries;

        public static bool Exists(string id)
        {
            foreach (var e in entries)
                if (e.Id == id) return true;
            return false;
        }

        public static Entry Get(string id)
        {
            foreach (var e in entries)
                if (e.Id == id) return e;
            throw new ArgumentException($"Unknown trait id '{id}'", nameof(id));
        }

        /// <summary>False when the trait is useless or a measured runaway on that weapon (Parry Master on a bladeless body, Glass Cannon on Brawler).</summary>
        public static bool IsEligible(string traitId, string weaponId)
        {
            var e = Get(traitId);
            if (e.BannedWeapon == weaponId) return false;
            return !e.RequiresBlade || Weapons.WeaponRegistry.Create(weaponId).HasBlade;
        }

        public static TraitRule Create(string id, int level) => Get(id).Create(level);
    }
}
