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
            readonly Func<int, TraitRule> factory;

            public Entry(string id, string displayName, string blurb, Func<int, TraitRule> factory)
            {
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

        public static TraitRule Create(string id, int level) => Get(id).Create(level);
    }
}
