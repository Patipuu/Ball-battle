using System;
using System.Collections.Generic;
using BallBattle.Sim.Run;

namespace BallBattle.Sim.Meta
{
    /// <summary>BB-12a: chest tiers. WOOD = the run ended in fights 1-4, SILVER = it beat the first boss, GOLD = it was won.</summary>
    public enum ChestTier { Wood = 0, Silver = 1, Gold = 2 }

    /// <summary>BB-12a: one ball's permanent progress (saved in ProgressData.Balls).</summary>
    [Serializable]
    public sealed class BallProgress
    {
        public string Id;
        public int Level = 1;
        /// <summary>Shards held toward the next level (spent by an upgrade).</summary>
        public int Shards;
        /// <summary>BB-12g skill tree (MetaTree checks and spends it): VITALITY, POWER, INSTINCT ranks 0..2.</summary>
        public int Vit, Pow, Growth;
        /// <summary>OPENER bought (2 points).</summary>
        public bool Start;
        /// <summary>Skill levels bought above LV1: 0 (skill LV1), 1 (LV2) or 2 (LV3), 1 point each.</summary>
        public int SkillUps;
        /// <summary>Skill branch: 0 none, 1 = A, 2 = B (set by the first skill level bought).</summary>
        public int Branch;
        /// <summary>FINALE of the branch bought (3 points).</summary>
        public bool Finale;
    }

    /// <summary>BB-12a: what one opened chest gave (also the data the CHEST screen shows). Already applied when returned.</summary>
    [Serializable]
    public sealed class ChestResult
    {
        public ChestTier Tier;
        /// <summary>Ball the run was played with ("" for a welcome chest: then all its shards are random).</summary>
        public string PlayedBall = "";
        /// <summary>Shards per ball, in the order they were drawn (the played ball first).</summary>
        public List<ShardGain> Shards = new List<ShardGain>();
        /// <summary>Gold from the chest itself.</summary>
        public int Gold;
        /// <summary>Gold from shards of balls already at the max level (MetaTuning.MaxedShardGold each).</summary>
        public int ConvertedGold;
        public int TotalGold => Gold + ConvertedGold;
    }

    [Serializable]
    public sealed class ShardGain
    {
        public string Ball;
        public int Count;
    }

    /// <summary>BB-12a numbers (docs/design/bb12-ball-progression.md §3-4). Change pacing here only.</summary>
    public static class MetaTuning
    {
        public const int MaxLevel = 10;
        /// <summary>Shards / gold to go from level L to L+1, index L-1 (LV1→2 … LV9→10).</summary>
        public static readonly int[] ShardCost = { 5, 10, 15, 25, 35, 50, 65, 85, 110 };
        public static readonly int[] GoldCost = { 10, 20, 35, 50, 70, 90, 120, 150, 190 };
        /// <summary>Per tier (Wood, Silver, Gold): shards for the played ball, random shards, gold.</summary>
        public static readonly int[] PlayedShards = { 4, 8, 15 };
        public static readonly int[] RandomShards = { 2, 6, 15 };
        public static readonly int[] ChestGold = { 20, 50, 120 };
        /// <summary>A shard of a max-level ball becomes this much gold.</summary>
        public const int MaxedShardGold = 2;
        /// <summary>Old saves (before v5): one SILVER welcome chest per run already won, at most this many.</summary>
        public const int MaxWelcomeChests = 5;
    }

    /// <summary>
    /// BB-12a meta progression: chest tier from a run, seeded chest draw, ball levels and upgrades. Pure C#, no Unity and no
    /// floating point; it never touches a match (the skill-tree points a level gives are spent elsewhere). A chest's draw comes
    /// from a seed made of the run seed and the number of chests opened before it, and the result is applied to the progress at
    /// once, so the saved progress already holds it: closing the game during the chest animation cannot re-roll it.
    /// </summary>
    public static class MetaRules
    {
        /// <summary>Skill-tree points a ball has at <paramref name="level"/>: one per level (LV1 = 1 … LV10 = 10).</summary>
        public static int TreePoints(int level) => Clamp(level, 1, MetaTuning.MaxLevel);

        /// <summary>The ball's progress row, created at LV1 with 0 shards when missing.</summary>
        public static BallProgress Ball(ProgressData p, string id)
        {
            if (p.Balls == null) p.Balls = new List<BallProgress>();
            foreach (var b in p.Balls)
                if (b.Id == id) return b;
            var row = new BallProgress { Id = id };
            p.Balls.Add(row);
            return row;
        }

        /// <summary>Level of a ball (1 when it has no row yet). Never creates a row.</summary>
        public static int LevelOf(ProgressData p, string id)
        {
            if (p?.Balls != null)
                foreach (var b in p.Balls)
                    if (b.Id == id) return Clamp(b.Level, 1, MetaTuning.MaxLevel);
            return 1;
        }

        public static int ShardsToNext(int level) => level >= 1 && level < MetaTuning.MaxLevel ? MetaTuning.ShardCost[level - 1] : 0;
        public static int GoldToNext(int level) => level >= 1 && level < MetaTuning.MaxLevel ? MetaTuning.GoldCost[level - 1] : 0;

