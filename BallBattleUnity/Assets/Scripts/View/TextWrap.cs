using System.Collections.Generic;

namespace BallBattle.View
{
    /// <summary>Line breaking for PixelText (it draws one line only). Breaks between items, never inside one.</summary>
    public static class TextWrap
    {
        /// <summary>Screen width in native pixels (portrait 270x480).</summary>
        public const int ScreenWidth = 270;
        public const int ScreenHeight = 480;
        /// <summary>Text keeps this many pixels from the screen edges.</summary>
        public const int ScreenMargin = 4;

        /// <summary>Most characters of <paramref name="scale"/> that fit the screen width minus margins.</summary>
        public static int MaxCharsOnScreen(int scale) => (ScreenWidth - 2 * ScreenMargin + 1) / (PixelFontData.Advance * scale);

        /// <summary>
        /// Join <paramref name="items"/> after <paramref name="prefix"/>, separated by <paramref name="separator"/>,
        /// into lines of at most <paramref name="maxChars"/>. Continuation lines are indented to line up under the
        /// first item when <paramref name="indent"/> is set. An item longer than a line gets a line of its own (cut).
        /// </summary>
        public static List<string> Items(IReadOnlyList<string> items, string prefix, string separator, int maxChars, bool indent = false)
        {
            var lines = new List<string>();
            if (items == null || items.Count == 0) return lines;
            var pad = indent ? new string(' ', prefix.Length) : string.Empty;
            var line = prefix;
            var lineHasItem = false;
            foreach (var item in items)
            {
                var candidate = lineHasItem ? line + separator + item : line + item;
                if (candidate.Length <= maxChars || !lineHasItem)
                {
                    line = candidate.Length > maxChars ? candidate.Substring(0, maxChars) : candidate;
                    lineHasItem = true;
                    continue;
                }
                lines.Add(line.TrimEnd());
                line = pad + item;
                if (line.Length > maxChars) line = line.Substring(0, maxChars);
            }
            lines.Add(line.TrimEnd());
            return lines;
        }
    }
}
