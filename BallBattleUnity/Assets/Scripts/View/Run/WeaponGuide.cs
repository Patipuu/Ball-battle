using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// UI-UX1: what each ball does, in plain words, for the weapon pick and the Codex: how it fights/grows, what it is good at, what beats it.
    /// From docs/design/weapon-bible.md and the BB-BAL2/BAL3 Versus/Run numbers. View text only (EN; VI deferred).
    /// </summary>
    public static class WeaponGuide
    {
        /// <summary>Readable on the dark panel at 1x (HpLost was too dark for text).</summary>
        public static readonly Color32 GoodColor = new Color32(120, 220, 120, 255);
        public static readonly Color32 WeakColor = new Color32(240, 120, 110, 255);
        public sealed class Entry
        {
            public readonly string Grows, Good, Weak;
            public Entry(string grows, string good, string weak) { Grows = grows; Good = good; Weak = weak; }
        }

        static readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>
        {
            { "blade", new Entry("BLADE DMG +1 WITH EVERY HIT", "STEADY DAMAGE. EASIEST TO PLAY", "PARRIES STOP IT GROWING") },
            { "fang", new Entry("SPINS FASTER WITH EVERY HIT", "FAST COMBOS ONCE IT SPINS", "SLOW START. WRECKER CRUSHES IT") },
            { "pike", new Entry("LINE GROWS LONGER EVERY HIT", "HUGE REACH LATE IN THE FIGHT", "LONG LINE IS EASY TO PARRY") },
            { "brawler", new Entry("NO WEAPON. FASTER BODY = MORE DMG", "WALL BOUNCES BUILD SPEED", "MIRROR REFLECTS BODY HITS") },
            { "volley", new Entry("+1 SHOT PER VOLLEY PER MELEE HIT", "DAMAGE FROM RANGE. SHOTS BOUNCE", "TIGHT ARENAS. PARRY TURNS SHOTS") },
            { "venom", new Entry("HITS POISON. MORE STACKS PER HIT", "LONG FIGHTS. BEATS MIRROR", "WEAK START. BURST BEATS IT") },
            { "aegis", new Entry("REFLECTS PARRIES AND BODY HITS", "PUNISHES WRECKER AND BIG HITS", "POISON (SPORE). TIGHT ARENAS") },
            { "rig", new Entry("EACH HIT BUILDS A TURRET (MAX 6)", "TURRETS FILL THE ARENA LATE", "SLOW START. WALLS EAT TURRETS") },
        };

        /// <summary>The entry for <paramref name="weaponId"/>, or null for an unknown id.</summary>
        public static Entry For(string weaponId) => weaponId != null && entries.TryGetValue(weaponId, out var e) ? e : null;
    }
}
