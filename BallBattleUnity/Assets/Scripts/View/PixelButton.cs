using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Pixel button: 1px border + fill + optional label/icon, hit-tested in native pixels.
    /// A click = press and release both inside the button (finger slide-off cancels). Only active buttons react;
    /// input comes from PixelInput's ordered event queue.
    /// </summary>
    public sealed class PixelButton : MonoBehaviour
    {
        public event Action Clicked;

        public Rect Area { get; private set; }

        /// <summary>
        /// Cue (AudioIds) played through UiAudio on a real tap, before Clicked. Default ui_click; BACK-style buttons use
        /// ui_back; null when the screen plays its own sound (cards: card_pick / ui_denied, sliders).
        /// </summary>
        public string ClickCue = AudioIds.UiClick;

        /// <summary>Native point of the last press that started inside this button.</summary>
        public Vector2 LastPressNative { get; private set; }

        /// <summary>Native point of the last release that ended a press started inside this button (set before Clicked).</summary>
        public Vector2 LastReleaseNative { get; private set; }

        SpriteRenderer border, fill;
        PixelText label;
        Color32 fillColor, borderColor;
        bool held;

        public static PixelButton Create(Transform parent, string name, ArtLibrary art, Rect area, string text, int textScale,
                                         Color32 fillColor, Color32 borderColor, Color32 textColor, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<PixelButton>();
            b.Area = area;
            b.border = PixelRect.Create(go.transform, "Border", art.Pixel, borderColor, order);
            b.fill = PixelRect.Create(go.transform, "Fill", art.Pixel, fillColor, order + 1);
            PixelRect.Set(b.border, area);
            PixelRect.Set(b.fill, area.xMin + 1, area.yMin + 1, area.xMax - 1, area.yMax - 1);
            b.fillColor = fillColor;
            b.borderColor = borderColor;
            if (!string.IsNullOrEmpty(text))
            {
                var textY = area.center.y - PixelFontData.GlyphHeight * textScale / 2f;
                b.label = PixelText.Create(go.transform, "Label", art.Font, new Vector2(area.center.x, Mathf.Floor(textY)), PixelText.Align.Center, textScale, order + 3);
                b.label.Set(text, textColor);
            }
            return b;
        }

        /// <summary>Order for icons placed on top of the fill.</summary>
        public int IconOrder => fill.sortingOrder + 1;

        public void SetColors(Color32 newFill, Color32 newBorder)
        {
            fillColor = newFill;
            borderColor = newBorder;
            fill.color = held ? Darken(newFill) : newFill;
            border.color = newBorder;
        }

        public void SetText(string text, Color32 color) => label?.Set(text, color);

        public Color32 FillColor => fillColor;
        public Color32 BorderColor => borderColor;
        public string LabelText => label != null ? label.Text : string.Empty;

        public bool Contains(Vector2 nativePoint) => Area.Contains(nativePoint);

        static Color32 Darken(Color32 c) => new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), c.a);

        void SetHeld(bool value)
        {
            if (held == value) return;
            held = value;
            fill.color = held ? Darken(fillColor) : fillColor;
        }

        /// <summary>Events with a smaller sequence number happened before this button was on screen: ignored.</summary>
        long shownSeq;

        /// <summary>From now on only taps that start after this moment count (called when the button appears).</summary>
        public void NoteShown()
        {
            shownSeq = PixelInput.NextSeq;
            SetHeld(false);
        }

        /// <summary>NoteShown on every active button under <paramref name="root"/> (screens call it when they appear).</summary>
        public static void NoteShownAll(Transform root)
        {
            if (root == null) return;
            foreach (var b in root.GetComponentsInChildren<PixelButton>(false)) b.NoteShown();
        }
        void OnEnable()
        {
            NoteShown();
            PixelInput.Register(this);
        }

        void OnDisable()
        {
            SetHeld(false);
            PixelInput.Unregister(this);
        }

        /// <summary>
        /// One pointer event, in order (see PixelInput). A click = press AND release both inside this button, the
        /// press after the button appeared. A press elsewhere, or one from before, never ends in a click here.
        /// </summary>
        internal void Handle(PixelInput.PointerEvent e)
        {
            if (e.Seq < shownSeq) return;
            if (e.Down)
            {
                SetHeld(Contains(e.Native));
                if (held) LastPressNative = e.Native;
                return;
            }
            if (!held) return;
            LastReleaseNative = e.Native;
            var inside = Contains(e.Native);
            SetHeld(false);
            if (!inside) return;
            UiAudio.Play(ClickCue);
            Clicked?.Invoke();
        }

        /// <summary>True while a press that started inside is still down.</summary>
        public bool IsHeld => held;

        /// <summary>Test/automation hook: behave as if tapped.</summary>
        public void SimulateClick() => Clicked?.Invoke();
    }
}
