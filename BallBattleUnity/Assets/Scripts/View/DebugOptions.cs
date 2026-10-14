using System;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Tester switches read from the command line. All off by default; none of them touches the sim.
    ///   -bbForceTrait &lt;traitId&gt;  the card offer before fight 1 (the first card pick that exists in the game)
    ///                              always contains that trait (LV1). Id as in TraitRegistry ("second-wind");
    ///                              dashes/underscores/case are ignored, so "secondwind" works too.
    /// </summary>
    public static class DebugOptions
    {
        public const string ForceTraitArg = "-bbForceTrait";
        /// <summary>-bbSeed &lt;number&gt;: every NEW run starts with this seed (a saved run keeps its own on CONTINUE).</summary>
        public const string ForceSeedArg = "-bbSeed";
        public const string TraceArg = "-bbTrace";
        /// <summary>-bbLangVi: hidden switch that draws the string table in Vietnamese (BB-FONT1). Off by default; EN stays the default.</summary>
        public const string LangViArg = "-bbLangVi";

        static bool parsed;
        static string forceTrait;
        static uint? forceSeed;
        static bool trace;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            parsed = false;
            forceTrait = null;
            forceSeed = null;
            trace = false;
            audioLogOn = null;
            forceDraw = null;
            forceBossParsed = false;
            forceBoss = null;
        }

        /// <summary>Trait id forced into the first offer, or null (normal play).</summary>
        public static string ForceTrait
        {
            get
            {
                if (!parsed)
                {
                    parsed = true;
                    var args = Environment.GetCommandLineArgs();
                    forceTrait = ParseForceTrait(args);
                    forceSeed = ParseSeed(args);
                    trace = ParseTrace(args);
                }
                return forceTrait;
            }
        }

        /// <summary>Seed forced for new runs, or null (normal play: random seed).</summary>
        public static uint? ForceSeed
        {
            get
            {
                _ = ForceTrait;   // parses the command line once (both switches)
                return forceSeed;
            }
        }

        public static bool Trace { get { _ = ForceTrait; return trace; } }

        /// <summary>-bbAudioLog: log every wall/obstacle cue (AudioLog). Off by default.</summary>
        public const string AudioLogArg = "-bbAudioLog";
        /// <summary>-bbForceDraw: Versus only, the first round to resolve ends the match as a draw (MatchFlow.ForceDraw). Off by default; Run mode ignores it.</summary>
        public const string ForceDrawArg = "-bbForceDraw";

        static bool? audioLogOn, forceDraw;   // parsed once (read on every hazard cue)

        public static bool AudioLogOn => audioLogOn ?? (audioLogOn = HasArg(Environment.GetCommandLineArgs(), AudioLogArg)).Value;
        public static bool ForceDraw => forceDraw ?? (forceDraw = HasArg(Environment.GetCommandLineArgs(), ForceDrawArg)).Value;
        public static bool ParseAudioLog(string[] args) => HasArg(args, AudioLogArg);
        public static bool ParseForceDraw(string[] args) => HasArg(args, ForceDrawArg);

        /// <summary>
        /// -bbForceBoss &lt;shellking|magnet|mimic|warden&gt; (BB-04): Run mode only, off by default. The boss takes its own slot (fight 4
        /// or 8) and, when the next fight is not already a boss fight, that fight too (RunState.ApplyForcedBoss). Never written into
        /// the "last boss" history. Versus ignores it. Case, dashes, underscores and spaces are ignored ("Shell-King" works).
        /// </summary>
        public const string ForceBossArg = "-bbForceBoss";

        static bool forceBossParsed;
        static string forceBoss;

        /// <summary>Boss id forced in Run mode, or null (normal play).</summary>
        public static string ForceBoss
        {
            get
            {
                if (forceBossParsed) return forceBoss;
                forceBossParsed = true;
                forceBoss = ParseForceBoss(Environment.GetCommandLineArgs());
                return forceBoss;
            }
        }

        /// <summary>The boss id after -bbForceBoss in <paramref name="args"/>, or null (absent or unknown, with a warning).</summary>
        public static string ParseForceBoss(string[] args)
        {
            if (args == null) return null;
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], ForceBossArg, StringComparison.OrdinalIgnoreCase)) continue;
                var id = i + 1 < args.Length ? ResolveBossId(args[i + 1]) : null;
                if (id == null) Debug.LogWarning($"[BallBattle] {ForceBossArg}: expected shellking, magnet, mimic or warden; ignored");
                else Debug.Log($"[BallBattle] {ForceBossArg}: Run boss forced to {id}");
                return id;
            }
            return null;
        }

        /// <summary>Boss id matching <paramref name="name"/> ignoring case, dashes, underscores and spaces; null if none.</summary>
        public static string ResolveBossId(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = Normalize(name).Replace(" ", "");
            foreach (var id in BallBattle.Sim.Boss.BossIds.All)
                if (id == key) return id;
            return null;
        }

        static bool HasArg(string[] args, string name)
        {
            if (args == null) return false;
            foreach (var arg in args)
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>True when the command line asks for Vietnamese (<see cref="LangViArg"/>).</summary>
        public static bool LangVi => ParseLangVi(Environment.GetCommandLineArgs());

        public static bool ParseLangVi(string[] args)
        {
            if (args == null) return false;
            foreach (var arg in args)
                if (string.Equals(arg, LangViArg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public static bool ParseTrace(string[] args)
        {
            if (args == null) return false;
            foreach (var arg in args)
                if (string.Equals(arg, TraceArg, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>The number after -bbSeed in <paramref name="args"/> (0 .. 4294967295), or null (absent or not a number, with a warning).</summary>
        public static uint? ParseSeed(string[] args)
        {
            if (args == null) return null;
            for (var i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], ForceSeedArg, StringComparison.OrdinalIgnoreCase)) continue;
                if (i + 1 < args.Length && uint.TryParse(args[i + 1].Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var seed))
                    return seed;
                Debug.LogWarning("[BallBattle] " + ForceSeedArg + ": expected a number from 0 to 4294967295, ignored");
                return null;
            }
            return null;
        }
        /// <summary>The registry id named after -bbForceTrait in <paramref name="args"/>, or null (absent or unknown, with a warning).</summary>
        public static string ParseForceTrait(string[] args)
        {
            if (args == null) return null;
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (!string.Equals(args[i], ForceTraitArg, StringComparison.OrdinalIgnoreCase)) continue;
                var id = ResolveTraitId(args[i + 1]);
                if (id == null) Debug.LogWarning($"[BallBattle] {ForceTraitArg}: unknown trait '{args[i + 1]}', ignored");
                else Debug.Log($"[BallBattle] {ForceTraitArg}: the offer before fight 1 will contain {id}");
                return id;
            }
            return null;
        }

        /// <summary>TraitRegistry id matching <paramref name="name"/> ignoring case, dashes and underscores; null if none.</summary>
        public static string ResolveTraitId(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            var key = Normalize(name);
            foreach (var e in TraitRegistry.All)
                if (Normalize(e.Id) == key) return e.Id;
            return null;
        }

        static string Normalize(string s) => s.Replace("-", "").Replace("_", "").Trim().ToLowerInvariant();

        /// <summary>
        /// Put trait <paramref name="traitId"/> (LV1) into the current offer if this is the offer before fight 1 and it
        /// is not there yet: replaces the last card. Changes only the offer list of the run (view-side debug), never
        /// the sim. Returns true when the offer was changed.
        /// </summary>
        public static bool ApplyForcedTrait(RunState run, string traitId)
        {
            if (run == null || traitId == null || run.Over || run.FightLocked || run.FightIndex != 0 || run.OfferCount == 0) return false;
            if (!TraitRegistry.Exists(traitId)) return false;
            for (var i = 0; i < run.OfferCount; i++)
            {
                var c = run.OfferCard(i);
                if (c.Kind == CardKind.Trait && c.Id == traitId) return false;
            }
            var slot = run.OfferCount - 1;
            if (run.IsPicked(slot)) return false;   // never swap a card already taken
            run.OfferCards[slot] = OfferSlot.From(new Card(CardKind.Trait, traitId, 1));
            return true;
        }
    }
}
