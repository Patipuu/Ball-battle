using System.Collections.Generic;
using BallBattle.Sim.Weapons;

namespace BallBattle.View
{
    /// <summary>
    /// Names the player sees for each weapon (art v1). Display only: weapon ids, saves and the sim's own
    /// WeaponRegistry.DisplayName are unchanged. An id missing here falls back to the registry name.
    /// </summary>
    public static class WeaponNames
    {
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            { BladeRule.WeaponId, "EMBER" },
            { FangRule.WeaponId, "MANTIS" },
            { PikeRule.WeaponId, "ANGLER" },
            { BrawlerRule.WeaponId, "WRECKER" },
            { VolleyRule.WeaponId, "SPARKLER" },
            { VenomRule.WeaponId, "SPORE" },
            { AegisRule.WeaponId, "MIRROR" },
            { RigRule.WeaponId, "HIVE" },
            { ChorusRule.WeaponId, "BGLGXLQ" },   // BB-14 tribute: Phu's church group
            { SerpentRule.WeaponId, "HUNTER" },   // BB-14 tribute: placeholder until Phu gives the friend's name
        };

        public static string Of(string weaponId) =>
            weaponId != null && Names.TryGetValue(weaponId, out var name) ? name : WeaponRegistry.Get(weaponId).DisplayName;

        /// <summary>True when every registered weapon has a display name here (tests).</summary>
        public static bool CoversAll()
        {
            foreach (var w in WeaponRegistry.All) if (!Names.ContainsKey(w.Id)) return false;
            return true;
        }
    }
}
