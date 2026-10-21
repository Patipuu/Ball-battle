using System.Collections.Generic;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Pvp
{
    /// <summary>One house bot's published defense (server-owned, deterministic from its index).</summary>
    public sealed class PvpBotSpec
    {
        public string Id;
        public string Name;
        public int Mmr;
        /// <summary>Target defense level (the roster's balls sit at this level, give or take one).</summary>
        public int Level;
        public List<BallProgress> Balls = new List<BallProgress>();
        public List<string> Roster = new List<string>();
        public string BanBall, BanArena;
    }

    /// <summary>
    /// House bots for ranked PvP (Phu 21/10: "nhả bot vào PvP"). They fill the three offers while there are not enough real
    /// players near you, so PvP is playable from day one. Every bot is named "BOT ..." so players always know. A bot is a defense
    /// only: it never challenges, its MMR never moves, and offers prefer real players (PvpMatchmaking).
    /// Ladder: <see cref="PerLevel"/> bots for each ball level 1..10, MMR rising with the level, so a player at any level and
    /// MMR finds three nearby bots. Rosters, trees and blind bans are rolled from the bot index (same bots after a restart).
    /// </summary>
    public static class PvpBots
    {
        public const string IdPrefix = "bot-";
        public const string NamePrefix = "BOT ";
        public const int PerLevel = 4;
        public const int Count = PerLevel * MetaTuning.MaxLevel;
        public const int BaseMmr = 900, MmrPerLevel = 80;
        static readonly int[] MmrOffsets = { -60, -20, 20, 60 };

        static readonly string[] Names =
        {
            "PEBBLE", "RUSTY", "ZIGZAG", "BOUNCER", "SPARK", "NIBBLER", "TUMBLE", "DASH", "BOLT", "ROCKET",
            "IRONJAW", "BRISTLE", "COMET", "RATTLE", "VORTEX", "HAMMER", "FLINT", "TORNADO", "QUAKE", "NOVA",
        };

        public static bool IsBot(string accountId) => accountId != null && accountId.StartsWith(IdPrefix);

        public static IReadOnlyList<PvpBotSpec> All()
        {
            var list = new List<PvpBotSpec>(Count);
            for (var i = 0; i < Count; i++) list.Add(Make(i));
            return list;
        }

        public static PvpBotSpec Make(int index)
        {
            var level = 1 + index / PerLevel;
            var slot = index % PerLevel;
            var rng = new SimRandom(0xB0750000u + (uint)index * 2654435761u);
            var spec = new PvpBotSpec
            {
                Id = IdPrefix + (index + 1).ToString("00"),
                Name = NamePrefix + Names[index % Names.Length] + " " + level,
                Mmr = BaseMmr + (level - 1) * MmrPerLevel + MmrOffsets[slot],
                Level = level,
            };

            var pool = new List<string>();
            foreach (var w in WeaponRegistry.All) if (!WeaponRegistry.IsPlayerOnly(w.Id)) pool.Add(w.Id);
            // Roster: three distinct balls; levels level-1 .. level+1 (clamped), the first one at the target level.
            for (var r = 0; r < 3; r++)
            {
                var pick = pool[rng.RangeInt(0, pool.Count)];
                pool.Remove(pick);
                var ballLevel = r == 0 ? level : Clamp(level + rng.RangeInt(-1, 2), 1, MetaTuning.MaxLevel);
                spec.Balls.Add(Tree(pick, ballLevel, rng));
                spec.Roster.Add(pick);
            }
            spec.BanBall = pool[rng.RangeInt(0, pool.Count)];
            var arenas = ArenaRegistry.All;
            spec.BanArena = arenas[rng.RangeInt(0, arenas.Count)].Id;
            return spec;
        }

        /// <summary>A legal tree spending the ball's points the way a player would: root and skill first, then OPENER and FINALE.</summary>
        static BallProgress Tree(string id, int level, SimRandom rng)
        {
            var b = new BallProgress { Id = id, Level = level };
            var skill = rng.NextBool() ? TreeNode.SkillA : TreeNode.SkillB;
            var order = new[]
            {
                TreeNode.Instinct, TreeNode.Power, skill, TreeNode.Vitality, TreeNode.Opener, TreeNode.Power,
                TreeNode.Vitality, skill, TreeNode.Instinct, TreeNode.Finale,
            };
            for (var pass = 0; pass < 3; pass++)   // each pass buys every node it can, in order; later passes spend what is left
                foreach (var n in order) MetaTree.Add(b, n);
            return b;
        }

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
