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

        PixelText stats;
        PixelButton continueButton;
        ConfirmDialog dialog;
        NextGoalBox goal;
        bool hasSavedRun;

        public static readonly Rect ContinueArea = new Rect(-80, 30, 160, 32);
        public static readonly Rect NewRunArea = new Rect(-80, -20, 160, 32);
        public static readonly Rect BackArea = new Rect(-60, -98, 120, 32);

        /// <summary>Footer band (8,380), 254 wide: the rule lines, one 13 px cell each; a line wider than the band wraps
        /// at its "  " separators onto more lines (VI) and the band grows down with it.</summary>
        const int FooterTop = 380;
        const int FooterLinePitch = PixelFont.CellHeight;
        const int FooterPad = 4;

        public static RunTitleView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunTitle", art);
            var v = root.gameObject.AddComponent<RunTitleView>();
            RunUi.Text(root, art, 0, 189, 3, initial: RunStrings.RunMode);                                   // top 36
            RunUi.Text(root, art, 0, 173, 1, initial: RunStrings.RulesSummary, color: Palette.TextDim);       // top 62
            v.stats = RunUi.Text(root, art, 0, 155, 1);                                                       // top 80
            v.goal = NextGoalBox.Create(root, art, 8, 96, RunUi.Order);
            v.continueButton = RunUi.Button(root, art, ContinueArea, "CONTINUE", 2, Palette.WallWarning, () => { if (!v.DialogOpen) v.ContinueRequested?.Invoke(); });
            RunUi.Button(root, art, NewRunArea, "NEW RUN", 2, Palette.Text, () => { if (!v.DialogOpen) v.OnNewRun(); });
            v.BackButton = RunUi.Button(root, art, BackArea, RunStrings.Back, RunUi.BackTextScale, Palette.TextDim, () => { if (!v.DialogOpen) v.BackRequested?.Invoke(); });
            v.BackButton.ClickCue = AudioIds.UiBack;

            var bandTop = NextGoalBox.LocalTop(FooterTop);
            var bandLeft = NextGoalBox.LocalX(8);
            string[] rules = { RunStrings.PickOneCard, RunStrings.BossAt, RunStrings.LoseOnce, RunStrings.LoseAgain };
            Color32[] colors = { Palette.TextDim, Palette.TextDim, Palette.WallWarning, Palette.HpLost };
            var lines = new System.Collections.Generic.List<(string text, Color32 color)>();
            for (var i = 0; i < rules.Length; i++)
                foreach (var l in TextWrap.SplitToFit(rules[i], NextGoalBox.Width - 2 * FooterPad)) lines.Add((l, colors[i]));
            PixelRect.Set(PixelRect.Create(root, "RulesBand", art.Pixel, RunUi.Panel, RunUi.Order + 1),
                          bandLeft, bandTop - (lines.Count * FooterLinePitch + 2 * FooterPad), bandLeft + NextGoalBox.Width, bandTop);
            for (var i = 0; i < lines.Count; i++)
                RunUi.Text(root, art, 0, bandTop - FooterPad - (i + 1) * FooterLinePitch, 1, initial: lines[i].text, color: lines[i].color);

            v.dialog = ConfirmDialog.Create(root, art, RunUi.Order + 20, "ConfirmAbandon");
            v.dialog.Closed += () => PixelButton.NoteShownAll(root);   // the tap on YES/NO never reaches NEW RUN / CONTINUE under it
            return v;
        }

        public void Show(RunFlow flow)
        {
            gameObject.SetActive(true);
            var p = flow.Progress;
            stats.Set(StatsLine(p), Palette.Text);
            goal.Show(p);
            hasSavedRun = flow.HasSavedRun;
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
