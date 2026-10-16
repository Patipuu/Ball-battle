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
        /// <summary>TAP-4: a press that starts inside the button (fires at once, before any release).</summary>
        public event Action Pressed;

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

        SpriteRenderer border, borderV, fill, shine, lip;
        PixelText label;
        Vector3 labelPos;
        Color32 fillColor, borderColor;
        bool held;

        public static PixelButton Create(Transform parent, string name, ArtLibrary art, Rect area, string text, int textScale,
                                         Color32 fillColor, Color32 borderColor, Color32 textColor, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<PixelButton>();
            b.Area = area;
            // UI1: a chunky pixel button. The border is a plus of two rects, so the four corner pixels stay empty (rounded
            // look). Inside it the face is split into three strips that never overlap: a 1px light shine on top, the
            // fill, and a darker lip at the bottom (2px, 1px on short buttons). Pressed: the shine goes dark, the face
            // darkens and the label drops 1px. Everything stays inside Area.
            b.border = PixelRect.Create(go.transform, "Border", art.Pixel, borderColor, order);
            b.borderV = PixelRect.Create(go.transform, "BorderV", art.Pixel, borderColor, order);
            b.fill = PixelRect.Create(go.transform, "Fill", art.Pixel, fillColor, order + 1);
            b.shine = PixelRect.Create(go.transform, "Shine", art.Pixel, fillColor, order + 1);
            b.lip = PixelRect.Create(go.transform, "Lip", art.Pixel, fillColor, order + 1);
            PixelRect.Set(b.border, area.xMin + 1, area.yMin, area.xMax - 1, area.yMax);
            PixelRect.Set(b.borderV, area.xMin, area.yMin + 1, area.xMax, area.yMax - 1);
            var lipH = area.height >= 14 ? 2 : 1;
            var bevel = area.height >= 8;
            PixelRect.Set(b.shine, area.xMin + 1, area.yMax - 2, area.xMax - 1, bevel ? area.yMax - 1 : area.yMax - 2);
            PixelRect.Set(b.lip, area.xMin + 1, area.yMin + 1, area.xMax - 1, bevel ? area.yMin + 1 + lipH : area.yMin + 1);
            PixelRect.Set(b.fill, area.xMin + 1, bevel ? area.yMin + 1 + lipH : area.yMin + 1, area.xMax - 1, bevel ? area.yMax - 2 : area.yMax - 1);
            b.fillColor = fillColor;
            b.borderColor = borderColor;
            b.Paint();
            if (!string.IsNullOrEmpty(text))
            {
                var textY = area.center.y - PixelFont.CellHeight * textScale / 2f;
                b.label = PixelText.Create(go.transform, "Label", art.Font, new Vector2(area.center.x, Mathf.Floor(textY)), PixelText.Align.Center, textScale, order + 3);
                b.label.Set(text, textColor);
                b.labelPos = b.label.transform.localPosition;
            }
            return b;
        }

        /// <summary>Order for icons placed on top of the fill.</summary>
        public int IconOrder => fill.sortingOrder + 1;

        public void SetColors(Color32 newFill, Color32 newBorder)
        {
            fillColor = newFill;
            borderColor = newBorder;
            Paint();
        }

        public void SetText(string text, Color32 color) => label?.Set(text, color);

        public Color32 FillColor => fillColor;
        public Color32 BorderColor => borderColor;
        public string LabelText => label != null ? label.Text : string.Empty;
        /// <summary>Label scale (0 without a label) and color (tests).</summary>
        public int LabelScale => label != null ? label.TextScale : 0;
        public Color32 LabelColor => label != null ? label.TextColor : default;

        public bool Contains(Vector2 nativePoint) => Area.Contains(nativePoint);

        static Color32 Darken(Color32 c) => Scale(c, 0.6f);
        static Color32 Scale(Color32 c, float k) => new Color32((byte)(c.r * k), (byte)(c.g * k), (byte)(c.b * k), c.a);
        static Color32 Mix(Color32 a, Color32 b, float k) =>
            new Color32((byte)(a.r + (b.r - a.r) * k), (byte)(a.g + (b.g - a.g) * k), (byte)(a.b + (b.b - a.b) * k), a.a);
        static Color32 Lighten(Color32 c, float k) =>
            new Color32((byte)(c.r + (255 - c.r) * k), (byte)(c.g + (255 - c.g) * k), (byte)(c.b + (255 - c.b) * k), c.a);

        /// <summary>Colour of the face strips and label offset for the current fill, border and held state.</summary>
        void Paint()
        {
            border.color = borderColor;
            borderV.color = borderColor;
            var face = held ? Darken(fillColor) : fillColor;
            fill.color = face;
            // A dark face (the secondary buttons) has nothing to lighten: its shine takes the accent (border) colour instead.
            var dark = fillColor.r + fillColor.g + fillColor.b < 3 * 70;
            var lit = dark ? Mix(fillColor, borderColor, 0.45f) : Lighten(fillColor, 0.35f);
            shine.color = held ? Scale(fillColor, 0.45f) : lit;
            lip.color = held ? face : Scale(fillColor, dark ? 0.55f : 0.68f);
            if (label != null) label.transform.localPosition = held ? labelPos + Vector3.down : labelPos;
        }

        void SetHeld(bool value)
        {
            if (held == value) return;
            held = value;
            Paint();
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
                if (held) { LastPressNative = e.Native; Pressed?.Invoke(); }
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
