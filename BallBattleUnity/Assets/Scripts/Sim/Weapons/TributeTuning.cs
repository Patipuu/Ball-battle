using System;
using System.Linq;
using System.Reflection;

namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// BB-14: tuning of the two tribute balls, BGLGXLQ (chorus, the ring of five colours) and the snake (serpent, prey and tail).
    /// Kept apart from WeaponTuning so the fingerprint of matches without them stays as it was: MatchSim mixes
    /// <see cref="Fingerprint"/> only when one of them is in the match.
    /// </summary>
    public static class TributeTuning
    {
        // ---- chorus: ring of five colours (PINK, GREEN, BLUE, YELLOW, RED) ----
        public static Fix ChorusLength = 18;
        public static Fix ChorusSpin = 5;
        public static Fix ChorusStartDamage = Fix.Ratio(3, 2);   // calibration: 1.2 -> 1.5
        /// <summary>Permanent melee damage per completed ring.</summary>
        public static Fix ChorusDamagePerRing = Fix.One;   // calibration: 0.15 -> 1.25 (a ring = 5 hits; EMBER grows +1 per hit); BB-18 orbs: -> 1
        /// <summary>BB-18 orbs: gap between the ball edge and an orb centre, orb radius, turn per tick (degrees).</summary>
        public static Fix ChorusOrbGap = 9;
        public static Fix ChorusOrbRadius = 4;
        public static Fix ChorusOrbSpin = 3;
        /// <summary>BB-18: damage of an unlit orb touching an enemy (it lights that colour).</summary>
        public static Fix ChorusOrbDamage = Fix.Ratio(1, 4);
        /// <summary>BB-18: ticks between two lights (one colour per touch), and the dark pause after UNITY.</summary>
        public static int ChorusLightGapTicks = 60;
        public static int ChorusDarkTicks = 90;
        /// <summary>PINK: heal per hit while lit.</summary>
        public static Fix ChorusPinkHeal = Fix.Ratio(3, 5);
        /// <summary>GREEN: top speed bonus while lit.</summary>
        public static Fix ChorusGreenSpeed = Fix.Ratio(3, 2);
        /// <summary>BLUE: shield charges given when it lights (one guard per ring).</summary>
        public static int ChorusBlueShield = 1;
        /// <summary>YELLOW: melee damage bonus while lit.</summary>
        public static Fix ChorusYellowDamage = Fix.Ratio(2, 5);
        /// <summary>RED: burn on the enemy whose touch lights it (damage per second, ticks).</summary>
        public static Fix ChorusRedDps = Fix.Ratio(3, 5);
        public static int ChorusRedTicks = 120;
        /// <summary>UNITY: damage = base + per ring completed before this one; reaches balls within the radius.</summary>
        public static Fix ChorusUnityBase = 4;   // calibration: 3 -> 4
        public static Fix ChorusUnityPerRing = 1;   // calibration: 1.5 -> 2; BB-18 orbs (about 5 rings a minute): -> 1
        public static Fix ChorusUnityRadius = 110;
        /// <summary>Skill HYMN: unlit colours lit at once.</summary>
        public static int ChorusHymnColors = 2;
        /// <summary>Branch A CHORUS: UNITY heals this share of max HP (skill LV2 / LV3).</summary>
        public static Fix ChorusHealL2 = Fix.Ratio(3, 200), ChorusHealL3 = Fix.Ratio(9, 400);   // Versus: BB-14 values
        /// <summary>Run-only CHORUS heal per UNITY (LV2 / LV3).</summary>
        public static Fix ChorusRunHealL2 = Fix.Ratio(1, 100), ChorusRunHealL3 = Fix.Ratio(3, 200);
        /// <summary>Branch B RADIANCE: UNITY damage multiplier (LV2 / LV3) and extra radius.</summary>
        public static Fix ChorusRadianceL2 = Fix.Ratio(8, 5), ChorusRadianceL3 = Fix.Ratio(19, 10);   // Run tuning 1: 1.2 / 1.35 -> 1.4 / 1.6; 2: -> 1.6 / 1.9
        public static Fix ChorusRadianceRadius = 30;   // Run tuning 1: 20 -> 30
        /// <summary>FINALE A EVERLIGHT: colours that stay lit after UNITY.</summary>
        public static int ChorusEverlightKeep = 1;   // Run tuning 1: PINK and GREEN -> PINK only
        /// <summary>FINALE B DOUBLE RAINBOW: each of the two bursts deals this share of UNITY.</summary>
        public static Fix ChorusDoubleRainbow = Fix.Ratio(3, 4);   // Run tuning 2: 60% -> 75%
        /// <summary>Run baseline (any ball with a tree): extra melee damage.</summary>
        public static Fix ChorusRunDamage = Fix.Ratio(3, 10);
        // BB-18 Versus profile: lower outgoing damage; orbiting orbs cushion body, projectile and ability impacts.
        public static Fix ChorusVersusMeleeScale = Fix.Ratio(7, 10);
        public static Fix ChorusVersusAbilityScale = Fix.Ratio(1, 2);
        public static Fix ChorusVersusBodyDamageScale = Fix.Ratio(19, 50);
        public static Fix ChorusVersusProjectileDamageScale = Fix.Ratio(13, 20);
        public static Fix ChorusVersusAbilityDamageScale = Fix.Ratio(1, 2);

        // ---- serpent: prey, hunt, tail ----
        public static Fix SerpentLength = 14;
        public static Fix SerpentSpin = 6;
        public static Fix SerpentBiteBase = 1;
        public static Fix SerpentBitePerSegment = Fix.Ratio(3, 10);   // calibration: 0.25 -> 0.3
        /// <summary>Permanent bite damage per prey eaten (growth that a cut cannot take away).</summary>
        public static Fix SerpentBitePerMeal = Fix.Ratio(3, 50);   // calibration: 0.05 -> 0.06
        /// <summary>After a cut the tail cannot be cut again for this long.</summary>
        public static int SerpentCutImmuneTicks = 60;
        /// <summary>Hunt: most degrees per tick the velocity turns toward the nearest prey.</summary>
        public static Fix SerpentHuntDeg = Fix.Ratio(6, 5);
        public static Fix SerpentStrikeDeg = 4;
        public static int SerpentMaxSegments = 8;
        public static Fix SerpentSegmentRadius = 6;
        /// <summary>History ticks between two tail segments.</summary>
        public static int SerpentSegmentSpacing = 8;
        public static Fix SerpentTailDamage = Fix.Ratio(7, 10);   // calibration: 0.6 -> 0.7
        public static int SerpentTailCooldownTicks = 30;
        public static Fix SerpentEatHeal = 1;
        public static Fix SerpentPreyRadius = 5;
        public static int SerpentFirstPreyTicks = 90;
        public static int SerpentPreyEveryTicks = 180;
        public static int SerpentPreyOnField = 3;
        public static int SerpentPreyMax = 5;
        /// <summary>Prey keep this far from walls and balls when they appear.</summary>
        public static Fix SerpentPreyMargin = 40;
        /// <summary>Skill STRIKE: next bite multiplier.</summary>
        public static Fix SerpentStrikeBite = Fix.Ratio(3, 2);
        /// <summary>Branch A CONSTRICT: from this many segments the tail slows the enemy it touches and hits harder.</summary>
        public static int SerpentConstrictSegments = 3;   // Run tuning 1: 4 -> 3
        public static Fix SerpentConstrictSlow = Fix.Ratio(3, 4);   // Run tuning 1: x0.8 -> x0.75
        public static Fix SerpentConstrictTail = Fix.Ratio(13, 10);   // Run tuning 1: new, tail damage x1.3
        /// <summary>FINALE B SHED SKIN: keeps 1/this of the lost segments.</summary>
        public static int SerpentShedDivisor = 4;   // Run tuning 1: half -> a third; 2: -> a quarter
        /// <summary>Run baseline (any ball with a tree): extra bite damage.</summary>
        public static Fix SerpentRunBite = Fix.Ratio(1, 5);

        public static ulong Fingerprint()
        {
            var h = SimHash.Seed;
            var fields = typeof(TributeTuning).GetFields(BindingFlags.Public | BindingFlags.Static)
                                              .OrderBy(f => f.Name, StringComparer.Ordinal);
            foreach (var f in fields)
            {
                var v = f.GetValue(null);
                if (v is Fix fx) h = SimHash.Mix(h, fx);
                else if (v is int i) h = SimHash.Mix(h, i);
                else throw new InvalidOperationException($"TributeTuning.{f.Name}: unsupported type {f.FieldType}");
            }
            return h;
        }
    }
}
