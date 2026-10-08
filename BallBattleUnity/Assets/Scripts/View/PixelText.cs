using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Text drawn with the 3x5 font atlas, one SpriteRenderer per glyph (pooled). Positions are whole pixels,
    /// scale is an integer, so text stays crisp in the 270x480 pixel-perfect view.
    /// </summary>
    public sealed class PixelText : MonoBehaviour
    {
        public enum Align { Left, Center, Right }

        static readonly Dictionary<Texture2D, Sprite[]> Atlases = new Dictionary<Texture2D, Sprite[]>();

        /// <summary>Drop cached glyph sprites on play start (survives when domain reload is disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => Atlases.Clear();

        readonly List<SpriteRenderer> glyphs = new List<SpriteRenderer>();
        Sprite[] sprites;
        int sortingOrder;
        Align align;
        int scale = 1;
        string current;
        Color32 currentColor;

        public static PixelText Create(Transform parent, string name, Texture2D font, Vector2 position, Align align, int scale, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(Mathf.Round(position.x), Mathf.Round(position.y), 0f);
            var t = go.AddComponent<PixelText>();
            t.sprites = AtlasFor(font);
            t.align = align;
            t.scale = Mathf.Max(1, scale);
            t.sortingOrder = sortingOrder;
            return t;
        }

        static Sprite[] AtlasFor(Texture2D font)
        {
            if (Atlases.TryGetValue(font, out var cached) && cached[0] != null) return cached;
            var arr = new Sprite[PixelFontData.Glyphs.Length];
            for (var i = 0; i < arr.Length; i++)
            {
                var rect = new Rect(i * PixelFontData.CellWidth, 0, PixelFontData.GlyphWidth, PixelFontData.GlyphHeight);
                arr[i] = Sprite.Create(font, rect, Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                arr[i].name = $"glyph_{PixelFontData.Glyphs[i].Key}";
            }
            Atlases[font] = arr;
            return arr;
        }

        public void Set(string text, Color32 color)
        {
            text = text ?? string.Empty;
            if (text == current && color.Equals(currentColor)) return;
            current = text;
            currentColor = color;

            var width = PixelFontData.MeasureWidth(text) * scale;
            var startX = align == Align.Left ? 0 : (align == Align.Center ? -(width / 2) : -width);

            while (glyphs.Count < text.Length) glyphs.Add(NewGlyph());
            for (var i = 0; i < glyphs.Count; i++)
            {
                var sr = glyphs[i];
                if (i >= text.Length) { sr.enabled = false; continue; }
                var index = PixelFontData.IndexOf(text[i]);
                sr.enabled = index > 0; // index 0 is space
                if (!sr.enabled) continue;
                sr.sprite = sprites[index];
                sr.color = color;
                sr.transform.localPosition = new Vector3(startX + i * PixelFontData.Advance * scale, 0f, 0f);
            }
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
