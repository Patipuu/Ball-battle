using System;
using System.Collections.Generic;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>End of a run: result, fight-by-fight line, achievements earned, NEW RUN / MENU.</summary>
    public sealed class RunSummaryView : MonoBehaviour
    {
        public event Action NewRunRequested, MenuRequested;

        /// <summary>Unlock list lines (scale 1, wrapped between names so nothing is cut at 270 px).</summary>
        public const int UnlockLines = 3;

        PixelText title, fights, record, hint;
        readonly PixelText[] unlocks = new PixelText[UnlockLines];

        public static RunSummaryView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunSummary", art);
            var v = root.gameObject.AddComponent<RunSummaryView>();
            v.title = RunUi.Text(root, art, 0, 160, 3);
            v.fights = RunUi.Text(root, art, 0, 130, 1);
            v.record = RunUi.Text(root, art, 0, 112, 2);
            for (var i = 0; i < UnlockLines; i++) v.unlocks[i] = RunUi.Text(root, art, 0, 84 - i * 9, 1);
            v.hint = RunUi.Text(root, art, 0, 84 - UnlockLines * 9 - 3, 1);
            RunUi.Button(root, art, new Rect(-80, -10, 160, 32), "NEW RUN", 2, Palette.WallWarning, () => v.NewRunRequested?.Invoke());
            RunUi.Button(root, art, new Rect(-80, -60, 160, 32), "MENU", 2, Palette.TextDim, () => v.MenuRequested?.Invoke());
            return v;
        }

        public void Show(RunState run)
        {
            var unlocked = new List<string>();
            foreach (var a in run.Achievements) unlocked.AddRange(UnlockRules.UnlockedNames(a));
            gameObject.SetActive(true);
            title.Set(run.Won ? "RUN WON!" : "RUN OVER", run.Won ? Palette.WallWarning : Palette.HpLost);
            var won = 0;
            var line = "";
            foreach (var f in run.History)
            {
                if (f.Won) won++;
                line += f.Won ? "W" : f.Draw ? "D" : "L";
            }
            fights.Set($"FIGHTS WON {won}/{run.History.Count}   BOSSES {run.BossesDefeated}/3", Palette.Text);
            record.Set(line, Palette.Text);
            var lines = UnlockText(unlocked);
            for (var i = 0; i < UnlockLines; i++) unlocks[i].Set(i < lines.Count ? lines[i] : "", Palette.WallWarning);
            hint.Set(unlocked.Count > 0 ? "THEY CAN APPEAR IN LATER RUNS" : "", Palette.TextDim);
        }

        /// <summary>
        /// "UNLOCKED: A  B  C" broken between names to fit the 270 px screen, at most UnlockLines lines
        /// (the last one ends with "+N MORE" if even that is not enough).
        /// </summary>
        public static List<string> UnlockText(IReadOnlyList<string> names)
        {
            var max = TextWrap.MaxCharsOnScreen(1);
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
