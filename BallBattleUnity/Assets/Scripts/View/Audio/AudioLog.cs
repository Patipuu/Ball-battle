using System;
using System.Globalization;
using System.IO;
using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Debug log of hazard cues (BB-SFX2), off by default. On with the -bbAudioLog command-line switch or <see cref="ForceOn"/>
    /// (tests). One line per WallBounce / ObstacleHit cue: event type, event tick, ActiveTick, ball, damage, the tick that ball's
    /// HP last changed as seen by the View, the cue id chosen and whether the per-tick gate let it play. Lines go to the
    /// console; in standalone builds they are also appended to Application.persistentDataPath/audio_log.txt, whose path is
    /// printed once at startup.
    /// </summary>
    public static class AudioLog
    {
        public const string FileName = "audio_log.txt";
        /// <summary>Most lines kept per director in AudioDirector.LogLines.</summary>
        public const int MaxLines = 256;

        /// <summary>Test switch: logs even without -bbAudioLog.</summary>
        public static bool ForceOn;

        static bool announced;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            ForceOn = false;
            announced = false;
        }

        public static bool Enabled => ForceOn || DebugOptions.AudioLogOn;

        public static string FilePath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Called when the AudioDirector is created: prints where the file goes (standalone builds, once).</summary>
        public static void AnnounceOnce()
        {
            if (!Enabled || announced) return;
            announced = true;
#if !UNITY_EDITOR
            try
            {
                File.AppendAllText(FilePath, $"--- audio log {DateTime.Now:yyyy-MM-dd HH:mm:ss} ---\n");
                Debug.Log("[Audio] log file: " + FilePath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Audio] cannot write " + FilePath + ": " + ex.Message);
            }
#endif
        }

        public static string Line(SimEvent e, MatchSim sim, float damage, int hpChangedTick, string cue, bool allowed) =>
            string.Format(CultureInfo.InvariantCulture,
                "[Audio] {0} tick={1} active={2} ball={3} dmg={4:0.00} hpChangedTick={5} cue={6} allowed={7}",
                e.Type, e.Tick, sim != null ? sim.ActiveTick.ToString(CultureInfo.InvariantCulture) : "-", e.A, damage, hpChangedTick, cue,
                allowed ? "yes" : "no");

        /// <summary>BB-04 boss cue: "[Audio] Boss tick=.. active=.. event=ShellOn cue=boss_shellking_hit result=played|missing|gated".</summary>
        public static string BossLine(SimEvent e, MatchSim sim, string cue, AudioDirector.BossCueResult result) =>
            string.Format(CultureInfo.InvariantCulture,
                "[Audio] Boss tick={0} active={1} event={2} cue={3} result={4}",
                e.Tick, sim != null ? sim.ActiveTick.ToString(CultureInfo.InvariantCulture) : "-", (BallBattle.Sim.Boss.BossEvent)e.Variant, cue,
                result.ToString().ToLowerInvariant());

        public static void Write(string line, AudioDirector director)
        {
            if (director != null)
            {
                if (director.LogLines.Count >= MaxLines) director.LogLines.RemoveAt(0);
                director.LogLines.Add(line);
            }
            Debug.Log(line);
#if !UNITY_EDITOR
            try { File.AppendAllText(FilePath, line + "\n"); }
            catch (Exception) { /* logging must never break the game */ }
#endif
        }
    }
}
