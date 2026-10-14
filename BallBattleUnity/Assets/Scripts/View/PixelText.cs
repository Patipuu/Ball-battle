using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Text drawn with the variable-width 5x7 pixel font (PixelFont, BB-FONT1), one SpriteRenderer per glyph (pooled).
    /// The position is the bottom-left (Left), bottom-centre (Center) or bottom-right (Right) of a 13*scale px tall cell
    /// (marks above, body, dot below). Positions are whole pixels, scale is an integer, so text stays crisp in the
    /// 270x480 pixel-perfect view. Text is stored NFC-normalized and uppercase (PixelFont.Normalize).
    /// </summary>
    public sealed class PixelText : MonoBehaviour
    {
        public enum Align { Left, Center, Right }

        readonly List<SpriteRenderer> glyphs = new List<SpriteRenderer>();
        PixelFont font;
        int sortingOrder;
        Align align;
        int scale = 1;
        string current;
        Color32 currentColor;
        bool highlighted;

        /// <summary><paramref name="font"/> (the old 3x5 atlas, ArtLibrary.Font) is no longer drawn; kept so callers stay unchanged.</summary>
        public static PixelText Create(Transform parent, string name, Texture2D font, Vector2 position, Align align, int scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), 0f);
            var t = go.AddComponent<PixelText>();
            t.font = PixelFont.Default;
            t.align = align;
            t.scale = Mathf.Max(1, scale);
            t.sortingOrder = sortingOrder;
            return t;
        }

        /// <summary>Text currently shown ("" before the first Set).</summary>
        public string Text => current ?? string.Empty;

        /// <summary>Color of the current text.</summary>
        public Color32 TextColor => currentColor;

        public int TextScale => scale;

        /// <summary>Area the current text covers, in this object's parent space (whole pixels). Empty text = zero width.</summary>
        public Rect Bounds
        {
            get
            {
                var width = PixelFont.MeasureWidth(Text) * scale;
                var startX = align == Align.Left ? 0 : (align == Align.Center ? -(width / 2) : -width);
                var p = transform.localPosition;
                return new Rect(p.x + startX, p.y, width, PixelFont.CellHeight * scale);
            }
        }

        /// <summary>Same area in world space (the 270x480 view is world units = native pixels).</summary>
        public Rect WorldBounds
        {
            get
            {
                var b = Bounds;
                var parent = transform.parent;
                if (parent == null) return b;
                var min = parent.TransformPoint(new Vector3(b.xMin, b.yMin, 0f));
                var max = parent.TransformPoint(new Vector3(b.xMax, b.yMax, 0f));
                return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
            }
        }
        public void Set(string text, Color32 color)
        {
            text = PixelFont.Normalize(text);
            if (text == current && color.Equals(currentColor) && !highlighted) return;
            highlighted = false;
            current = text;
            currentColor = color;

            var width = font.Measure(text) * scale;
            var startX = align == Align.Left ? 0 : (align == Align.Center ? -(width / 2) : -width);

            while (glyphs.Count < text.Length) glyphs.Add(NewGlyph());
            var x = startX;
            for (var i = 0; i < glyphs.Count; i++)
            {
                var sr = glyphs[i];
                if (i >= text.Length) { sr.enabled = false; continue; }
                var g = font.Get(text[i]);
                sr.enabled = !g.Blank;   // spaces (and the blank fallback) only advance
                sr.sprite = g.Sprite;
                sr.color = color;
                sr.transform.localPosition = new Vector3(x, 0f, 0f);
                x += (g.Width + PixelFont.Spacing) * scale;
            }
        }

        /// <summary>
        /// Same as Set, but numbers are drawn in <paramref name="numberColor"/> (card descriptions: 250,210,70).
        /// A number is a run of 0-9 . % + - / X S without spaces that contains a digit ("+15%", "X1.5", "0.7/S", "3S").
        /// </summary>
        public void SetHighlighted(string text, Color32 color, Color32 numberColor)
        {
            highlighted = false;
            current = null;   // force a full redraw
            Set(text, color);
            text = current;   // normalized: glyph i = character i
            highlighted = true;
            for (var i = 0; i < text.Length && i < glyphs.Count; i++)
                if (IsNumberChar(text, i)) glyphs[i].color = numberColor;
        }

        /// <summary>Color of character <paramref name="index"/> as drawn (tests).</summary>
        public Color32 GlyphColor(int index) => index < glyphs.Count ? (Color32)glyphs[index].color : currentColor;

        static bool NumberLike(char c) => (c >= '0' && c <= '9') || c == '.' || c == '%' || c == '+' || c == '-' || c == '/' || c == 'X' || c == 'S';

        /// <summary>True when character <paramref name="i"/> belongs to a number token (see SetHighlighted).</summary>
        public static bool IsNumberChar(string text, int i)
        {
            if (!NumberLike(text[i])) return false;
            var a = i;
            while (a > 0 && NumberLike(text[a - 1])) a--;
            var b = i;
            while (b < text.Length - 1 && NumberLike(text[b + 1])) b++;
            for (var k = a; k <= b; k++) if (text[k] >= '0' && text[k] <= '9') return true;
            return false;
        }

        SpriteRenderer NewGlyph()
        {
            var go = new GameObject("g");
            go.transform.SetParent(transform, false);
            go.transform.localScale = new Vector3(scale, scale, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            return sr;
        }
    }
}
