using System.Collections.Generic;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;

namespace BallBattle.View
{
    /// <summary>BB-04 boss names, Prepare hints and the summary line (EN rows in strings-en.csv; VI falls back to EN).</summary>
    public static class BossText
    {
        public static string Name(string bossId)
        {
            switch (bossId)
            {
                case BossIds.ShellKing: return StringTable.Get(StringKeys.BossShellKingName);
                case BossIds.Magnet: return StringTable.Get(StringKeys.BossMagnetName);
                case BossIds.Mimic: return StringTable.Get(StringKeys.BossMimicName);
                case BossIds.Warden: return StringTable.Get(StringKeys.BossWardenName);
                default: return (bossId ?? "").ToUpperInvariant();
            }
        }

        /// <summary>One line under the boss name on Prepare (what the mechanic does).</summary>
        public static string Hint(string bossId)
        {
            switch (bossId)
            {
                case BossIds.ShellKing: return StringTable.Get(StringKeys.BossShellKingHint);
                case BossIds.Magnet: return StringTable.Get(StringKeys.BossMagnetHint);
                case BossIds.Mimic: return StringTable.Get(StringKeys.BossMimicHint);
                case BossIds.Warden: return StringTable.Get(StringKeys.BossWardenHint);
                default: return "";
            }
        }

        /// <summary>
        /// BB-FIX4: the boss name, then its Prepare hint wrapped exactly as the Prepare ENEMY box wraps it (at most two lines), for the
        /// loss result and CHOOSE YOUR RETRY screens. Same strings as Prepare (Name / Hint); empty list for an unknown or empty id.
        /// </summary>
        public static List<string> NameAndHint(string bossId)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(bossId) || string.IsNullOrEmpty(Hint(bossId))) return lines;
            lines.Add(Name(bossId));
            lines.AddRange(PrepareView.BossLines(null, bossId, PrepareView.TraitsWidth));
            return lines;
        }

        /// <summary>MIMIC's preview name: "COPIES YOU" (its weapon and traits are only known when the fight starts).</summary>
        public static string CopiesYou => StringTable.Get(StringKeys.BossCopiesYou);

        /// <summary>Bosses this run met, in fight order, each once: (boss id, beaten?).</summary>
        public static List<(string id, bool beaten)> MetThisRun(RunState run)
        {
            var list = new List<(string id, bool beaten)>();
            if (run == null) return list;
            foreach (var f in run.History)
            {
                if (string.IsNullOrEmpty(f.BossId)) continue;
                var i = list.FindIndex(x => x.id == f.BossId);
                if (i < 0) list.Add((f.BossId, f.Won));
                else if (f.Won) list[i] = (f.BossId, true);
            }
            return list;
        }

        /// <summary>Summary line: "SHELL KING BEATEN  MIMIC MET"; "" when the run met no boss.</summary>
        public static string SummaryLine(RunState run)
        {
            var parts = new List<string>();
            foreach (var (id, beaten) in MetThisRun(run))
                parts.Add(string.Format(StringTable.Get(beaten ? StringKeys.BossBeaten : StringKeys.BossMet), Name(id)));
            return string.Join("  ", parts);
        }
    }
}
