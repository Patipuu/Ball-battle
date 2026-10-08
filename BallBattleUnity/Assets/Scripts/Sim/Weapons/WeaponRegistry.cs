using System;
using System.Collections.Generic;

namespace BallBattle.Sim.Weapons
{
    /// <summary>All selectable weapons. Adding a weapon = one rule class + one entry here.</summary>
    public static class WeaponRegistry
    {
        public readonly struct Entry
        {
            public readonly string Id;
            public readonly string DisplayName;
            readonly Func<WeaponRule> factory;

            public Entry(string id, string displayName, Func<WeaponRule> factory)
            {
                Id = id;
                DisplayName = displayName;
                this.factory = factory;
            }

            /// <summary>New instance every call (one per ball per match).</summary>
            public WeaponRule Create() => factory();
        }

        static readonly Entry[] entries =
        {
            new Entry(BladeRule.WeaponId, "BLADE", () => new BladeRule()),
            new Entry(FangRule.WeaponId, "FANG", () => new FangRule()),
            new Entry(PikeRule.WeaponId, "PIKE", () => new PikeRule()),
            new Entry(BrawlerRule.WeaponId, "BRAWLER", () => new BrawlerRule()),
        };

        public static IReadOnlyList<Entry> All => entries;

        public static Entry Get(string id)
        {
            foreach (var e in entries)
                if (e.Id == id) return e;
            throw new ArgumentException($"Unknown weapon id '{id}'", nameof(id));
        }

        public static WeaponRule Create(string id) => Get(id).Create();
    }
}
