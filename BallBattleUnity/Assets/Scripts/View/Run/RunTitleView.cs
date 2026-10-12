using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Run mode entry: progress line, CONTINUE (when a run is saved), NEW RUN, BACK.</summary>
    public sealed class RunTitleView : MonoBehaviour
    {
        public event Action ContinueRequested, NewRunRequested, BackRequested;

        PixelText stats;
        PixelButton continueButton;
        ConfirmDialog dialog;
        bool hasSavedRun;

        public static readonly Rect ContinueArea = new Rect(-80, 30, 160, 32);
        public static readonly Rect NewRunArea = new Rect(-80, -20, 160, 32);
        public static readonly Rect BackArea = new Rect(-60, -90, 120, 22);

        public static RunTitleView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunTitle", art);
            var v = root.gameObject.AddComponent<RunTitleView>();
            RunUi.Text(root, art, 0, 170, 3, initial: "RUN MODE");
            RunUi.Text(root, art, 0, 150, 1, initial: "8 FIGHTS  3 BOSSES  3 LIVES", color: Palette.TextDim);
            v.stats = RunUi.Text(root, art, 0, 120, 1);
            v.continueButton = RunUi.Button(root, art, ContinueArea, "CONTINUE", 2, Palette.WallWarning, () => { if (!v.DialogOpen) v.ContinueRequested?.Invoke(); });
            RunUi.Button(root, art, NewRunArea, "NEW RUN", 2, Palette.Text, () => { if (!v.DialogOpen) v.OnNewRun(); });
            RunUi.Button(root, art, BackArea, "BACK", 1, Palette.TextDim, () => { if (!v.DialogOpen) v.BackRequested?.Invoke(); });
            RunUi.Text(root, art, 0, -140, 1, initial: "PICK CARDS BEFORE EACH FIGHT", color: Palette.TextDim);
            RunUi.Text(root, art, 0, -150, 1, initial: "FIGHTS PLAY THEMSELVES", color: Palette.TextDim);
            v.dialog = ConfirmDialog.Create(root, art, RunUi.Order + 20, "ConfirmAbandon");
            v.dialog.Closed += () => PixelButton.NoteShownAll(root);   // the tap on YES/NO never reaches NEW RUN / CONTINUE under it
            return v;
        }

        public void Show(RunFlow flow)
        {
            gameObject.SetActive(true);
            var p = flow.Progress;
            stats.Set(StatsLine(p), Palette.Text);
            hasSavedRun = flow.HasSavedRun;
            continueButton.gameObject.SetActive(hasSavedRun);
            dialog.Close();
        }

        /// <summary>
        /// "BEST FIGHT b/8   WINS w/p": runs won (ended by beating the last boss) out of runs played (as counted today:
        /// started and finished or abandoned). Falls back to "WINS w" if the line would not fit the 270 px screen.
        /// </summary>
        public static string StatsLine(BallBattle.Sim.Run.ProgressData p)
        {
            var best = $"BEST FIGHT {p.BestFight}/{BallBattle.Sim.Run.RunTuning.Fights}   ";
            var full = best + $"WINS {p.RunsWon}/{p.RunsPlayed}";
            return full.Length <= TextWrap.MaxCharsOnScreen(1) ? full : best + $"WINS {p.RunsWon}";
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
