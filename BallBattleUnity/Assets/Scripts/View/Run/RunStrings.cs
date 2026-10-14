using System.Collections.Generic;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Player-facing strings added for BB-01/02 (Run entry, rules, next goal, summary), kept in one place so a later
    /// translation pass can swap them for keys. Rule numbers always come from RunTuning, never typed here.
    /// The pixel font has no comma glyph: list items are separated by two spaces, as elsewhere on screen.
    /// </summary>
    public static class RunStrings
    {
        public const string Run = "RUN";
        public const string ContinueRun = "CONTINUE RUN";
        public const string Versus = "VERSUS";
        public const string Back = "BACK";
        public const string RunMode = "RUN MODE";
        public const string NextGoal = "NEXT GOAL";
        public const string AllUnlocked = "ALL UNLOCKED";
        public const string Unlocks = "UNLOCKS: ";
        public const string Unlocked = "UNLOCKED: ";
        public const string AppearLater = "THEY CAN APPEAR IN LATER RUNS";
        /// <summary>One-time notice after a save update dropped an old run (VI later: "RUN CŨ ĐÃ HUỶ DO CẬP NHẬT").</summary>
        public const string OldRunEnded = "OLD RUN ENDED BY UPDATE";
        public const string Ok = "OK";
        public const string Settings = "SETTINGS";
        public const string Music = "MUSIC";
        public const string Sfx = "SFX";
        public const string Language = "LANGUAGE";
        public const string En = "EN";
        public const string Vi = "VI";
        public const string PickOneCard = "PICK 1 CARD BEFORE EACH FIGHT";
        public const string RunWon = "RUN WON!";
        public const string RunOver = "RUN OVER";
        const string ListSep = "  ";

        /// <summary>"8 FIGHTS  2 BOSSES  1 RETRY".</summary>
        public static string RulesSummary =>
            $"{RunTuning.Fights} FIGHTS  {RunTuning.BossFights.Length} {(RunTuning.BossFights.Length == 1 ? "BOSS" : "BOSSES")}  {RunTuning.Retries} {(RunTuning.Retries == 1 ? "RETRY" : "RETRIES")}";

        /// <summary>Boss line from RunTuning.BossFights in the current language (see <see cref="BossAtFor"/>).</summary>
        public static string BossAt => BossAtFor(RunTuning.BossFights, StringTable.Current);

        /// <summary>
        /// Boss line built from a boss fight list (0-based fight indices, shown 1-based). One boss: "BOSS AT FIGHT 4";
        /// two: "BOSS AT FIGHTS 4 AND 8"; three or more: "BOSS AT FIGHTS 4  6  8" (two spaces, no AND, never "/").
        /// VI: "BOSS Ở TRẬN 4", "BOSS Ở TRẬN 4 VÀ 8", "BOSS Ở TRẬN 4  6  8". Empty list: "".
        /// </summary>
        public static string BossAtFor(IReadOnlyList<int> bossFights, Lang lang)
        {
            if (bossFights == null || bossFights.Count == 0) return "";
            if (bossFights.Count == 1) return StringTable.Format(StringKeys.BossAtOne, lang, bossFights[0] + 1);
            if (bossFights.Count == 2) return StringTable.Format(StringKeys.BossAtTwo, lang, bossFights[0] + 1, bossFights[1] + 1);
            var numbers = "";
            for (var i = 0; i < bossFights.Count; i++) numbers += (i > 0 ? ListSep : "") + (bossFights[i] + 1);
            return StringTable.Format(StringKeys.BossAtMany, lang, numbers);
        }

        public static int RetryHealPercent => Mathf.RoundToInt(RunTuning.RetryHealPct.ToFloat() * 100f);

        /// <summary>"LOSE ONCE: FREE PICK  RETRY AT 60% HP" in the current language.</summary>
        public static string LoseOnce => LoseOnceIn(StringTable.Current);

        /// <summary>EN "LOSE ONCE: FREE PICK  RETRY AT 60% HP"; VI "THUA LẦN 1: CHỌN THẺ MIỄN PHÍ  ĐẤU LẠI VỚI 60% HP".</summary>
        public static string LoseOnceIn(Lang lang) => StringTable.Format(StringKeys.LoseOnce, lang, RetryHealPercent);

        /// <summary>Start pick footer: "NO REROLL ON START PICK".</summary>
        public static string StartPickNoReroll => StringTable.Get(StringKeys.StartPickNoReroll);

        public static string LoseAgain => RunTuning.Retries == 1 ? "LOSE AGAIN: RUN OVER" : "OUT OF RETRIES: RUN OVER";

        /// <summary>Second line of CONTINUE RUN: "FIGHT 3/8  RETRY 1" (fight 1-based, retries still left).</summary>
        public static string ContinueLine(RunState run) =>
            $"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}{ListSep}RETRY {Mathf.Max(0, RunTuning.Retries - run.RetryCount)}";

        public static string GoalTitle(string goalId)
        {
            switch (goalId)
            {
                case UnlockRules.Boss1: return "BEAT SHELL KING";
                case UnlockRules.Win1: return "WIN A RUN";
                case UnlockRules.Win3:
                    var row = UnlockRules.GoalRowOf(goalId);   // the run count is the row's Max (GoalTable), never typed here
                    return row != null ? $"WIN {row.Max} RUNS" : goalId.ToUpperInvariant();
                default: return (goalId ?? "").ToUpperInvariant();
            }
        }

        public static string Progress(int current, int max) => $"{current}/{max}";
        public static string Wins(int runsWon) => $"WINS {runsWon}";
        public static string FightsWon(int won, int played) => $"FIGHTS WON {won}/{played}";
        public static string Bosses(int defeated) => $"BOSSES {defeated}/{RunTuning.BossFights.Length}";
        public static string BestFight(int best) => $"BEST FIGHT {best}/{RunTuning.Fights}";
        public static string RetryUsedAt(int fight) => $"RETRY USED AT FIGHT {fight}";

        /// <summary>On-screen name of an unlockable id: weapons use the art names (WeaponNames), traits their registry name.</summary>
        public static string RewardName(string id)
        {
            foreach (var w in WeaponRegistry.All)
                if (w.Id == id) return WeaponNames.Of(id);
            return TraitRegistry.Exists(id) ? TraitRegistry.Get(id).DisplayName : (id ?? "").ToUpperInvariant();
        }

        /// <summary>
        /// On-screen names of what an achievement unlocks, in table order (weapons, then traits): the current ball names
        /// (WeaponNames: EMBER, MANTIS, ... HIVE), not the sim registry names. View-side replacement for
        /// UnlockRules.UnlockedNames, which reads WeaponRegistry.DisplayName (Sim stays untouched).
        /// </summary>
        public static List<string> UnlockedNames(string achievement)
        {
            var names = new List<string>();
            if (string.IsNullOrEmpty(achievement)) return names;
            var row = new GoalRow(achievement, new[] { achievement }, new[] { achievement }, GoalProgress.RunsWon, 1);
            foreach (var id in UnlockRules.NewRewards(row, new ProgressData())) names.Add(RewardName(id));
            return names;
        }

        /// <summary>"UNLOCKS: A  B +N": at most two names, the rest counted. Empty when there is nothing to unlock.</summary>
        public static string UnlocksLine(IReadOnlyList<string> rewardIds)
        {
            if (rewardIds == null || rewardIds.Count == 0) return "";
            var s = Unlocks + RewardName(rewardIds[0]);
            if (rewardIds.Count > 1) s += ListSep + RewardName(rewardIds[1]);
            if (rewardIds.Count > 2) s += " +" + (rewardIds.Count - 2);
            return s;
        }
    }
}
