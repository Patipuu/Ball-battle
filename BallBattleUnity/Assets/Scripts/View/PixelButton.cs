using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Pixel button: 1px border + fill + optional label/icon, hit-tested in native pixels.
    /// A click = press and release both inside the button (finger slide-off cancels). Only active buttons react.
    /// </summary>
    public sealed class PixelButton : MonoBehaviour
    {
        public event Action Clicked;

        public Rect Area { get; private set; }

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

        public bool Contains(Vector2 nativePoint) => Area.Contains(nativePoint);

        static Color32 Darken(Color32 c) => new Color32((byte)(c.r * 0.6f), (byte)(c.g * 0.6f), (byte)(c.b * 0.6f), c.a);

        void SetHeld(bool value)
        {
            if (held == value) return;
            held = value;
            fill.color = held ? Darken(fillColor) : fillColor;
        }

        void OnDisable() => SetHeld(false);

        void Update()
        {
            if (PixelInput.PressedThisFrame(out var down) && Contains(down)) SetHeld(true);
            if (!held) return;
            if (PixelInput.ReleasedThisFrame(out var up))
            {
                var inside = Contains(up);
                SetHeld(false);
                if (inside) Clicked?.Invoke();
            }
            else if (!PixelInput.Held) SetHeld(false);
        }

        /// <summary>Test/automation hook: behave as if tapped.</summary>
        public void SimulateClick() => Clicked?.Invoke();
    }
}
