using System;
using System.Collections.Generic;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// End of a run: result, fights won, W/L line, bosses, best fight, retry used, the milestones this run reached
    /// (green frame, grouped by UnlockRules.GoalTable: winning the run = one "WIN A RUN" entry), NEXT GOAL, NEW RUN / MENU.
    /// </summary>
    public sealed class RunSummaryView : MonoBehaviour
    {
        public event Action NewRunRequested, MenuRequested;
        /// <summary>Raised when the summary appears (not on a redraw): AudioDirector plays run_win / run_over and the unlocks.</summary>
        public event Action Shown;

        /// <summary>Milestone rows shown in the green frame by the last Show (each gets one unlock cue).</summary>
        public int MilestonesShown { get; private set; }

        /// <summary>Most lines UnlockText returns (scale 1, wrapped between names so nothing is cut at 270 px).</summary>
        public const int UnlockLines = 3;
        /// <summary>Lines inside the milestone frame.</summary>
        public const int BoxLines = 10;
        const int BoxTop = 149;          // screen y of the milestone frame (BB-04: 13 px lower for the boss line)
        const int BoxPad = 6;
        const int LinePitch = PixelFont.CellHeight;   // BB-FONT1: 13 px cells (was 9)
        /// <summary>Screen y of the NEXT GOAL box (60 px tall).</summary>
        const int GoalTop = 300;

        public static readonly Rect NewRunArea = new Rect(-80, -160, 160, 32);   // screen 368..400
        public static readonly Rect MenuArea = new Rect(-80, -200, 160, 32);     // screen 408..440

        PixelText title, fights, record, bosses, best, retry, bossNames;
        readonly PixelText[] boxLines = new PixelText[BoxLines];
        SpriteRenderer boxBorder, boxFill;
        NextGoalBox goal;

        public NextGoalBox Goal => goal;
        public PixelText RetryText => retry;
        public PixelText BossesText => bosses;
        /// <summary>BB-04: "SHELL KING BEATEN  MIMIC MET" (bosses this run met; empty when none).</summary>
        public PixelText BossNamesText => bossNames;
        /// <summary>Non-empty lines in the milestone frame (tests).</summary>
        public List<string> BoxText { get; } = new List<string>();

        public static RunSummaryView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunSummary", art);
            var v = root.gameObject.AddComponent<RunSummaryView>();
            v.title = RunUi.Text(root, art, 0, 190, 3);    // cell 190..229
            v.fights = RunUi.Text(root, art, 0, 175, 1);   // 175..188
            v.record = RunUi.Text(root, art, 0, 148, 2);   // 148..174
            v.bosses = RunUi.Text(root, art, 0, 134, 1);   // 134..147
            v.best = RunUi.Text(root, art, 0, 121, 1);     // 121..134
            v.bossNames = RunUi.Text(root, art, 0, 108, 1);   // 108..121 (BB-04)
            v.retry = RunUi.Text(root, art, 0, 95, 1);     // 95..108

            var left = NextGoalBox.LocalX(8);
            var top = NextGoalBox.LocalTop(BoxTop);
            v.boxBorder = PixelRect.Create(root, "MilestoneBorder", art.Pixel, NextGoalBox.Green, RunUi.Order + 1);
            v.boxFill = PixelRect.Create(root, "MilestoneFill", art.Pixel, RunUi.Panel, RunUi.Order + 2);
            for (var i = 0; i < BoxLines; i++)
                v.boxLines[i] = RunUi.Text(root, art, left + BoxPad, top - 5 - i * LinePitch - PixelFont.CellHeight, 1, PixelText.Align.Left);

            v.goal = NextGoalBox.Create(root, art, 8, GoalTop, RunUi.Order);
            RunUi.Button(root, art, NewRunArea, "NEW RUN", 2, Palette.WallWarning, () => v.NewRunRequested?.Invoke());
            RunUi.Button(root, art, MenuArea, "MENU", 2, Palette.TextDim, () => v.MenuRequested?.Invoke()).ClickCue = AudioIds.UiBack;
            return v;
        }

        /// <summary>Without saved progress (tests): progress is rebuilt from the run alone.</summary>
        public void Show(RunState run) => Show(run, null);

        /// <summary>Fight-4 boss the finished <paramref name="run"/> beat, for its boss1 milestone ("BEAT MAGNET"). Reaching win1 means
        /// the run was won, so that milestone keeps "WIN A RUN".</summary>
        static string RunBossOf(string goalId, RunState run) =>
            run != null && goalId == UnlockRules.Boss1 ? run.BossFor(RunTuning.BossFights[0]) : null;

        /// <summary><paramref name="progress"/> = long-term progress after this run was recorded.</summary>
        public void Show(RunState run, ProgressData progress)
        {
            var after = progress ?? ProgressOf(run);
            var before = ProgressBefore(after, run);
            var appearing = !gameObject.activeSelf;
            gameObject.SetActive(true);

            title.Set(run.Won ? RunStrings.RunWon : RunStrings.RunOver, run.Won ? Palette.WallWarning : Palette.HpLost);
            var won = 0;
            var streak = 0;
            var bestStreak = 0;
            foreach (var f in run.History)
            {
                if (f.Won)
                {
                    won++;
                    streak++;
                    bestStreak = Mathf.Max(bestStreak, streak);
                }
                else streak = 0;
            }
            fights.Set(RunStrings.FightsWon(won, run.History.Count), Palette.Text);
            record.Set(StringTable.Current == Lang.Vi ? $"CHUỖI THẮNG DÀI NHẤT {bestStreak}" : $"BEST WIN STREAK {bestStreak}", Palette.Text);
            bosses.Set(RunStrings.Bosses(run.BossesDefeated), Palette.Text);
            bossNames.Set(TextWrap.Cut(BossText.SummaryLine(run), TextWrap.MaxWidthOnScreen(1)), Palette.WallWarning);
            best.Set(RunStrings.BestFight(after.BestFight), Palette.TextDim);
            var retryAt = RetryUsedAtFight(run);
            retry.Set(retryAt > 0 ? RunStrings.RetryUsedAt(retryAt) : "", Palette.WallWarning);

            var lines = QuestLines(run);   // daily quests finished by this run (the goal ladder is gone)
            MilestonesShown = 0;
            foreach (var l in lines) if (l.color.Equals(NextGoalBox.Green)) MilestonesShown++;   // goal-name rows (green)
            BoxText.Clear();
            for (var i = 0; i < BoxLines; i++)
            {
                var has = i < lines.Count;
                boxLines[i].Set(has ? lines[i].text : "", has ? lines[i].color : Palette.Text);
                if (has) BoxText.Add(lines[i].text);
            }
            var show = lines.Count > 0;
            boxBorder.gameObject.SetActive(show);
            boxFill.gameObject.SetActive(show);
            if (show)
            {
                var left = NextGoalBox.LocalX(8);
                var top = NextGoalBox.LocalTop(BoxTop);
                var bottom = top - (5 + Mathf.Min(lines.Count, BoxLines) * LinePitch + 1);
                PixelRect.Set(boxBorder, left, bottom, left + NextGoalBox.Width, top);
                PixelRect.Set(boxFill, left + 1, bottom + 1, left + NextGoalBox.Width - 1, top - 1);
            }
            goal.Show(after);
            if (appearing) Shown?.Invoke();
        }

        public struct Milestone
        {
            public string GoalId;
            /// <summary>Ids this milestone unlocked now (not already unlocked before the run).</summary>
            public List<string> RewardIds;
        }

        /// <summary>Goal rows reached during the run, in GoalTable order: not reached before, reached after.</summary>
        public static List<Milestone> Reached(ProgressData before, ProgressData after)
        {
            var list = new List<Milestone>();
            foreach (var row in UnlockRules.GoalTable)
                if (!row.Done(before) && row.Done(after))
                    list.Add(new Milestone { GoalId = row.Id, RewardIds = UnlockRules.NewRewards(row, before) });
            return list;
        }

        /// <summary>Progress as it was before this run: the run's own achievements removed.</summary>
        public static ProgressData ProgressBefore(ProgressData after, RunState run)
        {
            var p = new ProgressData { RunsPlayed = after.RunsPlayed, RunsWon = after.RunsWon, BestFight = after.BestFight };
            foreach (var a in after.Achievements)
                if (!run.Achievements.Contains(a)) p.Achievements.Add(a);
            return p;
        }

        /// <summary>Stand-in progress when only the run is known.</summary>
        static ProgressData ProgressOf(RunState run)
        {
            var p = new ProgressData { RunsPlayed = 1, RunsWon = run.Won ? 1 : 0 };
            p.Achievements.AddRange(run.Achievements);
            foreach (var f in run.History) p.BestFight = Mathf.Max(p.BestFight, f.FightIndex + 1);
            return p;
        }

        /// <summary>
        /// Fight (1-based) whose loss used the retry, 0 if the run never used it. Read from RunState: RetryCount says
        /// whether it was used, the first lost fight in History is where (each loss before the retries run out uses one).
        /// </summary>
        public static int RetryUsedAtFight(RunState run)
        {
            if (run == null || run.RetryCount == 0) return 0;
            foreach (var f in run.History)
                if (!f.Won) return f.FightIndex + 1;
            return 0;
        }

        /// <summary>
        /// Frame lines for the daily quests this run finished (RunState.Achievements "quest:" marks, in order): a dim header, then per
        /// quest its title (green) and "REWARD: WOOD CHEST" (yellow); the all-done bonus last. Empty when the run finished none.
        /// </summary>
        public static List<(string text, Color32 color)> QuestLines(RunState run)
        {
            var lines = new List<(string, Color32)>();
            if (run == null) return lines;
            foreach (var a in run.Achievements)   // a new chapter opened by this win comes first
                if (a != null && a.StartsWith(Chapters.Mark) && int.TryParse(a.Substring(Chapters.Mark.Length), out var opened))
                    lines.Add((RunStrings.ChapterUnlocked(opened), NextGoalBox.Green));
            foreach (var a in run.Achievements)   // then a heat this win opened ("heat:chapter:heat")
            {
                if (a == null || !a.StartsWith(HeatRules.Mark)) continue;
                var parts = a.Substring(HeatRules.Mark.Length).Split(':');
                if (parts.Length == 2 && int.TryParse(parts[0], out var hc) && int.TryParse(parts[1], out var hh))
                    lines.Add((RunStrings.HeatUnlocked(hc, hh), NextGoalBox.Green));
            }
            foreach (var a in run.Achievements)
            {
                if (a == null || !a.StartsWith(DailyQuests.QuestMark)) continue;
                var id = a.Substring(DailyQuests.QuestMark.Length);
                if (lines.Count == 0) lines.Add((RunStrings.QuestsCompleted, Palette.TextDim));
                if (id == DailyQuests.BonusId)
                {
                    lines.Add((RunStrings.QuestBonus, NextGoalBox.Green));
                    lines.Add((RunStrings.QuestReward(DailyQuests.BonusChest), Palette.WallWarning));
                    continue;
                }
                var def = DailyQuests.Def(id);
                if (def == null) continue;
                var slot = new QuestSlot { Id = id, Ball = run.Build?.WeaponId ?? "" };
                lines.Add((RunStrings.QuestTitle(slot), NextGoalBox.Green));
                lines.Add((RunStrings.QuestReward(def.Reward), Palette.WallWarning));
            }
            if (lines.Count > BoxLines) lines.RemoveRange(BoxLines, lines.Count - BoxLines);
            return lines;
        }

        /// <summary>Frame lines: goal name (green), "UNLOCKED: ..." wrapped (yellow), then a dim hint when anything unlocked.</summary>
        public static List<(string text, Color32 color)> MilestoneLines(List<Milestone> reached) => MilestoneLines(reached, null);

        /// <summary>BB-VIEW3: <paramref name="run"/> (the finished run) names the boss behind a boss goal it reached ("BEAT MAGNET").</summary>
        public static List<(string text, Color32 color)> MilestoneLines(List<Milestone> reached, RunState run)
        {
            var lines = new List<(string, Color32)>();
            var max = NextGoalBox.Width - 2 * BoxPad;   // pixels (scale 1)
            var any = false;
            foreach (var m in reached)
            {
                lines.Add((RunStrings.GoalTitle(m.GoalId, RunBossOf(m.GoalId, run)), NextGoalBox.Green));
                var names = new List<string>();
                foreach (var id in m.RewardIds) names.Add(RunStrings.RewardName(id));
                foreach (var l in TextWrap.Items(names, RunStrings.Unlocked, "  ", max, true)) lines.Add((l, Palette.WallWarning));
                any |= names.Count > 0;
            }
            if (lines.Count > BoxLines) lines.RemoveRange(BoxLines, lines.Count - BoxLines);
            return lines;
        }

        /// <summary>
        /// "UNLOCKED: A  B  C" broken between names to fit the 270 px screen, at most UnlockLines lines
        /// (the last one ends with "+N MORE" if even that is not enough).
        /// </summary>
        public static List<string> UnlockText(IReadOnlyList<string> names)
        {
            var max = TextWrap.MaxWidthOnScreen(1);
            var lines = TextWrap.Items(names, "UNLOCKED: ", "  ", max);
            if (lines.Count <= UnlockLines) return lines;
            // Too many: keep what fits, say how many more.
            var shown = 0;
            var kept = new List<string>();
            for (var take = names.Count - 1; take >= 1; take--)
            {
                var head = new List<string>();
                for (var i = 0; i < take; i++) head.Add(names[i]);
                head.Add("+" + (names.Count - take) + " MORE");
                kept = TextWrap.Items(head, "UNLOCKED: ", "  ", max);
                shown = take;
                if (kept.Count <= UnlockLines) break;
            }
            return shown > 0 ? kept : lines.GetRange(0, UnlockLines);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
