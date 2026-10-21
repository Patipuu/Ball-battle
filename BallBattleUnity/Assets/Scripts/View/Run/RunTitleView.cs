using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Run mode entry: rules line, progress line, NEXT GOAL box, CONTINUE (when a run is saved), NEW RUN, BACK,
    /// and the rules footer (pick, bosses, retry, run over). Every rule number comes from RunTuning via RunStrings.
    /// </summary>
    public sealed class RunTitleView : MonoBehaviour
    {
        public event Action ContinueRequested, NewRunRequested, BackRequested;

        PixelText stats, chapterName, chapterInfo, chapterLore, heatRule, heatReward;
        PixelButton newRunButton, prevChapter, nextChapter;
        int unlockedChapter;
        RunFlow flow;
        /// <summary>Chapter NEW RUN starts (0-based). Defaults to the newest unlocked one each time the screen opens.</summary>
        public int SelectedChapter { get; private set; }
        /// <summary>HEAT NEW RUN starts (0-based). Defaults to the chapter's highest open heat.</summary>
        public int SelectedHeat { get; private set; }
        int unlockedHeat;
        readonly PixelButton[] heatButtons = new PixelButton[BallBattle.Sim.Run.HeatRules.Count];
        public static readonly Rect[] HeatAreas = { new Rect(-117, -96, 76, 26), new Rect(-38, -96, 76, 26), new Rect(41, -96, 76, 26) };
        public static readonly Rect PrevChapterArea = new Rect(-125, -62, 26, 36), NextChapterArea = new Rect(99, -62, 26, 36);
        PixelButton continueButton;
        ConfirmDialog dialog;
        NextGoalBox goal;
        bool hasSavedRun;

        public static readonly Rect ContinueArea = new Rect(-80, 30, 160, 32);
        public static readonly Rect NewRunArea = new Rect(-80, -20, 160, 32);
        public static readonly Rect BackArea = new Rect(-60, -160, 120, 32);


        public static RunTitleView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunTitle", art);
            var v = root.gameObject.AddComponent<RunTitleView>();
            RunUi.Text(root, art, 0, 189, 3, initial: RunStrings.RunMode);                                   // top 36
            v.stats = RunUi.Text(root, art, 0, 155, 1);                                                       // top 80
            v.goal = NextGoalBox.Create(root, art, 8, 96, RunUi.Order);
            v.continueButton = RunUi.Button(root, art, ContinueArea, "CONTINUE", 2, Palette.WallWarning, () => { if (!v.DialogOpen) v.ContinueRequested?.Invoke(); });
            v.newRunButton = RunUi.Button(root, art, NewRunArea, "NEW RUN", 2, Palette.Text, () => { if (!v.DialogOpen) v.OnNewRun(); });
            // Chapter picker between NEW RUN and BACK: "< CHAPTER 2: IRON FORT >" and its boss pair (or how to unlock it).
            v.chapterName = RunUi.Text(root, art, 0, -40, 1);    // -40..-27
            v.chapterInfo = RunUi.Text(root, art, 0, -56, 1);    // -56..-43
            v.prevChapter = v.ArrowButton(root, art, PrevChapterArea, -1);
            v.nextChapter = v.ArrowButton(root, art, NextChapterArea, 1);
            // HEAT row under the chapter picker: three steps, then the selected step's rule and reward.
            for (var h = 0; h < v.heatButtons.Length; h++)
            {
                var heat = h;
                v.heatButtons[h] = RunUi.Button(root, art, HeatAreas[h], RunStrings.HeatName(h), 1, Palette.Text, () => { if (!v.DialogOpen) v.SelectHeat(heat); });
            }
            v.heatRule = RunUi.Text(root, art, 0, -112, 1);      // -112..-99
            v.heatReward = RunUi.Text(root, art, 0, -126, 1);    // -126..-113
            // The chapter's background concept, between CONTINUE and the goal box.
            v.chapterLore = RunUi.Text(root, art, 0, 66, 1);     // 66..79
            v.BackButton = RunUi.Button(root, art, BackArea, RunStrings.Back, RunUi.BackTextScale, Palette.TextDim, () => { if (!v.DialogOpen) v.BackRequested?.Invoke(); });
            v.BackButton.ClickCue = AudioIds.UiBack;

            v.dialog = ConfirmDialog.Create(root, art, RunUi.Order + 20, "ConfirmAbandon");
            v.dialog.Closed += () => PixelButton.NoteShownAll(root);   // the tap on YES/NO never reaches NEW RUN / CONTINUE under it
            return v;
        }

        PixelButton ArrowButton(Transform root, ArtLibrary art, Rect area, int dir)
        {
            var b = PixelButton.Create(root, dir < 0 ? "ChapterPrev" : "ChapterNext", art, area, null, 1, Palette.Floor, Palette.TextDim, Palette.Text, RunUi.Order + 2);
            b.Clicked += () => { if (!DialogOpen) SelectChapter(SelectedChapter + dir); };
            // A 5-column pixel triangle pointing left or right, centred in the tap target.
            var cx = Mathf.Round(area.center.x);
            var cy = Mathf.Round(area.center.y);
            for (var i = 0; i < 5; i++)
            {
                var x = dir < 0 ? cx - 2 + i : cx + 2 - i;
                PixelRect.Set(PixelRect.Create(b.transform, "Arrow" + i, art.Pixel, Palette.Text, b.IconOrder), x, cy - i, x + 1, cy + i + 1);
            }
            return b;
        }

        /// <summary>Show chapter <paramref name="index"/> (clamped). A locked chapter is shown, but NEW RUN is greyed out.</summary>
        public void SelectChapter(int index)
        {
            var count = BallBattle.Sim.Run.Chapters.Count;
            SelectedChapter = Mathf.Clamp(index, 0, count - 1);
            var ch = BallBattle.Sim.Run.Chapters.Get(SelectedChapter);
            var open = SelectedChapter <= unlockedChapter;
            var cleared = flow != null && BallBattle.Sim.Run.Chapters.Cleared(flow.Progress, SelectedChapter);
            chapterName.Set(RunStrings.ChapterTitle(SelectedChapter, ch.Name), !open ? Palette.TextDim : cleared ? NextGoalBox.Green : Palette.WallWarning);
            chapterInfo.Set(open ? RunStrings.ChapterBosses(BossText.Name(ch.Bosses[0]), BossText.Name(ch.Bosses[1]))
                : RunStrings.ChapterLocked(SelectedChapter), open ? Palette.Text : Palette.TextDim);
            chapterLore.Set("", Palette.TextDim);
            prevChapter.gameObject.SetActive(SelectedChapter > 0);
            nextChapter.gameObject.SetActive(SelectedChapter < count - 1);
            unlockedHeat = open && flow != null ? BallBattle.Sim.Run.HeatRules.Unlocked(flow.Progress, SelectedChapter) : 0;
            SelectHeat(unlockedHeat);
        }

        /// <summary>Show heat <paramref name="heat"/> of the selected chapter (clamped). A locked heat is shown, NEW RUN greyed out.</summary>
        public void SelectHeat(int heat)
        {
            SelectedHeat = Mathf.Clamp(heat, 0, heatButtons.Length - 1);
            var chapterOpen = SelectedChapterOpen;
            var cleared = flow != null ? BallBattle.Sim.Run.HeatRules.Cleared(flow.Progress, SelectedChapter) : 0;
            for (var h = 0; h < heatButtons.Length; h++)
            {
                var open = chapterOpen && h <= unlockedHeat;
                var accent = h == SelectedHeat && open ? Palette.WallWarning : !open ? Palette.Wall : h < cleared ? NextGoalBox.Green : Palette.Text;
                heatButtons[h].SetColors(h == SelectedHeat ? RunUi.Panel : Palette.Floor, accent); heatButtons[h].SetText(RunStrings.HeatName(h), accent);
            }
            var heatOpen = SelectedHeatOpen;
            heatRule.Set(RunStrings.HeatRule(SelectedHeat), heatOpen ? Palette.Text : Palette.TextDim);
            heatReward.Set(!chapterOpen ? "" : heatOpen ? RunStrings.HeatReward(SelectedHeat) : RunStrings.HeatLocked(SelectedHeat),
                heatOpen ? Palette.WallWarning : Palette.TextDim);
            newRunButton.SetColors(Palette.Floor, heatOpen ? Palette.Text : Palette.Wall);
        }

        public bool SelectedChapterOpen => SelectedChapter <= unlockedChapter;
        public bool SelectedHeatOpen => SelectedChapterOpen && SelectedHeat <= unlockedHeat;
        public PixelButton HeatButton(int heat) => heatButtons[heat];

        public void Show(RunFlow flow)
        {
            gameObject.SetActive(true);
            this.flow = flow;
            var p = flow.Progress;
            unlockedChapter = BallBattle.Sim.Run.Chapters.Unlocked(p);
            SelectChapter(unlockedChapter);
            stats.Set(StatsLine(p), Palette.Text);
            hasSavedRun = flow.HasSavedRun;
            goal.Show(p, hasSavedRun ? flow.PeekSave().resumable : null);   // BB-VIEW3: the saved run's bosses name the boss goals
            continueButton.gameObject.SetActive(hasSavedRun);
            dialog.Close();
        }

        public NextGoalBox Goal => goal;
        /// <summary>The BACK button (tests).</summary>
        public PixelButton BackButton { get; private set; }

        /// <summary>
        /// "BEST FIGHT b/8   WINS w/p": runs won (ended by beating the last boss) out of runs played (as counted today:
        /// started and finished or abandoned). Falls back to "WINS w" if the line would not fit the 270 px screen.
        /// </summary>
        public static string StatsLine(BallBattle.Sim.Run.ProgressData p)
        {
            var best = $"BEST FIGHT {p.BestFight}/{BallBattle.Sim.Run.RunTuning.Fights}   ";
            var full = best + $"WINS {p.RunsWon}/{p.RunsPlayed}";
            return TextWrap.FitsScreen(full, 1) ? full : best + $"WINS {p.RunsWon}";
        }
        /// <summary>NEW RUN with a run saved asks first: only YES overwrites it; NO keeps it. Without a saved run it starts at once.</summary>
        void OnNewRun()
        {
            if (!SelectedHeatOpen) return;
            if (hasSavedRun) dialog.Open("ABANDON CURRENT RUN?", "THE SAVED RUN IS LOST", NewRunArea, () => NewRunRequested?.Invoke());
            else NewRunRequested?.Invoke();
        }

        public void Hide()
        {
            dialog?.Close();
            gameObject.SetActive(false);
        }

        // ---- Tests / automation ----
        public void PressNewRun() { if (!DialogOpen) OnNewRun(); }
        public bool DialogOpen => dialog != null && dialog.IsOpen;
        public ConfirmDialog Dialog => dialog;
    }
}
