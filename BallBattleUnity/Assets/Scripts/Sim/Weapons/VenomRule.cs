using System;

namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Poisoner: a wide, slow blade. Hits poison the target while it carries fewer stacks than Venom allows;
    /// every hit raises that allowance by one (max 8). Beats healing and Second Wind; loses to fast burst damage.
    /// </summary>
    public sealed class VenomRule : WeaponRule
    {
        public const string WeaponId = "venom";

        public int MaxStacks { get; private set; } = 1;
        public float CurrentDamage { get; private set; } = WeaponTuning.VenomMeleeDamage;

        public VenomRule()
        {
            BladeLength = WeaponTuning.VenomLength;
            BladeThickness = WeaponTuning.VenomThickness;
            SpinDegPerTick = WeaponTuning.VenomSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "VENOM";
        public override float StatValue => MaxStacks;

        public override float Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx)
        {
            MaxStacks = Math.Min(StatusEffects.MaxPoisonStacks, MaxStacks + 1);
            CurrentDamage += WeaponTuning.VenomDamagePerHit;
        }

        public override void OnHitDealt(BallState target, float damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return;
            if (target.Status.PoisonStackCount >= MaxStacks) return;
            Sim.ApplyPoison(target, Self, WeaponTuning.VenomDps, WeaponTuning.VenomSeconds * MatchConfig.TicksPerSecond);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, MaxStacks), CurrentDamage);
    }
}