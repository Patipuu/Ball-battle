using System;
using System.Linq;
using System.Reflection;

namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Every weapon number in one place, so balancing (Phase 7) edits this file only.
    /// Lengths/speeds in native pixels and ticks. Change a value that alters outcomes → bump SimVersion.Rules.
    /// Static fields (not const) so balance tools can sweep values without rebuilding. Only change them between
    /// matches, never while one is running; gameplay code must treat them as read-only.
    /// </summary>
    public static class WeaponTuning
    {
        // Blade: steady damage growth.
        public static Fix BladeLength = 24;
        public static Fix BladeSpin = 6;
        public static Fix BladeStartDamage = 1;
        public static Fix BladeDamagePerHit = 1;

        // Fang: short, spins faster on every hit (gain shrinks), many cheap hits, parries a lot.
        public static Fix FangLength = 16;
        public static Fix FangStartSpin = 8;
        public static Fix FangStartSpinGain = 3;
        public static Fix FangSpinGainDecay = Fix.Ratio(23, 25);   // BB-6c: 0.98 -> 0.94; BB-6d: 0.94 -> 0.92 (tight MANTIS chains)
        public static Fix FangMaxSpin = 40;
        public static Fix FangStartDamage = 1;
        public static Fix FangDamagePerHit = Fix.Ratio(2, 5);
        public static int FangHitCooldownTicks = 3;
        public static Fix FangKnockbackScale = Fix.Ratio(3, 10);

        // Pike: slow, grows longer and harder.
        public static Fix PikeStartLength = 26;
        public static Fix PikeSpin = 4;
        public static Fix PikeStartDamage = 1;
        public static Fix PikeGrowthPerHit = Fix.Ratio(3, 4);
        public static Fix PikeMaxLength = 90;

        // Brawler: no blade; body damage scales with its own speed; speed cap grows on hits and wall bounces.
        public static Fix BrawlerStartDamagePerSpeed = Fix.Ratio(1, 10);
        public static Fix BrawlerDamagePerSpeedPerHit = Fix.Ratio(1, 100);
        public static Fix BrawlerStartSpeedBonus = -3;
        public static Fix BrawlerSpeedBonusPerHit = Fix.Ratio(1, 2);
        public static Fix BrawlerSpeedBonusPerWall = Fix.Ratio(3, 20);   // BB-6c: 0.2 -> 0.15
        public static Fix BrawlerMaxSpeedBonus = 4;
        public static Fix BrawlerWallSpeedBoost = Fix.Ratio(19, 250);   // BB-6e: 0.08 -> 0.076 (-5%; WRECKER vs MANTIS on bumpers)

        // Volley: stubby blade; every interval a fan of arrows along the blade; each melee hit adds an arrow.
        public static Fix VolleyLength = 12;
        public static Fix VolleySpin = Fix.Ratio(35, 8);   // BB-6c: 3.5 -> 4.375
        public static Fix VolleyMeleeDamage = Fix.Ratio(5, 4);   // BB-6c: 1 -> 1.25
        public static Fix VolleyArrowDamagePerHit = Fix.Ratio(1, 10);
        public static int VolleyIntervalTicks = 90;
        public static int VolleyMaxArrows = 8;
        public static Fix VolleySpreadDeg = 8;
        public static Fix VolleyArrowSpeed = 5;
        public static Fix VolleyArrowRadius = Fix.Ratio(3, 2);
        public static Fix VolleyArrowDamage = Fix.Ratio(9, 10);
        public static int VolleyArrowLifetimeTicks = 180;

        // Venom: wide slow blade; the poison stack allowance grows per hit.
        public static Fix VenomLength = 22;
        public static Fix VenomThickness = 6;
        public static Fix VenomSpin = 4;
        public static Fix VenomMeleeDamage = Fix.Ratio(3, 2);
        public static Fix VenomDamagePerHit = Fix.Ratio(3, 16);   // BB-6c: 0.25 -> 0.1875
        public static Fix VenomDps = 1;
        public static int VenomSeconds = 4;

        // Aegis: broad shield blade; reflects the parried blow; widens on body hits.
        public static Fix AegisLength = 22;
        public static Fix AegisThickness = 7;
        public static Fix AegisSpin = 3;
        public static Fix AegisChipDamage = Fix.Ratio(7, 10);
        public static Fix AegisDamagePerWidth = Fix.Ratio(1, 4);
        public static Fix AegisBodyReflectPct = Fix.Ratio(9, 16);   // BB-6c: 0.5 -> 0.5625
        public static Fix AegisWidthPerHit = Fix.Ratio(5, 16);   // BB-6c: 0.25 -> 0.3125
        public static Fix AegisMaxThickness = 14;
        public static Fix AegisReflectPct = Fix.Ratio(9, 20);   // BB-6c: 0.4 -> 0.45
        public static Fix AegisShotAbsorbPct = Fix.Ratio(3, 20);   // BB-6c: 0.2 -> 0.15
        // BB-6d: MIRROR grows on combat, not time. Guard points per combat event: a parry against a blade or a blade hit taken
        // (AegisGuardMelee), a body blow taken (AegisGuardBody), a shot taken (AegisGuardShot; shots stay its weakness).
        // Replaces AegisRegenPerSecond (heal per second) and AegisRageStartTicks / AegisRageRampTicks (rage clock).
        // BB-6e: guard points are Fix, so a shot can be worth a fraction (structure unchanged).
        public static Fix AegisGuardMelee = 3;
        public static Fix AegisGuardBody = 2;
        public static Fix AegisGuardShot = Fix.Ratio(5, 4);   // BB-6e: 1 -> 1.25 (SPARKLER vs MIRROR; 1.375 and 1.5 push MIRROR vs HIVE on bumpers over 80)
        /// <summary>A parry between two Aegis (mirror match only): rage builds fast and no guard from an Aegis mends, so the mirror ends
        /// (weapon gate: median &lt;= 90 s).</summary>
        public static Fix AegisGuardMirror = 9;
        /// <summary>HP healed per guard point (was a time regen).</summary>
        public static Fix AegisMendPerGuard = Fix.Ratio(3, 25);
        /// <summary>After this many guard points Aegis hits harder and harder (ends Aegis mirrors); +100% per AegisRageRampGuards more.</summary>
        public static Fix AegisRageStartGuards = 18;
        public static Fix AegisRageRampGuards = 27;
        /// <summary>Rage counts at most this many guard points past the start (+AegisRageMaxGuards/AegisRageRampGuards, about +300%).</summary>
        public static Fix AegisRageMaxGuards = 80;

        // Rig: every melee hit drops a turret (ghost emplacement); turrets shoot together.
        public static Fix RigLength = 18;
        public static Fix RigSpin = 5;
        public static Fix RigMeleeDamage = Fix.Ratio(3, 2);
        public static Fix RigDamagePerHit = Fix.Ratio(1, 5);
        public const int RigMaxTurrets = 6;
        public static Fix RigTurretRadius = 3;
        public static int RigFireIntervalTicks = 68;
        public static Fix RigShotSpeed = Fix.Ratio(7, 2);
        public static Fix RigShotRadius = Fix.Ratio(3, 2);
        public static Fix RigShotDamage = Fix.Ratio(11, 10);
        public static int RigShotLifetimeTicks = 150;

        /// <summary>
        /// Hash of every tuning field (fixed name order). MatchSim mixes it into its state hash, so two matches
        /// with the same seed but different tuning never report the same hash.
        /// </summary>
        public static ulong Fingerprint()
        {
            var h = SimHash.Seed;
            var fields = typeof(WeaponTuning).GetFields(BindingFlags.Public | BindingFlags.Static)
                                             .OrderBy(f => f.Name, StringComparer.Ordinal);
            foreach (var f in fields)
            {
                var v = f.GetValue(null);
                if (v is Fix fx) h = SimHash.Mix(h, fx);
                else if (v is int i) h = SimHash.Mix(h, i);
                else throw new InvalidOperationException($"WeaponTuning.{f.Name}: unsupported type {f.FieldType}");
            }
            return h;
        }
    }
}
