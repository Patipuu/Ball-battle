using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Languages available to the player.</summary>
    public enum Lang { En, Vi }

    /// <summary>Keys of <see cref="StringTable"/> (BB-FIX1b). Every new player-facing string gets a key here and a row in both CSV files.</summary>
    public static class StringKeys
    {
        public const string StartPickNoReroll = "startpick.no_reroll";
        public const string BossAtOne = "run.boss_at.one";
        public const string BossAtTwo = "run.boss_at.two";
        public const string BossAtMany = "run.boss_at.many";
        public const string LoseOnce = "run.lose_once";
        public const string VersusDraw = "versus.draw";

        public static readonly string[] All = { StartPickNoReroll, BossAtOne, BossAtTwo, BossAtMany, LoseOnce, VersusDraw };

        // Boss names, hints and summary labels are localized in both CSV files.
        public const string BossShellKingName = "boss.shellking.name", BossShellKingHint = "boss.shellking.hint";
        public const string BossMagnetName = "boss.magnet.name", BossMagnetHint = "boss.magnet.hint";
        public const string BossMimicName = "boss.mimic.name", BossMimicHint = "boss.mimic.hint";
        public const string BossWardenName = "boss.warden.name", BossWardenHint = "boss.warden.hint";
        public const string BossHunterName = "boss.hunter.name", BossHunterHint = "boss.hunter.hint";
        public const string BossBellName = "boss.bell.name", BossBellHint = "boss.bell.hint";
        public const string BossEmberName = "boss.ember.name", BossEmberHint = "boss.ember.hint";
        public const string BossPrismName = "boss.prism.name", BossPrismHint = "boss.prism.hint";
        public const string BossCopiesYou = "boss.mimic.copies_you";
        public const string BossBeaten = "boss.summary.beaten", BossMet = "boss.summary.met";

        public static readonly string[] Boss =
        {
            BossShellKingName, BossShellKingHint, BossMagnetName, BossMagnetHint, BossMimicName, BossMimicHint,
            BossWardenName, BossWardenHint, BossHunterName, BossHunterHint, BossBellName, BossBellHint,
            BossEmberName, BossEmberHint, BossPrismName, BossPrismHint, BossCopiesYou, BossBeaten, BossMet
        };
    }

    /// <summary>
    /// EN/VI string table: Resources/Strings/strings-en.csv and strings-vi.csv, UTF-8, one "key,text" row per line
    /// (split at the first comma, so the text may hold anything but a line break; header "key,text", blank lines and
    /// lines starting with # are skipped). Text may use string.Format holes {0}, {1}.
    /// The 5x7 pixel font supports Vietnamese. A key missing in VI falls back to EN; a key missing everywhere
    /// returns the key itself (visible on screen, never a crash).
    /// </summary>
    public static class StringTable
    {
        public const string ResourceFolder = "Strings/";

        /// <summary>Language used by <see cref="Get(string)"/>. Vietnamese is the default.</summary>
        public static Lang Current = Lang.Vi;

        static readonly Dictionary<Lang, Dictionary<string, string>> Tables = new Dictionary<Lang, Dictionary<string, string>>();

        /// <summary>Drop cached tables on play start (survives when domain reload is disabled).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            Tables.Clear();
            Current = Lang.Vi;
        }

        public static string FileName(Lang lang) => lang == Lang.Vi ? "strings-vi" : "strings-en";

        public static string Get(string key) => Get(key, Current);

        public static string Get(string key, Lang lang)
        {
            if (key == null) return string.Empty;
            if (Table(lang).TryGetValue(key, out var s)) return s;
            if (lang != Lang.En && Table(Lang.En).TryGetValue(key, out s)) return s;
            return key;
        }

        public static string Format(string key, Lang lang, params object[] args) =>
            string.Format(CultureInfo.InvariantCulture, Get(key, lang), args);

        /// <summary>True when the language's own file has the key (no fallback).</summary>
        public static bool Has(string key, Lang lang) => key != null && Table(lang).ContainsKey(key);

        /// <summary>All rows of a language's file (loaded once from Resources).</summary>
        public static IReadOnlyDictionary<string, string> Table(Lang lang)
        {
            if (Tables.TryGetValue(lang, out var t)) return t;
            var asset = Resources.Load<TextAsset>(ResourceFolder + FileName(lang));
            t = asset != null ? Parse(asset.text) : new Dictionary<string, string>();
            if (asset == null) Debug.LogWarning($"StringTable: Resources/{ResourceFolder}{FileName(lang)} not found");
            Tables[lang] = t;
            return t;
        }

        /// <summary>"key,text" rows to a dictionary (see the class comment for the format). A repeated key keeps the last row.</summary>
        public static Dictionary<string, string> Parse(string csv)
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(csv)) return map;
            foreach (var raw in csv.TrimStart('﻿').Split('\n'))
            {
                var line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                var comma = line.IndexOf(',');
                if (comma <= 0) continue;
                var key = line.Substring(0, comma).Trim();
                if (key == "key") continue;   // header
                map[key] = line.Substring(comma + 1);
            }
            return map;
        }
    }
}
