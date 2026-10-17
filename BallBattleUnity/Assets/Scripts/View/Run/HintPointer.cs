using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>A single, non-interactive first-run hint. It never creates or disables a button.</summary>
    public sealed class HintPointer : MonoBehaviour
    {
        public sealed class Hint
        {
            public string Id;
            public RunFlow.State Screen;
            public string Line;
            public Vector2 Label;
            public Vector2 Arrow;
        }

        public static readonly Hint[] Hints =
        {
            new Hint { Id = "pick", Screen = RunFlow.State.PickWeapon, Line = "CHOOSE YOUR BALL", Label = new Vector2(0, 112), Arrow = new Vector2(-78, 102) },
            new Hint { Id = "startpick", Screen = RunFlow.State.StartPick, Line = "KEEP THIS CARD ALL RUN", Label = new Vector2(0, -174), Arrow = new Vector2(0, -155) },
            new Hint { Id = "carry", Screen = RunFlow.State.FightResult, Line = "LEFTOVER HP GOES TO THE NEXT FIGHT", Label = new Vector2(0, -108), Arrow = new Vector2(0, -91) },
            new Hint { Id = "shop", Screen = RunFlow.State.Prepare, Line = "SPEND COINS ON ANOTHER CARD", Label = new Vector2(0, 11), Arrow = new Vector2(0, -3) },
            new Hint { Id = "boss", Screen = RunFlow.State.Prepare, Line = "BEAT THE BOSS TO UNLOCK A BALL", Label = new Vector2(0, 11), Arrow = new Vector2(0, -3) },
            new Hint { Id = "next", Screen = RunFlow.State.Summary, Line = "NEXT: UPGRADE A BALL IN BALLS", Label = new Vector2(0, -228), Arrow = new Vector2(0, -210) },
        };

        PixelText label;
        Transform arrow;
        float phase;
        Vector2 arrowBase;
        public string CurrentId { get; private set; }
        public bool Showing => gameObject.activeSelf;
        public string Text => label.Text;

        public static HintPointer Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("HintPointer");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<HintPointer>();
            v.label = PixelText.Create(go.transform, "Line", art.Font, Vector2.zero, PixelText.Align.Center, 1, RunUi.Order + 25);
            v.arrow = CreateArrow(go.transform, art, RunUi.Order + 25);
            v.Hide();
            return v;
        }

        public void Show(Hint hint)
        {
            if (hint == null) { Hide(); return; }
            CurrentId = hint.Id;
            gameObject.SetActive(true);
            label.transform.localPosition = hint.Label;
            arrow.transform.localPosition = hint.Arrow;
            arrowBase = hint.Arrow;
            arrow.localScale = new Vector3(1f, hint.Id == "pick" || hint.Id == "shop" || hint.Id == "boss" ? -1f : 1f, 1f);
            label.Set(hint.Line, Palette.WallWarning);
        }

        public void Hide()
        {
            CurrentId = null;
            gameObject.SetActive(false);
        }

        void Update()
        {
            if (!Showing) return;
            phase += Time.unscaledDeltaTime;
            arrow.localPosition = arrowBase + Vector2.up * Mathf.Round(Mathf.Sin(phase * 5f) * 2f);
        }

        public static Transform CreateArrow(Transform parent, ArtLibrary art, int order)
        {
            var a = new GameObject("PixelArrow").transform;
            a.SetParent(parent, false);
            PixelRect.Set(PixelRect.Create(a, "Stem", art.Pixel, Palette.WallWarning, order), -1, 0, 1, 6);
            PixelRect.Set(PixelRect.Create(a, "Head", art.Pixel, Palette.WallWarning, order), -6, 5, 6, 7);
            PixelRect.Set(PixelRect.Create(a, "Mid", art.Pixel, Palette.WallWarning, order), -4, 7, 4, 9);
            PixelRect.Set(PixelRect.Create(a, "Tip", art.Pixel, Palette.WallWarning, order), -2, 9, 2, 11);
            return a;
        }
    }
}
