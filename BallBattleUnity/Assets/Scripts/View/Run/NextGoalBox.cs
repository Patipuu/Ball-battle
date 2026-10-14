using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Shared NEXT GOAL panel, fixed 254x60 so a taller font (Vietnamese) never moves the screen around it.
    /// Left: "NEXT GOAL", goal name (HUD yellow, scale 2), "UNLOCKS: A  B +N", each in its own 13*scale px cell (BB-FONT1).
    /// Right: "current/max" and a 66x4 bar under it.
    /// When everything is unlocked: "ALL UNLOCKED" in green and "WINS n", no number or bar.
    /// Placed by its top-left corner in 270x480 screen pixels (origin top-left).
    /// </summary>
    public sealed class NextGoalBox : MonoBehaviour
    {
        public const int Width = 254;
        public const int Height = 60;
        const int Pad = 6;
        const int BarWidth = 66;
        const int BarHeight = 4;
        /// <summary>Bar top, px below the box top (under the count).</summary>
        const int BarTop = 31;

        public static readonly Color32 Green = new Color32(110, 220, 110, 255);

        PixelText label, title, sub, count;
        SpriteRenderer barBack, barFill;
        float left, top, barLeft;

        /// <summary>What the box shows now (tests).</summary>
        public NextGoalInfo Info { get; private set; }
        public PixelText TitleText => title;
        public PixelText SubText => sub;
        public PixelText CountText => count;
        public float BarFillWidth => barFill.gameObject.activeSelf ? barFill.transform.localScale.x : 0f;

        /// <summary>Screen (top-left origin) to the parent's local space (centre origin, y up).</summary>
        public static float LocalX(float screenX) => screenX - TextWrap.ScreenWidth / 2f;
        public static float LocalTop(float screenY) => TextWrap.ScreenHeight / 2f - screenY;

        public static NextGoalBox Create(Transform parent, ArtLibrary art, int screenX, int screenY, int order)
        {
            var go = new GameObject("NextGoal");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<NextGoalBox>();
            b.Build(art, screenX, screenY, order);
            return b;
        }

        void Build(ArtLibrary art, int screenX, int screenY, int order)
        {
            left = LocalX(screenX);
            top = LocalTop(screenY);
            PixelRect.Set(PixelRect.Create(transform, "Border", art.Pixel, Palette.Wall, order + 1), left, top - Height, left + Width, top);
            PixelRect.Set(PixelRect.Create(transform, "Fill", art.Pixel, RunUi.Panel, order + 2), left + 1, top - Height + 1, left + Width - 1, top - 1);

            label = TextAt(art, left + Pad, 2, 1, PixelText.Align.Left, order + 4);       // cell 2..15
            label.Set(RunStrings.NextGoal, Palette.TextDim);
            title = TextAt(art, left + Pad, 15, 2, PixelText.Align.Left, order + 4);      // cell 15..41
            sub = TextAt(art, left + Pad, 43, 1, PixelText.Align.Left, order + 4);        // cell 43..56
            count = TextAt(art, left + Width - Pad, 15, 1, PixelText.Align.Right, order + 4);   // cell 15..28

            barLeft = left + Width - Pad - BarWidth;
            barBack = PixelRect.Create(transform, "BarBack", art.Pixel, Palette.HpBack, order + 3);
            PixelRect.Set(barBack, barLeft, top - BarTop - BarHeight, barLeft + BarWidth, top - BarTop);
            barFill = PixelRect.Create(transform, "BarFill", art.Pixel, Palette.WallWarning, order + 4);
        }

        /// <summary>Text whose top edge is <paramref name="dy"/> pixels below the box top.</summary>
        PixelText TextAt(ArtLibrary art, float x, int dy, int scale, PixelText.Align align, int order) =>
            PixelText.Create(transform, "Text", art.Font, new Vector2(x, top - dy - PixelFont.CellHeight * scale), align, scale, order);

        public void Show(ProgressData progress)
        {
            var p = progress ?? new ProgressData();
            var g = UnlockRules.NextGoal(p);
            Info = g;
            if (g.AllDone)
            {
                title.Set(RunStrings.AllUnlocked, Green);
                sub.Set(RunStrings.Wins(p.RunsWon), Palette.TextDim);
                count.Set("", Palette.Text);
                barBack.gameObject.SetActive(false);
                barFill.gameObject.SetActive(false);
                return;
            }
            title.Set(RunStrings.GoalTitle(g.GoalId), Palette.WallWarning);
            sub.Set(RunStrings.UnlocksLine(g.RewardIds), Palette.TextDim);   // empty (hidden) when nothing new
            count.Set(RunStrings.Progress(g.Current, g.Max), Palette.Text);
            barBack.gameObject.SetActive(true);
            var fill = g.Max > 0 ? Mathf.Round(BarWidth * Mathf.Clamp01((float)g.Current / g.Max)) : 0f;
            barFill.gameObject.SetActive(fill > 0f);
            PixelRect.Set(barFill, barLeft, top - BarTop - BarHeight, barLeft + fill, top - BarTop);
        }
    }
}
