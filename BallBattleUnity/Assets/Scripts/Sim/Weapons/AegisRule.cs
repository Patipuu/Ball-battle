using System;

namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Counter-fighter: a broad shield blade that barely scratches. When it parries, the blow it stopped comes back at
    /// the attacker, and so does part of a body blow (Reflect damage); it absorbs most of a shot's damage and rages late in a fight. Each time it bumps a body the shield widens a little. Loses to poison and shots.
    /// </summary>
    public sealed class AegisRule : WeaponRule
    {
        public const string WeaponId = "aegis";

        int ticks;

        public AegisRule()
        {
            BladeLength = WeaponTuning.AegisLength;
            BladeThickness = WeaponTuning.AegisThickness;
            SpinDegPerTick = WeaponTuning.AegisSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "WIDTH";
        public override float StatValue => BladeThickness;

        public override float Damage(in HitContext ctx)
        {
            var d = WeaponTuning.AegisChipDamage + (BladeThickness - WeaponTuning.AegisThickness) * WeaponTuning.AegisDamagePerWidth;
            var rage = ticks - WeaponTuning.AegisRageStartTicks;
            return rage > 0 ? d * (1f + rage / (float)WeaponTuning.AegisRageRampTicks) : d;
        }

        /// <summary>Slow regen (answers poison and chip damage) and the late-fight rage clock.</summary>
        public override void OnTick()
        {
            ticks++;
            if (ticks % MatchConfig.TicksPerSecond == 0) Sim.Heal(Self, WeaponTuning.AegisRegenPerSecond);
        }

        public override void OnHit(in HitContext ctx) => BladeThickness = Math.Min(WeaponTuning.AegisMaxThickness, BladeThickness + WeaponTuning.AegisWidthPerHit);

        /// <summary>A body blow (bladeless attacker, e.g. Brawler) cannot be parried, so part of it is sent back.</summary>
        public override void OnHitTaken(BallState attacker, float damage, DamageKind kind)
        {
            if (Self.Hp <= 0f) return;
            if (kind == DamageKind.Projectile) { Sim.Heal(Self, damage * WeaponTuning.AegisShotAbsorbPct); return; }
            if (kind != DamageKind.Weapon || attacker.Weapon.HasBlade) return;
            Sim.DealDamage(Self, attacker, damage * WeaponTuning.AegisBodyReflectPct, DamageKind.Reflect);
        }

        public override void OnParry(BallState other)
        {
            if (Self.Hp <= 0f || !other.Weapon.HasBlade || other.Weapon is AegisRule) return; // two Aegis would only trade reflects until both fall
            var blow = other.Weapon.Damage(new HitContext(other.Vel.Length, 0f));
            Sim.DealDamage(Self, other, blow * WeaponTuning.AegisReflectPct, DamageKind.Reflect);
        }

        // The width (BladeThickness) is hashed by the base class; the clock is hashed here.
        protected override ulong HashState(ulong h) => SimHash.Mix(h, ticks);
    }
}