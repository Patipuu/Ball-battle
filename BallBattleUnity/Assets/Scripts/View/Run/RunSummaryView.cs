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

        PixelText title, fights, record, unlocks, unlocks2;

        public static RunSummaryView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "RunSummary", art);
            var v = root.gameObject.AddComponent<RunSummaryView>();
            v.title = RunUi.Text(root, art, 0, 160, 3);
            v.fights = RunUi.Text(root, art, 0, 130, 1);
            v.record = RunUi.Text(root, art, 0, 112, 2);
            v.unlocks = RunUi.Text(root, art, 0, 80, 1);
            v.unlocks2 = RunUi.Text(root, art, 0, 70, 1);
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
            unlocks.Set(unlocked.Count > 0 ? "UNLOCKED: " + string.Join(" ", unlocked) : "", Palette.WallWarning);
            unlocks2.Set(unlocked.Count > 0 ? "THEY CAN APPEAR IN LATER RUNS" : "", Palette.TextDim);
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
