using System;
using System.Collections.Generic;

namespace BallBattle.Sim.Pvp
{
    /// <summary>Server-owned public opponent data. No defense roster or blind-ban choice belongs here.</summary>
    public sealed class PvpCandidate
    {
        public string AccountId;
        public int Mmr;
        public int AverageDefenseLevel;
        public DateTime LastChallengedUtc;
        public bool Verified;
        /// <summary>House bot (PvpBots): only fills offers that real players cannot.</summary>
        public bool IsBot;
    }

    /// <summary>
    /// Selects three different eligible opponents. Real players come first: bots (PvpBots, named "BOT ...") only fill the offers
    /// that nearby real players cannot, and when even the nearby bots run out, the nearest bots anywhere fill in.
    /// </summary>
    public static class PvpMatchmaking
    {
        static readonly int[] MmrRanges = { 150, 300, 500 };
        static readonly int[] LevelRanges = { 2, 3, 4 };

        public static IReadOnlyList<PvpCandidate> ThreeOffers(string selfId, int selfMmr, int averageLevel,
            DateTime nowUtc, IEnumerable<PvpCandidate> candidates)
        {
            if (string.IsNullOrEmpty(selfId) || candidates == null) throw new ArgumentException("Missing matchmaking input.");
            var pool = new List<PvpCandidate>();
            var ids = new HashSet<string>();
            foreach (var candidate in candidates)
            {
                if (candidate == null || !candidate.Verified || string.IsNullOrEmpty(candidate.AccountId) ||
                    candidate.AccountId == selfId || !ids.Add(candidate.AccountId) ||
                    candidate.LastChallengedUtc > nowUtc.AddHours(-24)) continue;
                pool.Add(candidate);
            }

            var humans = pool.FindAll(c => !c.IsBot);
            var picked = Pick(humans, selfMmr, averageLevel, false);
            if (picked.Count == 3) return picked;
            picked = Pick(pool, selfMmr, averageLevel, false);   // humans still sort first inside each band
            if (picked.Count == 3) return picked;
            return pool.Count >= 3 ? Pick(pool, selfMmr, averageLevel, true) : Array.Empty<PvpCandidate>();
        }

        /// <summary>Three from the first band holding three; <paramref name="anyBand"/> = no MMR / level limit at all.</summary>
        static IReadOnlyList<PvpCandidate> Pick(List<PvpCandidate> pool, int selfMmr, int averageLevel, bool anyBand)
        {
            for (var band = 0; band < MmrRanges.Length; band++)
            {
                var eligible = new List<PvpCandidate>();
                foreach (var candidate in pool)
                    if (anyBand || (Math.Abs(candidate.Mmr - selfMmr) <= MmrRanges[band] &&
                        Math.Abs(candidate.AverageDefenseLevel - averageLevel) <= LevelRanges[band]))
                        eligible.Add(candidate);
                if (eligible.Count < 3) continue;
                eligible.Sort((a, b) =>
                {
                    if (a.IsBot != b.IsBot) return a.IsBot ? 1 : -1;
                    var diff = Math.Abs(a.Mmr - selfMmr).CompareTo(Math.Abs(b.Mmr - selfMmr));
                    if (diff != 0) return diff;
                    diff = Math.Abs(a.AverageDefenseLevel - averageLevel).CompareTo(Math.Abs(b.AverageDefenseLevel - averageLevel));
                    return diff != 0 ? diff : string.CompareOrdinal(a.AccountId, b.AccountId);
                });
                // Real players found here are all kept (fewer than three, or the human-only pass would have returned);
                // bots then spread over the missing lower / even / higher slots.
                var result = new List<PvpCandidate>(3);
                foreach (var candidate in eligible)
                    if (!candidate.IsBot && eligible.Exists(c => c.IsBot) && result.Count < 3) result.Add(candidate);
                AddFirst(eligible, result, c => c.Mmr < selfMmr - 50);
                AddFirst(eligible, result, c => Math.Abs(c.Mmr - selfMmr) <= 50);
                AddFirst(eligible, result, c => c.Mmr > selfMmr + 50);
                foreach (var candidate in eligible)
                {
                    if (result.Count == 3) break;
                    if (!result.Contains(candidate)) result.Add(candidate);
                }
                return result;
            }
            return Array.Empty<PvpCandidate>();
        }

        /// <summary>Adds the first candidate of that kind unless the result is full or already holds one of that kind.</summary>
        static void AddFirst(List<PvpCandidate> eligible, List<PvpCandidate> result, Func<PvpCandidate, bool> condition)
        {
            if (result.Count >= 3 || result.Exists(c => condition(c))) return;
            foreach (var candidate in eligible)
                if (condition(candidate) && !result.Contains(candidate)) { result.Add(candidate); return; }
        }
    }
}
