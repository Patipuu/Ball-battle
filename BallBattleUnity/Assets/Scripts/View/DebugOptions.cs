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

        static bool parsed;
        static string forceTrait;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            parsed = false;
            forceTrait = null;
        }

        /// <summary>Trait id forced into the first offer, or null (normal play).</summary>
        public static string ForceTrait
        {
            get
            {
                if (!parsed)
                {
                    parsed = true;
                    forceTrait = ParseForceTrait(Environment.GetCommandLineArgs());
                }
                return forceTrait;
            }
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
