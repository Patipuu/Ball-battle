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