        public static bool CanUpgrade(ProgressData p, string id)
        {
            var level = LevelOf(p, id);
            if (level >= MetaTuning.MaxLevel) return false;
            var shards = 0;
            if (p.Balls != null) foreach (var b in p.Balls) if (b.Id == id) shards = b.Shards;
            return shards >= ShardsToNext(level) && p.Gold >= GoldToNext(level);
        }

        /// <summary>Spend shards and gold for one level. False (nothing changes) when it cannot.</summary>
        public static bool Upgrade(ProgressData p, string id)
        {
            if (!CanUpgrade(p, id)) return false;
            var b = Ball(p, id);
            b.Shards -= ShardsToNext(b.Level);
            p.Gold -= GoldToNext(b.Level);
            b.Level++;
            return true;
        }

        /// <summary>
        /// Chest earned by a run: null when no fight was played. <paramref name="furthestWonFight"/> is the highest 0-based fight
        /// index the run won (-1 = none).
        /// </summary>
        public static ChestTier? TierFor(bool runWon, int furthestWonFight, int fightsPlayed)
        {
            if (fightsPlayed <= 0) return null;
            if (runWon) return ChestTier.Gold;
            return furthestWonFight >= RunTuning.BossFights[0] ? ChestTier.Silver : ChestTier.Wood;
        }

        public static ChestTier? TierFor(RunState run)
        {
            if (run == null) return null;
            var furthest = -1;
            foreach (var h in run.History) if (h.Won && h.FightIndex > furthest) furthest = h.FightIndex;
            return TierFor(run.Over && run.Won, furthest, run.History.Count);
        }

        /// <summary>The seed of the next chest: run seed mixed with how many chests were opened before.</summary>
        public static uint ChestSeed(uint runSeed, int chestsOpened) =>
            unchecked((runSeed ^ 0x9E3779B9u) * 2654435761u + (uint)(chestsOpened + 1) * 40503u);

        /// <summary>
        /// Open a chest now: draw it from (<paramref name="runSeed"/>, chests opened so far), apply it to <paramref name="p"/> and
        /// return what it gave. Random shards go to unlocked balls with equal odds (locked balls get none); shards of a max-level
        /// ball become gold.
        /// </summary>
        public static ChestResult Open(ProgressData p, ChestTier tier, string playedBall, uint runSeed)
        {
            var t = (int)tier;
            var rng = new SimRandom(ChestSeed(runSeed, p.ChestsOpened));
            var result = new ChestResult { Tier = tier, PlayedBall = playedBall ?? "", Gold = MetaTuning.ChestGold[t] };
            var unlocked = UnlockRules.Weapons(p);
            var played = MetaTuning.PlayedShards[t];
            var random = MetaTuning.RandomShards[t];
            if (string.IsNullOrEmpty(playedBall)) random += played;   // welcome chest: everything random
            else Add(result, playedBall, played);
            for (var i = 0; i < random && unlocked.Count > 0; i++) Add(result, unlocked[rng.RangeInt(0, unlocked.Count)], 1);
            foreach (var s in result.Shards)
            {
                var b = Ball(p, s.Ball);
                if (b.Level >= MetaTuning.MaxLevel) result.ConvertedGold += s.Count * MetaTuning.MaxedShardGold;
                else b.Shards += s.Count;
            }
            p.Gold += result.TotalGold;
            p.ChestsOpened++;
            return result;
        }

        static void Add(ChestResult r, string ball, int count)
        {
            foreach (var s in r.Shards)
                if (s.Ball == ball) { s.Count += count; return; }
            r.Shards.Add(new ShardGain { Ball = ball, Count = count });
        }

        /// <summary>The chest a finished run earns, opened at once (null when the run played no fight).</summary>
        public static ChestResult AwardRun(ProgressData p, RunState run)
        {
            var tier = TierFor(run);
            return tier.HasValue ? Open(p, tier.Value, run.Build?.WeaponId, run.Seed) : null;
        }

        /// <summary>Old saves: one SILVER chest per run already won (at most 5), waiting in PendingChests.</summary>
        public static void GrantWelcomeChests(ProgressData p)
        {
            if (p.PendingChests == null) p.PendingChests = new List<ChestTier>();
            var n = p.RunsWon < MetaTuning.MaxWelcomeChests ? p.RunsWon : MetaTuning.MaxWelcomeChests;
            for (var i = 0; i < n; i++) p.PendingChests.Add(ChestTier.Silver);
        }

        /// <summary>Open the first waiting chest (welcome chests), or null when none. <paramref name="seed"/> picks its draw.</summary>
        public static ChestResult OpenPending(ProgressData p, uint seed)
        {
            if (p.PendingChests == null || p.PendingChests.Count == 0) return null;
            var tier = p.PendingChests[0];
            p.PendingChests.RemoveAt(0);
            return Open(p, tier, "", seed);
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
