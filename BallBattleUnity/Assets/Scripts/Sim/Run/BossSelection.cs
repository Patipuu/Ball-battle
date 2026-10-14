using System.Collections.Generic;
using BallBattle.Sim.Boss;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Which boss each Run boss slot gets (BB-04). Decided once when the run starts and stored in RunState.BossIds, so the preview,
    /// the fight and a retry always agree and a saved run keeps its bosses. Rules:
    /// the very first run on a save gets the first boss of each slot (SHELL KING + MIMIC); later runs never repeat the previous run's
    /// boss in the same slot (ProgressData.LastBoss) and pick among the rest from the run seed (RunTuning.Derive with Salt).
    /// </summary>
    public static class BossSelection
    {
        public const int Salt = 5000;

        /// <summary>No run played and no boss remembered yet.</summary>
        public static bool IsFirstRun(ProgressData p)
        {
            if (p == null) return true;
            if (p.RunsPlayed > 0) return false;
            if (p.LastBoss != null)
                foreach (var id in p.LastBoss)
                    if (!string.IsNullOrEmpty(id)) return false;
            return true;
        }

        public static List<string> Choose(uint seed, ProgressData p)
        {
            var first = IsFirstRun(p);
            var ids = new List<string>();
            for (var slot = 0; slot < BossIds.Slots.Length; slot++)
            {
                var pool = BossIds.Slots[slot];
                if (first)
                {
                    ids.Add(pool[0]);
                    continue;
                }
                var last = p.LastBossOf(slot);
                var candidates = new List<string>();
                foreach (var id in pool)
                    if (id != last) candidates.Add(id);
                if (candidates.Count == 0) candidates.AddRange(pool);
                ids.Add(candidates[(int)(RunTuning.Derive(seed, Salt + slot, 0) % (uint)candidates.Count)]);
            }
            return ids;
        }

        /// <summary>
        /// Call once when a new run starts: its natural bosses become "last boss" per slot. A boss forced by -bbForceBoss lives in
        /// RunState.ForcedBossId, never in BossIds, so it is never remembered.
        /// </summary>
        public static void Remember(ProgressData p, RunState run)
        {
            if (p == null || run == null) return;
            for (var slot = 0; slot < run.BossIds.Count; slot++) p.SetLastBoss(slot, run.BossIds[slot]);
        }
    }
}
