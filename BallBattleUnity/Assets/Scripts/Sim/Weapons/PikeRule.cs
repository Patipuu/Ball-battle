namespace BallBattle.Sim.Weapons
{
    /// <summary>Spear-like: slow spin; every hit adds length and damage. Long reach late, but a long shaft is easy to parry.</summary>
    public sealed class PikeRule : WeaponRule
    {
        public const string WeaponId = "pike";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.PikeStartDamage;

        public PikeRule()
        {
            BladeLength = WeaponTuning.PikeStartLength;
            SpinDegPerTick = WeaponTuning.PikeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "LEN";
        public override Fix StatValue => BladeLength;
        public override string StatText => FixMath.Format(BladeLength, 0);

        public override Fix Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx)
        {
            CurrentDamage += WeaponTuning.PikeGrowthPerHit;
            BladeLength += WeaponTuning.PikeGrowthPerHit;
            if (BladeLength > WeaponTuning.PikeMaxLength) BladeLength = WeaponTuning.PikeMaxLength;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, CurrentDamage);
    }
}
