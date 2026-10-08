using System.Collections.Generic;

namespace BallBattle.View
{
    /// <summary>
    /// 3x5 pixel font definition, shared by the Editor generator (writes font_3x5.png) and PixelText (slices it).
    /// Each glyph: 5 rows top→bottom, '#' = ink. Atlas layout: one row, glyph i at x = i * CellWidth.
    /// </summary>
    public static class PixelFontData
    {
        public const int GlyphWidth = 3;
        public const int GlyphHeight = 5;
        public const int CellWidth = 4;
        /// <summary>Horizontal advance per character when drawing text (glyph + 1px gap).</summary>
        public const int Advance = 4;

        public static readonly KeyValuePair<char, string>[] Glyphs =
        {
            G(' ', "... ... ... ... ..."),
            G('0', "### #.# #.# #.# ###"), G('1', ".#. ##. .#. .#. ###"), G('2', "### ..# ### #.. ###"),
            G('3', "### ..# ### ..# ###"), G('4', "#.# #.# ### ..# ..#"), G('5', "### #.. ### ..# ###"),
            G('6', "### #.. ### #.# ###"), G('7', "### ..# ..# ..# ..#"), G('8', "### #.# ### #.# ###"),
            G('9', "### #.# ### ..# ###"),
            G('A', ".#. #.# ### #.# #.#"), G('B', "##. #.# ##. #.# ##."), G('C', ".## #.. #.. #.. .##"),
            G('D', "##. #.# #.# #.# ##."), G('E', "### #.. ### #.. ###"), G('F', "### #.. ### #.. #.."),
            G('G', ".## #.. #.# #.# .##"), G('H', "#.# #.# ### #.# #.#"), G('I', "### .#. .#. .#. ###"),
            G('J', "..# ..# ..# #.# .#."), G('K', "#.# #.# ##. #.# #.#"), G('L', "#.. #.. #.. #.. ###"),
            G('M', "#.# ### ### #.# #.#"), G('N', "##. #.# #.# #.# #.#"), G('O', ".#. #.# #.# #.# .#."),
            G('P', "##. #.# ##. #.. #.."), G('Q', ".#. #.# #.# ##. .##"), G('R', "##. #.# ##. #.# #.#"),
            G('S', ".## #.. .#. ..# ##."), G('T', "### .#. .#. .#. .#."), G('U', "#.# #.# #.# #.# ###"),
            G('V', "#.# #.# #.# #.# .#."), G('W', "#.# #.# ### ### #.#"), G('X', "#.# #.# .#. #.# #.#"),
            G('Y', "#.# #.# .#. .#. .#."), G('Z', "### ..# .#. #.. ###"),
            G('.', "... ... ... ... .#."), G('-', "... ... ### ... ..."), G(':', "... .#. ... .#. ..."),
            G('/', "..# ..# .#. #.. #.."), G('%', "#.# ..# .#. #.. #.#"), G('+', "... .#. ### .#. ..."),
            G('!', ".#. .#. .#. ... .#."), G('?', "### ..# .#. ... .#."),
        };

        static KeyValuePair<char, string> G(char c, string rows) => new KeyValuePair<char, string>(c, rows);

        public static int AtlasWidth => Glyphs.Length * CellWidth;

        /// <summary>Index of a character in the atlas, or -1 (drawn as a space). Lowercase maps to uppercase.</summary>
        public static int IndexOf(char c)
        {
            c = char.ToUpperInvariant(c);
            for (var i = 0; i < Glyphs.Length; i++)
                if (Glyphs[i].Key == c) return i;
            return -1;
        }

        /// <summary>True if glyph pixel (x, rowFromTop) is ink.</summary>
        public static bool Ink(int glyphIndex, int x, int rowFromTop)
        {
            var rows = Glyphs[glyphIndex].Value.Split(' ');
            return rows[rowFromTop][x] == '#';
        }

        /// <summary>Pixel width of a string at scale 1 (no trailing gap).</summary>
        public static int MeasureWidth(string text) => string.IsNullOrEmpty(text) ? 0 : text.Length * Advance - 1;
    }
}
