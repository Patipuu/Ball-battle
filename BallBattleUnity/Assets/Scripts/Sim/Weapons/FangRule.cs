namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Dagger-like: small damage that creeps up a little per hit; every hit spins it faster (the gain shrinks 2% per hit) and its
    /// own hit cooldown is short, so it lands many hits and parries slow blades. Weak against bodies (no parry possible).
    /// </summary>
    public sealed class FangRule : WeaponRule
    {
        public const string WeaponId = "fang";

        public float SpinGain { get; private set; } = WeaponTuning.FangStartSpinGain;
        public float CurrentDamage { get; private set; } = WeaponTuning.FangStartDamage;

        public FangRule()
        {
            BladeLength = WeaponTuning.FangLength;
            SpinDegPerTick = WeaponTuning.FangStartSpin;
            HitCooldownTicks = WeaponTuning.FangHitCooldownTicks;
            KnockbackScale = WeaponTuning.FangKnockbackScale;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "SPIN";
        public override float StatValue => SpinDegPerTick;

        public override float Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx)
        {
            CurrentDamage += WeaponTuning.FangDamagePerHit;
            SpinDegPerTick += SpinGain;
            if (SpinDegPerTick > WeaponTuning.FangMaxSpin) SpinDegPerTick = WeaponTuning.FangMaxSpin;
            SpinGain *= WeaponTuning.FangSpinGainDecay;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, SpinGain), CurrentDamage);
    }
}
