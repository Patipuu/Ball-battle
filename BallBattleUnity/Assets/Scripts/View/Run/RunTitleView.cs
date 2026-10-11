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

        public static RunTitleView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunTitle", art);
            var v = root.gameObject.AddComponent<RunTitleView>();
            RunUi.Text(root, art, 0, 170, 3, initial: "RUN MODE");
            RunUi.Text(root, art, 0, 150, 1, initial: "8 FIGHTS  3 BOSSES  3 LIVES", color: Palette.TextDim);
            v.stats = RunUi.Text(root, art, 0, 120, 1);
            v.continueButton = RunUi.Button(root, art, new Rect(-80, 30, 160, 32), "CONTINUE", 2, Palette.WallWarning, () => v.ContinueRequested?.Invoke());
            RunUi.Button(root, art, new Rect(-80, -20, 160, 32), "NEW RUN", 2, Palette.Text, () => v.NewRunRequested?.Invoke());
            RunUi.Button(root, art, new Rect(-60, -90, 120, 22), "BACK", 1, Palette.TextDim, () => v.BackRequested?.Invoke());
            RunUi.Text(root, art, 0, -140, 1, initial: "PICK CARDS BEFORE EACH FIGHT", color: Palette.TextDim);
            RunUi.Text(root, art, 0, -150, 1, initial: "FIGHTS PLAY THEMSELVES", color: Palette.TextDim);
            return v;
        }

        public void Show(RunFlow flow)
        {
            gameObject.SetActive(true);
            var p = flow.Progress;
            stats.Set(StatsLine(p), Palette.Text);
            continueButton.gameObject.SetActive(flow.HasSavedRun);
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
        public void Hide() => gameObject.SetActive(false);
    }
}
