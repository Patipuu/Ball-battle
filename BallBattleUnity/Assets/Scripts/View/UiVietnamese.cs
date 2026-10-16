using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Vietnamese copy for legacy view strings. English remains the stable source text used by
    /// saves and tests. Entries in ui-vi.tsv are either exact strings or templates with {0},
    /// {1}, etc. Templates are matched against the complete rendered sentence, so word order
    /// can change in Vietnamese without changing gameplay data.
    /// </summary>
    public static class UiVietnamese
    {
        const string Resource = "Strings/ui-vi";
        static Dictionary<string, string> exact;
        static List<(Regex pattern, string translation, int specificity)> templates;

        public static string Translate(string source)
        {
            if (StringTable.Current != Lang.Vi || string.IsNullOrEmpty(source)) return source;
            if (exact == null) Load();
            if (exact.TryGetValue(source, out var result)) return result;
            foreach (var row in templates)
            {
                var match = row.pattern.Match(source);
                if (!match.Success) continue;
                result = row.translation;
                for (var i = 0; i < 8; i++)
                    result = result.Replace("{" + i + "}", Translate(match.Groups["v" + i].Value));
                return result;
            }
            return source;
        }

        static void Load()
        {
            exact = new Dictionary<string, string>(StringComparer.Ordinal);
            templates = new List<(Regex, string, int)>();
            var asset = Resources.Load<TextAsset>(Resource);
            if (asset == null) { Debug.LogError("Missing Resources/" + Resource); return; }
            foreach (var raw in asset.text.Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var tab = line.IndexOf('\t');
                if (tab <= 0) continue;
                var english = line.Substring(0, tab).Replace("\\n", "\n").Replace("\\s", " ");
                var vietnamese = line.Substring(tab + 1).Replace("\\n", "\n").Replace("\\s", " ");
                if (!english.Contains("{0}")) { exact[english] = vietnamese; continue; }
                var pattern = new StringBuilder("^");
                var cursor = 0;
                var holes = 0;
                foreach (Match hole in Regex.Matches(english, @"\{([0-7])\}"))
                {
                    holes++;
                    pattern.Append(Regex.Escape(english.Substring(cursor, hole.Index - cursor)));
                    pattern.Append("(?<v").Append(hole.Groups[1].Value).Append(@">.*?)");
                    cursor = hole.Index + hole.Length;
                }
                pattern.Append(Regex.Escape(english.Substring(cursor))).Append('$');
                templates.Add((new Regex(pattern.ToString(), RegexOptions.CultureInvariant), vietnamese, english.Length - 3 * holes));
            }
            // A broad template such as "NEXT: FIGHT {0}/{1}" can absorb a later
            // "  RETRY" suffix in its last capture. Match the most specific copy first.
            templates.Sort((a, b) => b.specificity.CompareTo(a.specificity));
        }
    }
}
