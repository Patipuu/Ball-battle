using System;
using System.Collections.Generic;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>Long-term progress (saved). Unlocks are derived from achievements, so the table can change later.</summary>
    [Serializable]
    public sealed class OnboardingState
    {
        /// <summary>0: first launch; 1: a run has started; 2: the first run has ended.</summary>
        public int MenuStage;
        public bool StrikeLearned;
        public bool HintsOff;
        public List<string> SeenHints = new List<string>();

        public bool HasSeen(string id) => SeenHints != null && SeenHints.Contains(id);
        public void MarkSeen(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            if (SeenHints == null) SeenHints = new List<string>();
            if (!SeenHints.Contains(id)) SeenHints.Add(id);
        }
        public void AdvanceTo(int stage)
        {
            if (stage > 2) stage = 2;
            if (stage > MenuStage) MenuStage = stage;
        }

        public static OnboardingState FromLegacy(int runsPlayed, bool hasRun, List<string> seenTips)
        {
            var state = new OnboardingState { MenuStage = runsPlayed > 0 ? 2 : hasRun ? 1 : 0 };
            if (seenTips != null) foreach (var id in seenTips) state.MarkSeen(id);
            return state;
        }
    }

    [Serializable]
    public sealed class ProgressData
    {
        public List<string> Achievements = new List<string>();
        public int RunsPlayed;
        public int RunsWon;
        /// <summary>Furthest fight reached (1-based); 0 = never played.</summary>
        public int BestFight;
        /// <summary>BB-04: boss id the last run had in each boss slot ("" = none yet). Index = slot (0 = fight 4, 1 = fight 8).</summary>
        public List<string> LastBoss = new List<string>();
        /// <summary>BB-04 Boss Codex: one entry per boss met so far.</summary>
        public List<BossCodexEntry> BossCodex = new List<BossCodexEntry>();
        /// <summary>UI-TUT1: first-run tips already shown (TutorialCoach ids). Old saves load it empty.</summary>
        public List<string> SeenTips = new List<string>();
        /// <summary>TUT-2: first-launch menu stages, strike lesson and non-blocking hints.</summary>
        public OnboardingState Onboarding = new OnboardingState();
        /// <summary>BB-12a (save v5): per-ball level and shards; old saves load it empty (every ball LV1).</summary>
        public List<Meta.BallProgress> Balls = new List<Meta.BallProgress>();
        /// <summary>BB-12a: gold for ball upgrades.</summary>
        public int Gold;
        /// <summary>BB-12a: chests opened so far (part of the next chest's seed).</summary>
        public int ChestsOpened;
        /// <summary>BB-12a: earned chests not opened yet (welcome chests for old saves).</summary>
        public List<Meta.ChestTier> PendingChests = new List<Meta.ChestTier>();
        /// <summary>Daily quests (DailyQuests). Old saves load it empty: the day's quests are rolled on first use.</summary>
        public DailyQuestState Daily = new DailyQuestState();
        /// <summary>PvE chapters won at least once (0 = none; chapter n+1 opens when n is cleared). See Chapters.</summary>
        public int ChaptersCleared;
        /// <summary>HEAT steps cleared per chapter (index = chapter; 0..HeatRules.Count). Old saves load it empty. See HeatRules.</summary>
        public List<int> ChapterHeat = new List<int>();

        public bool Has(string achievement) => Achievements.Contains(achievement);

        public string LastBossOf(int slot) => LastBoss != null && slot >= 0 && slot < LastBoss.Count ? LastBoss[slot] ?? "" : "";

        public void SetLastBoss(int slot, string id)
        {
            if (LastBoss == null) LastBoss = new List<string>();
            while (LastBoss.Count <= slot) LastBoss.Add("");
            LastBoss[slot] = id ?? "";
        }

        /// <summary>Codex entry of a boss, or null when never met.</summary>
        public BossCodexEntry Codex(string bossId)
        {
            if (BossCodex == null) return null;
            foreach (var e in BossCodex)
                if (e.Id == bossId) return e;
            return null;
        }

        public bool BossMet(string bossId) => Codex(bossId)?.Met == true;
        public bool BossDefeated(string bossId) => Codex(bossId)?.Defeated == true;

        /// <summary>Record a fight against <paramref name="bossId"/>: met, and defeated when <paramref name="won"/>.</summary>
        public void RecordBoss(string bossId, bool won)
        {
            if (string.IsNullOrEmpty(bossId)) return;
            if (BossCodex == null) BossCodex = new List<BossCodexEntry>();
            var e = Codex(bossId);
            if (e == null) BossCodex.Add(e = new BossCodexEntry { Id = bossId });
            e.Met = true;
            if (won) e.Defeated = true;
        }
    }

    /// <summary>BB-04 Boss Codex row: has the player met / beaten this boss (any run).</summary>
    [Serializable]
    public sealed class BossCodexEntry
    {
        public string Id;
        public bool Met;
        public bool Defeated;
    }

    /// <summary>
    /// What is unlocked. Every ball is open from the start (balls no longer unlock through progress; daily quests pay chests
    /// instead). Traits: the starting set + the newer traits; achievements still unlock the older trait rewards.
    /// Ids not implemented yet are skipped (content arrives in later phases), so the table is stable.
    /// </summary>
    public static class UnlockRules
    {
        public const string Boss1 = "boss1", Boss2 = "boss2", Boss3 = "boss3", Win1 = "win1", Win3 = "win3";

        static readonly string[] StartWeapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };
        static readonly string[] StartTraits = { "heavy", "vampire", "spiky", "thorns", "second-wind",
            "berserker", "iron-skin", "regen", "executioner", "bank-shot" };

        /// <summary>achievement → (weapons, traits) it unlocks.</summary>
        static readonly (string achievement, string[] weapons, string[] traits)[] Table =
        {
            (Boss1, new string[0], new[] { "glass-cannon" }),
            (Boss2, new string[0], new[] { "poison-tip" }),
            (Boss3, new string[0], new[] { "parry-master" }),
            (Win1, new string[0], new[] { "bubble", "parry-master" }),
            (Win3, new string[0], new[] { "twin-blade" }),
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
                p.RecordBoss(last.BossId, last.Won);   // BB-04 Boss Codex (rewards below stay mapped to the boss slots)
                if (last.Won && last.FightIndex == RunTuning.BossFights[0]) Earn(Boss1);
                if (last.Won && last.FightIndex == RunTuning.BossFights[1]) Earn(Boss2);
            }

            if (run.Over)
            {
                p.RunsPlayed++;
                if (run.Won)
                {
                    HeatRules.RecordWin(p, run);   // first: a first win must still read its chapter as uncleared
                    p.RunsWon++;
                    Chapters.RecordWin(p, run);
                    Earn(Win1);
                    if (p.RunsWon >= 3) Earn(Win3);
                }
            }
            return earned;
        }

        /// <summary>A saved run replaced by a new one still counts as played (and lost).</summary>
        public static void RecordAbandoned(ProgressData p) => p.RunsPlayed++;

        // ---- Next goal (BB-02): the milestones shown to the player, in order. Change milestones in GoalTable only. ----

        /// <summary>
        /// Goal rows, first not-yet-reached row = the next goal. Beating the Mimic in the last fight always wins the run,
        /// so boss2 and win1 are one goal (either achievement reaches it; its reward is both). Legacy boss3 (no longer
        /// earned) has no row: it never counts as progress and never blocks AllDone.
        /// </summary>
        public static readonly GoalRow[] GoalTable =
        {
            new GoalRow(Boss1, new[] { Boss1 }, new[] { Boss1 }, GoalProgress.FightsCleared, RunTuning.BossFights[0] + 1),
            new GoalRow(Win1, new[] { Win1, Boss2 }, new[] { Boss2, Win1 }, GoalProgress.FightsCleared, RunTuning.Fights),
            new GoalRow(Win3, new[] { Win3 }, new[] { Win3 }, GoalProgress.RunsWon, 3),
        };

        public static GoalRow GoalRowOf(string goalId)
        {
            foreach (var row in GoalTable)
                if (row.Id == goalId) return row;
            return null;
        }

        /// <summary>The next goal for this progress: plain data, no display strings.</summary>
        public static NextGoalInfo NextGoal(ProgressData p)
        {
            foreach (var row in GoalTable)
            {
                if (row.Done(p)) continue;
                var progress = row.Progress == GoalProgress.RunsWon ? p.RunsWon : FightsCleared(p);
                return new NextGoalInfo
                {
                    GoalId = row.Id,
                    RewardIds = NewRewards(row, p),
                    // Never a full bar before the goal is actually reached.
                    Current = MaxInt(0, progress < row.Max - 1 ? progress : row.Max - 1),
                    Max = row.Max,
                };
            }
            return new NextGoalInfo { RewardIds = new List<string>(), AllDone = true };
        }

        /// <summary>
        /// Fights cleared in the best run so far. BestFight is the furthest fight reached, 1-based, counted when that
        /// fight's result is recorded whether won or lost; so cleared = BestFight - 1 (an under-count of one only when
        /// the furthest fight recorded was a win and the run was then left).
        /// </summary>
        public static int FightsCleared(ProgressData p) => MaxInt(p.BestFight - 1, 0);

        static int MaxInt(int a, int b) => a > b ? a : b;

        /// <summary>Weapon/trait ids the row grants that exist in this build and are not unlocked in <paramref name="p"/> yet.</summary>
        public static List<string> NewRewards(GoalRow row, ProgressData p)
        {
            var have = Weapons(p);
            have.AddRange(Traits(p));
            var ids = new List<string>();
            foreach (var a in row.Rewards)
                foreach (var t in Table)
                {
                    if (t.achievement != a) continue;
                    foreach (var w in t.weapons)
                        if (WeaponExists(w) && !have.Contains(w) && !ids.Contains(w)) ids.Add(w);
                    foreach (var tr in t.traits)
                        if (TraitRegistry.Exists(tr) && !have.Contains(tr) && !ids.Contains(tr)) ids.Add(tr);
                }
            return ids;
        }
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

    public enum GoalProgress { FightsCleared, RunsWon }

    /// <summary>One row of <see cref="UnlockRules.GoalTable"/>.</summary>
    public sealed class GoalRow
    {
        public readonly string Id;
        /// <summary>Having any one of these achievements means the goal is reached.</summary>
        public readonly string[] DoneWhen;
        /// <summary>Achievements whose unlocks this goal grants (merged).</summary>
        public readonly string[] Rewards;
        public readonly GoalProgress Progress;
        public readonly int Max;

        public GoalRow(string id, string[] doneWhen, string[] rewards, GoalProgress progress, int max)
        {
            Id = id;
            DoneWhen = doneWhen;
            Rewards = rewards;
            Progress = progress;
            Max = max;
        }

        public bool Done(ProgressData p)
        {
            foreach (var a in DoneWhen)
                if (p.Has(a)) return true;
            return false;
        }
    }

    /// <summary>Result of <see cref="UnlockRules.NextGoal"/>: plain data, the view turns it into text.</summary>
    public struct NextGoalInfo
    {
        public string GoalId;
        public List<string> RewardIds;
        public int Current;
        public int Max;
        public bool AllDone;
    }
}
