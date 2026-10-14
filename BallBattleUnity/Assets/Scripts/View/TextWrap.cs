using System;
using System.Collections.Generic;

namespace BallBattle.View
{
    /// <summary>
    /// Line breaking for PixelText (it draws one line only). Breaks between items, never inside one. Widths are real pixel
    /// widths (PixelFont.MeasureWidth, variable-width font), never a character count.
    /// </summary>
    public static class TextWrap
    {
        /// <summary>Screen width in native pixels (portrait 270x480).</summary>
        public const int ScreenWidth = 270;
        public const int ScreenHeight = 480;
        /// <summary>Text keeps this many pixels from the screen edges.</summary>
        public const int ScreenMargin = 4;

        /// <summary>Widest text (pixels at scale 1) that fits the screen width minus margins when drawn at <paramref name="scale"/>.</summary>
        public static int MaxWidthOnScreen(int scale) => (ScreenWidth - 2 * ScreenMargin) / scale;

        /// <summary>True when <paramref name="text"/> at <paramref name="scale"/> fits <see cref="MaxWidthOnScreen"/>.</summary>
        public static bool FitsScreen(string text, int scale) => PixelFont.MeasureWidth(text) <= MaxWidthOnScreen(scale);

        /// <summary>
        /// Join <paramref name="items"/> after <paramref name="prefix"/>, separated by <paramref name="separator"/>,
        /// into lines at most <paramref name="maxWidth"/> pixels wide (scale 1). Continuation lines are indented with spaces
        /// to line up under the first item when <paramref name="indent"/> is set. An item longer than a line gets a line of
        /// its own (cut to fit).
        /// </summary>
        public static List<string> Items(IReadOnlyList<string> items, string prefix, string separator, int maxWidth, bool indent = false)
        {
            var lines = new List<string>();
            if (items == null || items.Count == 0) return lines;
            var pad = indent ? IndentFor(prefix) : string.Empty;
            var line = prefix;
            var lineHasItem = false;
            foreach (var item in items)
            {
                var candidate = lineHasItem ? line + separator + item : line + item;
                if (PixelFont.MeasureWidth(candidate) <= maxWidth || !lineHasItem)
                {
                    line = Cut(candidate, maxWidth);
                    lineHasItem = true;
                    continue;
                }
                lines.Add(line.TrimEnd());
                line = Cut(pad + item, maxWidth);
            }
            lines.Add(line.TrimEnd());
            return lines;
        }

        /// <summary>
        /// One line when <paramref name="text"/> fits <paramref name="maxWidth"/> px (scale 1); otherwise broken at the item
        /// separator "  " (then at single spaces) into as few lines as fit. A word wider than a line keeps its own line.
        /// Never drops or shortens text.
        /// </summary>
        public static List<string> SplitToFit(string text, int maxWidth)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text)) return lines;
            if (PixelFont.MeasureWidth(text) <= maxWidth) { lines.Add(text); return lines; }
            var sep = text.Contains("  ") ? "  " : " ";
            var parts = text.Split(new[] { sep }, StringSplitOptions.None);
            var line = "";
            foreach (var part in parts)
            {
                var candidate = line.Length == 0 ? part : line + sep + part;
                if (line.Length > 0 && PixelFont.MeasureWidth(candidate) > maxWidth)
                {
                    lines.Add(line);
                    line = part;
                }
                else line = candidate;
            }
            if (line.Length > 0) lines.Add(line);
            if (sep == "  ")   // a piece still too wide: break it at single spaces
            {
                var fixedLines = new List<string>();
                foreach (var l in lines)
                    if (PixelFont.MeasureWidth(l) > maxWidth && l.Contains(" ")) fixedLines.AddRange(SplitToFit(l.Replace("  ", " "), maxWidth));
                    else fixedLines.Add(l);
                return fixedLines;
            }
            return lines;
        }

        /// <summary>Spaces as wide as <paramref name="prefix"/> plus the letter gap (an item after them starts where it would after the prefix).</summary>
        public static string IndentFor(string prefix)
        {
            var space = PixelFont.MeasureWidth(" ") + PixelFont.Spacing;
            var n = (PixelFont.MeasureWidth(prefix) + PixelFont.Spacing + space / 2) / space;
            return new string(' ', n);
        }

        /// <summary>Longest start of <paramref name="text"/> that is at most <paramref name="maxWidth"/> px (scale 1).</summary>
        public static string Cut(string text, int maxWidth)
        {
            if (PixelFont.MeasureWidth(text) <= maxWidth) return text;
            var n = text.Length;
            while (n > 0 && PixelFont.MeasureWidth(text.Substring(0, n)) > maxWidth) n--;
            return text.Substring(0, n);
        }
    }
}
