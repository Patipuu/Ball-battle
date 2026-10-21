using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Data-driven variable-width pixel font (BB-FONT1): Resources/Fonts/pixelfont_5x7.json, a copy of Art's file
    /// { "&lt;char&gt;": ["row0", ..., "row12"] }. Every glyph is a 13-row cell of '.'/'#' rows, all rows of one glyph the same
    /// width (2..7 px; the comma is 2): rows 0-2 marks above (Vietnamese tones/hats), row 3 blank, rows 4-10 the 5x7 body, row 11 blank,
    /// row 12 the dot below (nặng). Letters are 1 px apart. Uppercase only: lowercase maps to uppercase, input is NFC-normalized.
    /// A character the font lacks draws as the fallback (the blank space glyph, as the old 3x5 font did) and is logged once
    /// (Editor only). The glyph atlas is built at runtime from the data (point filtered); the old 3x5 font (PixelFontData,
    /// font_3x5.png) stays in the repo but is no longer drawn.
    /// </summary>
    public sealed class PixelFont
    {
        public const string ResourcePath = "Fonts/pixelfont_5x7";
        /// <summary>Rows per glyph cell = line height at scale 1.</summary>
        public const int CellHeight = 13;
        /// <summary>Gap between letters at scale 1.</summary>
        public const int Spacing = 1;
        public const int MinGlyphWidth = 2;   // BB-SFX2: the v2 file's comma is 2 px wide
        public const int MaxGlyphWidth = 7;
        /// <summary>Body rows (cap height 7): rows 4..10 of the cell.</summary>
        public const int BodyTopRow = 4, BodyBottomRow = 10;
        public const char Fallback = ' ';
        /// <summary>The gold coin icon: written in text instead of the word GOLD / VÀNG ("¤ 120"). Drawn in its own colours.</summary>
        public const char Coin = '\u00A4';
        /// <summary>Coin pixels (rows 0..12 of the cell, 7 wide): o outline, y gold, w shine, d shade, '.' clear.</summary>
        static readonly string[] CoinRows =
        {
            ".......", ".......", ".......", ".......",
            "..ooo..", ".owyyo.", "owyyydo", "oyydydo", "oyyyddo", ".odddo.", "..ooo..",
            ".......", ".......",
        };
        static readonly Color32 CoinOutline = new Color32(110, 62, 12, 255), CoinGold = new Color32(250, 196, 54, 255),
            CoinShine = new Color32(255, 244, 170, 255), CoinShade = new Color32(214, 136, 30, 255);

        public sealed class Glyph
        {
            public char Char;
            public int Width;
            public string[] Rows;
            public Sprite Sprite;
            public bool Blank;
            /// <summary>Drawn in its own colours (the coin): text colour does not tint it.</summary>
            public bool Colored;
        }

        readonly Dictionary<char, Glyph> glyphs = new Dictionary<char, Glyph>();
#if UNITY_EDITOR
        readonly HashSet<char> warned = new HashSet<char>();
#endif
        Texture2D atlas;
        /// <summary>The coin icon, built with the atlas (not one of the file's glyphs, so Count stays the file's count).</summary>
        Glyph coin;

        public int Count => glyphs.Count;
        public IEnumerable<char> Chars => glyphs.Keys;
        public Texture2D Atlas => atlas;

        static PixelFont cached;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => cached = null;

        /// <summary>The game font (loaded from Resources once).</summary>
        public static PixelFont Default
        {
            get
            {
                if (cached != null && cached.atlas != null) return cached;
                var asset = Resources.Load<TextAsset>(ResourcePath);
                if (asset == null) throw new InvalidOperationException($"PixelFont: Resources/{ResourcePath}.json missing");
                cached = Parse(asset.text);
                cached.BuildAtlas();
                return cached;
            }
        }

        // ---------------- parsing and validation ----------------

        /// <summary>Parses and validates the font JSON (no atlas). Throws FormatException naming the bad glyph.</summary>
        public static PixelFont Parse(string json)
        {
            var font = new PixelFont();
            foreach (var kv in FlatJson.Read(json))
            {
                var key = kv.Key.Normalize(NormalizationForm.FormC);
                if (key.Length != 1) throw new FormatException($"PixelFont: key '{kv.Key}' is not one character");
                var c = key[0];
                var rows = kv.Value;
                if (rows.Count != CellHeight) throw new FormatException($"PixelFont: '{c}' has {rows.Count} rows, expected {CellHeight}");
                var w = rows[0].Length;
                if (w < MinGlyphWidth || w > MaxGlyphWidth) throw new FormatException($"PixelFont: '{c}' width {w} outside {MinGlyphWidth}..{MaxGlyphWidth}");
                var blank = true;
                for (var r = 0; r < rows.Count; r++)
                {
                    if (rows[r].Length != w) throw new FormatException($"PixelFont: '{c}' row {r} is {rows[r].Length} wide, row 0 is {w}");
                    foreach (var ch in rows[r])
                    {
                        if (ch == '#') blank = false;
                        else if (ch != '.') throw new FormatException($"PixelFont: '{c}' row {r} has '{ch}' (only '.' and '#')");
                    }
                }
                if (font.glyphs.ContainsKey(c)) throw new FormatException($"PixelFont: '{c}' defined twice");
                font.glyphs[c] = new Glyph { Char = c, Width = w, Rows = rows.ToArray(), Blank = blank };
            }
            if (!font.glyphs.ContainsKey(Fallback)) throw new FormatException("PixelFont: the space glyph (fallback) is missing");
            return font;
        }

        void BuildAtlas()
        {
            if (!glyphs.ContainsKey(Coin)) coin = new Glyph { Char = Coin, Width = CoinRows[0].Length, Rows = CoinRows, Colored = true };
            var all = new List<Glyph>(glyphs.Values);
            if (coin != null) all.Add(coin);
            var width = 0;
            foreach (var g in all) width += g.Width + 1;
            atlas = new Texture2D(Math.Max(1, width), CellHeight, TextureFormat.RGBA32, false)
            {
                name = "pixelfont_5x7_atlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave,
            };
            var clear = new Color32(0, 0, 0, 0);
            var ink = new Color32(255, 255, 255, 255);
            var pixels = new Color32[atlas.width * CellHeight];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = clear;
            var x = 0;
            foreach (var g in all)
            {
                for (var r = 0; r < CellHeight; r++)
                    for (var col = 0; col < g.Width; col++)
                    {
                        var c = g.Rows[r][col];
                        if (c == '.') continue;
                        pixels[(CellHeight - 1 - r) * atlas.width + x + col] = !g.Colored ? ink
                            : c == 'o' ? CoinOutline : c == 'w' ? CoinShine : c == 'd' ? CoinShade : CoinGold;
                    }
                g.Sprite = Sprite.Create(atlas, new Rect(x, 0, g.Width, CellHeight), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                g.Sprite.name = "glyph_" + g.Char;
                g.Sprite.hideFlags = HideFlags.DontSave;
                x += g.Width + 1;
            }
            atlas.SetPixels32(pixels);
            atlas.Apply(false, false);
        }

        // ---------------- lookup and measuring ----------------

        public bool Has(char c) => glyphs.ContainsKey(c) || (c == Coin && coin != null);

        /// <summary>Glyph of a character of an already normalized string; the fallback (logged once in the Editor) when missing.</summary>
        public Glyph Get(char c)
        {
            if (glyphs.TryGetValue(c, out var g)) return g;
            if (c == Coin && coin != null) return coin;
            if (glyphs.TryGetValue(char.ToUpperInvariant(c), out g)) return g;
#if UNITY_EDITOR
            if (warned.Add(c)) Debug.LogWarning($"PixelFont: no glyph for '{c}' (U+{(int)c:X4}), drawn as the fallback");
#endif
            return glyphs[Fallback];
        }

        /// <summary>Width in pixels at scale 1: the real glyph widths plus 1 px between letters (no trailing gap).</summary>
        public int Measure(string text)
        {
            text = Normalize(text);
            if (text.Length == 0) return 0;
            var w = 0;
            foreach (var c in text) w += Get(c).Width;
            return w + Spacing * (text.Length - 1);
        }

        /// <summary><see cref="Measure"/> with the game font: what every layout uses instead of a fixed character width.</summary>
        public static int MeasureWidth(string text) => string.IsNullOrEmpty(text) ? 0 : Default.Measure(text);

        /// <summary>NFC, then uppercase (the font has capitals only). Plain ASCII without lowercase is returned as is.</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var ascii = true;
            var lower = false;
            foreach (var c in text)
            {
                if (c > 0x7F) { ascii = false; break; }
                if (c >= 'a' && c <= 'z') lower = true;
            }
            if (ascii) return lower ? text.ToUpperInvariant() : text;
            return text.Normalize(NormalizationForm.FormC).ToUpperInvariant().Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Reader for the font file's flat shape only: one object of string keys whose values are arrays of strings.
        /// Handles the JSON string escapes; anything else is a FormatException.
        /// </summary>
        static class FlatJson
        {
            public static List<KeyValuePair<string, List<string>>> Read(string s)
            {
                var list = new List<KeyValuePair<string, List<string>>>();
                var i = 0;
                Ws(s, ref i);
                if (i < s.Length && s[i] == '﻿') { i++; Ws(s, ref i); }
                Expect(s, ref i, '{');
                Ws(s, ref i);
                if (Peek(s, i) == '}') return list;
                while (true)
                {
                    Ws(s, ref i);
                    var key = Str(s, ref i);
                    Ws(s, ref i);
                    Expect(s, ref i, ':');
                    Ws(s, ref i);
                    Expect(s, ref i, '[');
                    var rows = new List<string>();
                    Ws(s, ref i);
                    if (Peek(s, i) != ']')
                        while (true)
                        {
                            Ws(s, ref i);
                            rows.Add(Str(s, ref i));
                            Ws(s, ref i);
                            if (Peek(s, i) == ',') { i++; continue; }
                            break;
                        }
                    Expect(s, ref i, ']');
                    list.Add(new KeyValuePair<string, List<string>>(key, rows));
                    Ws(s, ref i);
                    if (Peek(s, i) == ',') { i++; continue; }
                    Expect(s, ref i, '}');
                    return list;
                }
            }

            static char Peek(string s, int i) => i < s.Length ? s[i] : '\0';

            static void Ws(string s, ref int i)
            {
                while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
            }

            static void Expect(string s, ref int i, char c)
            {
                if (Peek(s, i) != c) throw new FormatException($"PixelFont JSON: expected '{c}' at {i}");
                i++;
            }

            static string Str(string s, ref int i)
            {
                Expect(s, ref i, '"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (i >= s.Length) throw new FormatException("PixelFont JSON: unterminated string");
                    var c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    var e = Peek(s, i++);
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw new FormatException("PixelFont JSON: bad \\u escape");
                            sb.Append((char)Convert.ToInt32(s.Substring(i, 4), 16));
                            i += 4;
                            break;
                        default: throw new FormatException($"PixelFont JSON: bad escape \\{e}");
                    }
                }
            }
        }
    }
}
