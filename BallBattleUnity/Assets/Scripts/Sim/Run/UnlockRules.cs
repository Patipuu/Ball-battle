using System;
using System.Collections.Generic;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>Long-term progress (saved). Unlocks are derived from achievements, so the table can change later.</summary>
    [Serializable]
    public sealed class ProgressData
    {
        public List<string> Achievements = new List<string>();
        public int RunsPlayed;
        public int RunsWon;
        /// <summary>Furthest fight reached (1-based); 0 = never played.</summary>
        public int BestFight;

        public bool Has(string achievement) => Achievements.Contains(achievement);
    }

    /// <summary>
    /// What is unlocked. Start: the 4 Step-1 weapons + 5 traits. Achievements unlock the rest.
    /// Ids not implemented yet are skipped (content arrives in later phases), so the table is stable.
    /// </summary>
    public static class UnlockRules
    {
        public const string Boss1 = "boss1", Boss2 = "boss2", Boss3 = "boss3", Win1 = "win1", Win3 = "win3";

        static readonly string[] StartWeapons = { "blade", "fang", "pike", "brawler" };
        static readonly string[] StartTraits = { "heavy", "vampire", "spiky", "thorns", "second-wind" };

        /// <summary>achievement → (weapons, traits) it unlocks.</summary>
        static readonly (string achievement, string[] weapons, string[] traits)[] Table =
        {
            (Boss1, new string[0], new[] { "glass-cannon" }),
            (Boss2, new[] { "volley" }, new[] { "poison-tip" }),
            (Boss3, new[] { "venom" }, new[] { "parry-master" }),
            (Win1, new[] { "aegis" }, new[] { "bubble" }),
            (Win3, new[] { "rig" }, new[] { "twin-blade" }),
        };

        public static List<string> Weapons(ProgressData p) => Collect(p, StartWeapons, true);
        public static List<string> Traits(ProgressData p) => Collect(p, StartTraits, false);

        static List<string> Collect(ProgressData p, string[] start, bool weapons)
        {
            var result = new List<string>();
            void Add(string id)
            {
                var exists = weapons ? WeaponExists(id) : TraitRegistry.Exists(id);
                if (exists && !result.Contains(id)) result.Add(id);
            }
            foreach (var id in start) Add(id);
            foreach (var row in Table)
                if (p.Has(row.achievement))
                    foreach (var id in weapons ? row.weapons : row.traits) Add(id);
            return result;
        }

        static bool WeaponExists(string id)
        {
            foreach (var e in WeaponRegistry.All)
                if (e.Id == id) return true;
            return false;
        }

        /// <summary>
        /// Call once after every RunState.ApplyResult: updates stats and achievements, returns the achievements
        /// earned now (the view can announce what they unlocked).
        /// </summary>
        public static List<string> Record(ProgressData p, RunState run)
        {
            var earned = new List<string>();
            void Earn(string a)
            {
                if (p.Has(a)) return;
                p.Achievements.Add(a);
                run.Achievements.Add(a);
                earned.Add(a);
            }

            var last = run.History.Count > 0 ? run.History[run.History.Count - 1] : null;
            if (last != null)
            {
                if (last.FightIndex + 1 > p.BestFight) p.BestFight = last.FightIndex + 1;
                if (last.Won && last.FightIndex == RunTuning.BossFights[0]) Earn(Boss1);
                if (last.Won && last.FightIndex == RunTuning.BossFights[1]) Earn(Boss2);
                if (last.Won && last.FightIndex == RunTuning.BossFights[2]) Earn(Boss3);
            }

            if (run.Over)
            {
                p.RunsPlayed++;
                if (run.Won)
                {
                    p.RunsWon++;
                    Earn(Win1);
                    if (p.RunsWon >= 3) Earn(Win3);
                }
            }
            return earned;
        }

        /// <summary>A saved run replaced by a new one still counts as played (and lost).</summary>
        public static void RecordAbandoned(ProgressData p) => p.RunsPlayed++;

        /// <summary>Display names of the weapons/traits an achievement unlocks that exist in this build.</summary>
        public static List<string> UnlockedNames(string achievement)
        {
            var names = new List<string>();
            foreach (var row in Table)
            {
                if (row.achievement != achievement) continue;
                foreach (var w in row.weapons)
                    foreach (var e in WeaponRegistry.All)
                        if (e.Id == w) names.Add(e.DisplayName);
                foreach (var t in row.traits)
                    if (TraitRegistry.Exists(t)) names.Add(TraitRegistry.Get(t).DisplayName);
            }
            return names;
        }
    }
}
