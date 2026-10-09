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
        public static float BladeLength = 24f;
        public static float BladeSpin = 6f;
        public static float BladeStartDamage = 1f;
        public static float BladeDamagePerHit = 1f;

        // Fang: short, spins faster on every hit (gain shrinks), many cheap hits, parries a lot.
        public static float FangLength = 16f;
        public static float FangStartSpin = 8f;
        public static float FangStartSpinGain = 3f;
        public static float FangSpinGainDecay = 0.98f;
        public static float FangMaxSpin = 40f;
        public static float FangStartDamage = 1f;
        public static float FangDamagePerHit = 0.4f;
        public static int FangHitCooldownTicks = 3;
        public static float FangKnockbackScale = 0.3f;

        // Pike: slow, grows longer and harder.
        public static float PikeStartLength = 26f;
        public static float PikeSpin = 4f;
        public static float PikeStartDamage = 1f;
        public static float PikeGrowthPerHit = 0.75f;
        public static float PikeMaxLength = 90f;

        // Brawler: no blade; body damage scales with its own speed; speed cap grows on hits and wall bounces.
        public static float BrawlerStartDamagePerSpeed = 0.1f;
        public static float BrawlerDamagePerSpeedPerHit = 0.01f;
        public static float BrawlerStartSpeedBonus = -3f;
        public static float BrawlerSpeedBonusPerHit = 0.5f;
        public static float BrawlerSpeedBonusPerWall = 0.2f;
        public static float BrawlerMaxSpeedBonus = 4f;
        public static float BrawlerWallSpeedBoost = 0.08f;

        // Volley: stubby blade; every interval a fan of arrows along the blade; each melee hit adds an arrow.
        public static float VolleyLength = 12f;
        public static float VolleySpin = 3.5f;
        public static float VolleyMeleeDamage = 1f;
        public static float VolleyArrowDamagePerHit = 0.1f;
        public static int VolleyIntervalTicks = 90;
        public static int VolleyMaxArrows = 8;
        public static float VolleySpreadDeg = 8f;
        public static float VolleyArrowSpeed = 5f;
        public static float VolleyArrowRadius = 1.5f;
        public static float VolleyArrowDamage = 0.9f;
        public static int VolleyArrowLifetimeTicks = 180;

        // Venom: wide slow blade; the poison stack allowance grows per hit.
        public static float VenomLength = 22f;
        public static float VenomThickness = 6f;
        public static float VenomSpin = 4f;
        public static float VenomMeleeDamage = 1.5f;
        public static float VenomDamagePerHit = 0.25f;
        public static float VenomDps = 1f;
        public static int VenomSeconds = 4;

        // Aegis: broad shield blade; reflects the parried blow; widens on body hits.
        public static float AegisLength = 22f;
        public static float AegisThickness = 7f;
        public static float AegisSpin = 3f;
        public static float AegisChipDamage = 0.7f;
        public static float AegisDamagePerWidth = 0.25f;
        public static float AegisBodyReflectPct = 0.5f;
        public static float AegisWidthPerHit = 0.25f;
        public static float AegisMaxThickness = 14f;
        public static float AegisReflectPct = 0.4f;
        public static float AegisShotAbsorbPct = 0.2f;
        public static float AegisRegenPerSecond = 0.25f;
        /// <summary>After this many active ticks Aegis hits harder and harder (ends Aegis mirrors); +100% per ramp.</summary>
        public static int AegisRageStartTicks = 900;
        public static int AegisRageRampTicks = 600;

        // Rig: every melee hit drops a turret (ghost emplacement); turrets shoot together.
        public static float RigLength = 18f;
        public static float RigSpin = 5f;
        public static float RigMeleeDamage = 1.5f;
        public static float RigDamagePerHit = 0.2f;
        public const int RigMaxTurrets = 6;
        public static float RigTurretRadius = 3f;
        public static int RigFireIntervalTicks = 68;
        public static float RigShotSpeed = 3.5f;
        public static float RigShotRadius = 1.5f;
        public static float RigShotDamage = 1.1f;
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
                if (v is float fl) h = SimHash.Mix(h, fl);
                else if (v is int i) h = SimHash.Mix(h, i);
                else throw new InvalidOperationException($"WeaponTuning.{f.Name}: unsupported type {f.FieldType}");
            }
            return h;
        }
    }
}