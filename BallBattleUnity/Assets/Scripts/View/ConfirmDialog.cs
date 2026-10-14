using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// YES / NO question over a screen (SKIP FREE PICK?, SWAP TO X?, ABANDON CURRENT RUN?). Rules for every dialog:
    ///   - opening it drops all input for OpenInputBlockSeconds, and YES / NO only take taps that start after it opened;
    ///   - neither YES nor NO lies on the area that was tapped to open it (the card, NEW RUN, FIGHT), so tapping that
    ///     spot again - repeatedly - can never answer the question. The dialog takes the first of a few fixed placements
    ///     whose buttons both clear that area.
    /// While open, the screen under it must ignore taps (IsOpen); on close, Closed fires so it can forget the tap that
    /// closed the dialog.
    /// </summary>
    public sealed class ConfirmDialog
    {
        public const float OpenInputBlockSeconds = 0.3f;
        public const float PanelWidth = 224f;
        public const float PanelHeight = 92f;

        /// <summary>Panel bottoms tried in order: the classic middle spot first, then high, then low.</summary>
        public static readonly float[] PanelBottoms = { -66f, 64f, -150f };

        public static Rect PanelAt(int layout) => new Rect(-PanelWidth / 2f, PanelBottoms[layout], PanelWidth, PanelHeight);
        public static Rect YesAt(int layout) => new Rect(-102f, PanelBottoms[layout] + 8f, 92f, 28f);
        public static Rect NoAt(int layout) => new Rect(10f, PanelBottoms[layout] + 8f, 92f, 28f);

        /// <summary>First placement whose YES and NO both stay off <paramref name="opener"/> (the area tapped to open).</summary>
        public static int LayoutFor(Rect opener)
        {
            for (var i = 0; i < PanelBottoms.Length; i++)
                if (!YesAt(i).Overlaps(opener) && !NoAt(i).Overlaps(opener)) return i;
            return 0;
        }

        sealed class Layout
        {
            public GameObject Root;
            /// <summary>The question at scale 2, or at scale 1 when scale 2 would not fit the panel (only one is shown).</summary>
            public PixelText Question, QuestionSmall, Info;
            public PixelText ShownQuestion => Question.Text.Length > 0 ? Question : QuestionSmall;
        }

        Transform root;
        GameObject shade;
        Layout[] layouts;
        int current = -1;
        Action onYes, onNo;

        /// <summary>Fires after the dialog closes (either answer, or Close).</summary>
        public event Action Closed;

        public static ConfirmDialog Create(Transform parent, ArtLibrary art, int order, string name = "Confirm")
        {
            var d = new ConfirmDialog();
            d.root = new GameObject(name).transform;
            d.root.SetParent(parent, false);
            d.shade = PixelRect.Create(d.root, "Shade", art.Pixel, new Color32(16, 16, 26, 200), order).gameObject;
            PixelRect.Set(d.shade.GetComponent<SpriteRenderer>(), -135, -240, 135, 240);
            d.shade.SetActive(false);
            d.layouts = new Layout[PanelBottoms.Length];
            for (var i = 0; i < d.layouts.Length; i++)
            {
                var l = new Layout { Root = new GameObject("Layout" + i) };
                l.Root.transform.SetParent(d.root, false);
                var p = PanelAt(i);
                var t = l.Root.transform;
                PixelRect.Set(PixelRect.Create(t, "Border", art.Pixel, Palette.WallWarning, order + 1), p.xMin, p.yMin, p.xMax, p.yMax);
                PixelRect.Set(PixelRect.Create(t, "Fill", art.Pixel, RunUi.Panel, order + 2), p.xMin + 1, p.yMin + 1, p.xMax - 1, p.yMax - 1);
                // BB-FONT1 cells: question 6..32 px under the top (scale 2; scale 1 sits at 13..26), info 36..49, buttons 56..84.
                l.Question = PixelText.Create(t, "Question", art.Font, new Vector2(0, p.yMax - 6 - 2 * PixelFont.CellHeight), PixelText.Align.Center, 2, order + 4);
                l.QuestionSmall = PixelText.Create(t, "QuestionSmall", art.Font, new Vector2(0, p.yMax - 13 - PixelFont.CellHeight), PixelText.Align.Center, 1, order + 4);
                l.Info = PixelText.Create(t, "Info", art.Font, new Vector2(0, p.yMax - 36 - PixelFont.CellHeight), PixelText.Align.Center, 1, order + 4);
                PixelButton.Create(t, "Yes", art, YesAt(i), "YES", 2, Palette.Floor, Palette.WallWarning, Palette.WallWarning, order + 3).Clicked += () => d.Press(true);
                var no = PixelButton.Create(t, "No", art, NoAt(i), "NO", 2, Palette.Floor, Palette.Text, Palette.Text, order + 3);
                no.ClickCue = AudioIds.UiBack;
                no.Clicked += () => d.Press(false);
                l.Root.SetActive(false);
                d.layouts[i] = l;
            }
            return d;
        }

        public bool IsOpen => current >= 0;
        public Transform Root => root;
        public string Question => IsOpen ? layouts[current].ShownQuestion.Text : "";
        public Rect PanelArea => PanelAt(Mathf.Max(0, current));
        public Rect YesArea => YesAt(Mathf.Max(0, current));
        public Rect NoArea => NoAt(Mathf.Max(0, current));

        /// <summary>Ask <paramref name="question"/>; <paramref name="opener"/> = the area tapped to open it (kept clear of YES/NO).</summary>
        public void Open(string question, string info, Rect opener, Action yes, Action no = null)
        {
            Hide();
            current = LayoutFor(opener);
            onYes = yes;
            onNo = no;
            var l = layouts[current];
            var big = PixelFont.MeasureWidth(question) * 2 <= PanelWidth - 8f;
            l.Question.Set(big ? question : "", Palette.WallWarning);
            l.QuestionSmall.Set(big ? "" : question, Palette.WallWarning);
            l.Info.Set(info ?? "", Palette.TextDim);
            shade.SetActive(true);
            l.Root.SetActive(true);
            PixelInput.BlockFor(OpenInputBlockSeconds);
            PixelButton.NoteShownAll(l.Root.transform);   // only taps that start after the dialog opened
        }

        /// <summary>Answer it (tap on YES / NO, or tests). Closes first, then runs the answer.</summary>
        public void Press(bool yes)
        {
            if (!IsOpen) return;
            var action = yes ? onYes : onNo;
            Hide();
            Closed?.Invoke();
            action?.Invoke();
        }

        /// <summary>Close without answering.</summary>
        public void Close()
        {
            if (!IsOpen) return;
            Hide();
            Closed?.Invoke();
        }

        void Hide()
        {
            foreach (var l in layouts) l.Root.SetActive(false);
            shade.SetActive(false);
            current = -1;
            onYes = onNo = null;
        }
    }
}
