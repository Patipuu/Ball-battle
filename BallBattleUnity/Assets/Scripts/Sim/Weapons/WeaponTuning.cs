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
        public static Fix FangSpinGainDecay = Fix.Ratio(47, 50);   // BB-6c: 0.98 -> 0.94
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
        public static Fix BrawlerWallSpeedBoost = Fix.Ratio(2, 25);

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
        public static Fix AegisRegenPerSecond = Fix.Ratio(3, 16);   // BB-6c: 0.25 -> 0.1875
        /// <summary>After this many active ticks Aegis hits harder and harder (ends Aegis mirrors); +100% per ramp.</summary>
        public static int AegisRageStartTicks = 675;   // BB-6c: 900 -> 675
        public static int AegisRageRampTicks = 750;   // BB-6c: 600 -> 750

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
