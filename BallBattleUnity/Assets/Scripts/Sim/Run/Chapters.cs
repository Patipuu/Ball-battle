using System.Collections.Generic;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>One PvE chapter: a fixed boss pair (fight 4 and fight 8), its arenas, its enemy balls and a difficulty step.</summary>
    public sealed class ChapterDef
    {
        public readonly int Index;
        /// <summary>English source name (ui-vi translates it).</summary>
        public readonly string Name;
        /// <summary>Boss of slot 0 (fight 4) and slot 1 (fight 8).</summary>
        public readonly string[] Bosses;
        public readonly string[] Arenas;
        /// <summary>Normal enemies roll from these (player-only balls such as BGLGXLQ are never listed).</summary>
        public readonly string[] EnemyWeapons;
        /// <summary>Added to every enemy of the chapter, bosses included: max HP and damage.</summary>
        public readonly Fix HpPct, DamagePct;
        /// <summary>One-line background concept (English source; ui-vi translates it). The fight backdrop is drawn per chapter too.</summary>
        public readonly string Lore;

        public ChapterDef(int index, string name, string boss1, string boss2, string[] arenas, string[] enemyWeapons, int hpPct, int damagePct, string lore = "")
        {
            Lore = lore;
            Index = index; Name = name; Bosses = new[] { boss1, boss2 }; Arenas = arenas; EnemyWeapons = enemyWeapons;
            HpPct = Fix.Ratio(hpPct, 100); DamagePct = Fix.Ratio(damagePct, 100);
        }
    }

    /// <summary>
    /// PvE chapters (Phu 21/10): four chapters, each a boss pair; the normal enemies are random but drawn from the chapter's own balls
    /// and arenas. Chapter 1 is the balance reference (a LV1 ball clears it in about 5-6 runs); each later chapter adds enemy HP and
    /// damage on top of the run's stage ramp. Clearing a chapter (winning its run) unlocks the next.
    /// Measured with LV1 balls (SimTests BotBalanceProbe, Skilled bot): chapter 1 20.1% run wins (N=800), 2: 7.8%, 3: 6.3%, 4: 2.8%
    /// (N=600). Later chapters expect a levelled ball; re-measure them with levelled bots before launch.
    /// </summary>
    public static class Chapters
    {
        public const string Mark = "chapter:";

        static readonly ChapterDef[] all =
        {
            new ChapterDef(0, "TRAINING GROUND", BossIds.ShellKing, BossIds.Mimic,
                new[] { ArenaRegistry.Classic, ArenaRegistry.Pillar, ArenaRegistry.Wide },
                new[] { BladeRule.WeaponId, FangRule.WeaponId, PikeRule.WeaponId, BrawlerRule.WeaponId }, 5, 0,
                "WHERE EVERY BALL LEARNS ITS FIRST HIT"),
            new ChapterDef(1, "IRON FORT", BossIds.Magnet, BossIds.Warden,
                new[] { ArenaRegistry.Bumpers, ArenaRegistry.SpikeWalls, ArenaRegistry.Tight },
                new[] { BrawlerRule.WeaponId, AegisRule.WeaponId, RigRule.WeaponId, VolleyRule.WeaponId, PikeRule.WeaponId }, 15, 10,
                "STEEL WALLS, SPIKES AND A STRICT WARDEN"),
            new ChapterDef(2, "EMBER WOODS", BossIds.Hunter, BossIds.Ember,
                new[] { ArenaRegistry.LowGravity, ArenaRegistry.Wide, ArenaRegistry.Pillar },
                new[] { SerpentRule.WeaponId, VenomRule.WeaponId, FangRule.WeaponId, BladeRule.WeaponId, VolleyRule.WeaponId }, 35, 25,
                "A FOREST THAT NEVER STOPS BURNING"),
            new ChapterDef(3, "BELL TOWER", BossIds.Bell, BossIds.Prism,
                new[] { ArenaRegistry.Classic, ArenaRegistry.Pillar, ArenaRegistry.Bumpers, ArenaRegistry.SpikeWalls, ArenaRegistry.LowGravity, ArenaRegistry.Tight, ArenaRegistry.Wide },
                BotWeapons(), 45, 30,
                "THE LAST BELL AT THE TOP OF THE TOWER"),
        };

        public static int Count => all.Length;
        public static IReadOnlyList<ChapterDef> All => all;

        public static ChapterDef Get(int index) => all[index < 0 ? 0 : index >= all.Length ? all.Length - 1 : index];

        /// <summary>Every ball a bot may use (the tribute ball BGLGXLQ is for players only).</summary>
        public static string[] BotWeapons()
        {
            var list = new List<string>();
            foreach (var w in WeaponRegistry.All) if (!WeaponRegistry.IsPlayerOnly(w.Id)) list.Add(w.Id);
            return list.ToArray();
        }

        /// <summary>Highest chapter index the player may start (0-based). Old saves with a won run have chapter 2 open.</summary>
        public static int Unlocked(ProgressData p)
        {
            if (p == null) return 0;
            var cleared = p.ChaptersCleared;
            if (cleared == 0 && p.RunsWon > 0) cleared = 1;
            return cleared >= all.Length ? all.Length - 1 : cleared;
        }

        /// <summary>True when chapter <paramref name="index"/> has been cleared once.</summary>
        public static bool Cleared(ProgressData p, int index) => p != null && (index < p.ChaptersCleared || (index == 0 && p.RunsWon > 0));

        /// <summary>Call when a run is won: clearing the newest chapter opens the next one (marked in the run's achievements).</summary>
        public static void RecordWin(ProgressData p, RunState run)
        {
            if (p == null || run == null || !run.Won) return;
            var cleared = p.ChaptersCleared == 0 && p.RunsWon > 1 ? 1 : p.ChaptersCleared;   // RunsWon already counts this run
            if (run.Chapter >= cleared)
            {
                p.ChaptersCleared = run.Chapter + 1;
                if (run.Chapter + 1 < all.Length) run.Achievements.Add(Mark + (run.Chapter + 1));
            }
            else p.ChaptersCleared = cleared;
        }
    }

    /// <summary>
    /// HEAT (Phu 21/10): three difficulty steps inside every chapter. Heat 1 is the chapter as balanced (base rules). Winning a
    /// chapter's run at heat n opens heat n+1 of that chapter (and heat 1 of the next chapter, see Chapters.RecordWin).
    /// Heat 2: normal foes +1 trait, every foe +10% HP. Heat 3: heat 2, no retry, bosses +25% HP and +15% damage on top.
    /// Rewards: the run's chest is one tier higher per heat step (capped at GOLD) and gives +50% gold per heat step.
    /// Heat indexes here are 0-based (0 = HEAT 1); old saved runs load 0.
    /// </summary>
    public static class HeatRules
    {
        public const int Count = 3;
        public const string Mark = "heat:";

        public static int Clamp(int heat) => heat < 0 ? 0 : heat >= Count ? Count - 1 : heat;
        /// <summary>Extra random traits of every normal (non-boss) foe.</summary>
        public static int ExtraTraits(int heat) => heat >= 1 ? 1 : 0;
        /// <summary>Extra max HP of every foe, bosses included.</summary>
        public static Fix HpPct(int heat) => heat >= 1 ? Fix.Ratio(10, 100) : Fix.Zero;
        public static Fix BossHpPct(int heat) => heat >= 2 ? Fix.Ratio(25, 100) : Fix.Zero;
        public static Fix BossDamagePct(int heat) => heat >= 2 ? Fix.Ratio(15, 100) : Fix.Zero;
        /// <summary>Retries of a run at this heat (heat 3 has none).</summary>
        public static int Retries(int heat) => heat >= 2 ? 0 : RunTuning.Retries;
        /// <summary>Chest tier steps and gold bonus (percent) of a run at this heat.</summary>
        public static int ChestTierBonus(int heat) => Clamp(heat);
        public static int ChestGoldBonusPct(int heat) => Clamp(heat) * 50;

        /// <summary>Heats cleared in <paramref name="chapter"/> (0..Count). A chapter cleared before heat existed counts heat 1.</summary>
        public static int Cleared(ProgressData p, int chapter)
        {
            if (p == null || chapter < 0) return 0;
            var n = p.ChapterHeat != null && chapter < p.ChapterHeat.Count ? p.ChapterHeat[chapter] : 0;
            if (n < 1 && Chapters.Cleared(p, chapter)) n = 1;
            return n > Count ? Count : n;
        }

        /// <summary>Highest heat index the player may start in <paramref name="chapter"/>.</summary>
        public static int Unlocked(ProgressData p, int chapter)
        {
            var n = Cleared(p, chapter);
            return n >= Count ? Count - 1 : n;
        }

        /// <summary>Call when a run is won: records the heat cleared in its chapter; a newly opened heat is marked in the run's achievements.</summary>
        public static void RecordWin(ProgressData p, RunState run)
        {
            if (p == null || run == null || !run.Won) return;
            var before = Cleared(p, run.Chapter);
            if (p.ChapterHeat == null) p.ChapterHeat = new List<int>();
            while (p.ChapterHeat.Count <= run.Chapter) p.ChapterHeat.Add(0);
            var now = run.Heat + 1 > before ? run.Heat + 1 : before;
            p.ChapterHeat[run.Chapter] = now;
            if (now > before && now < Count) run.Achievements.Add(Mark + run.Chapter + ":" + now);
        }
    }
}
