namespace BallBattle.Sim.Weapons
{
    /// <summary>Spear-like: slow spin; every hit adds length and damage. Long reach late, but a long shaft is easy to parry.</summary>
    public sealed class PikeRule : WeaponRule
    {
        public const string WeaponId = "pike";

        public float CurrentDamage { get; private set; } = WeaponTuning.PikeStartDamage;

        public PikeRule()
        {
            BladeLength = WeaponTuning.PikeStartLength;
            SpinDegPerTick = WeaponTuning.PikeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "LEN";
        public override float StatValue => BladeLength;
        public override string StatText => BladeLength.ToString("0", System.Globalization.CultureInfo.InvariantCulture);

        public override float Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx)
        {
            CurrentDamage += WeaponTuning.PikeGrowthPerHit;
            BladeLength += WeaponTuning.PikeGrowthPerHit;
            if (BladeLength > WeaponTuning.PikeMaxLength) BladeLength = WeaponTuning.PikeMaxLength;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, CurrentDamage);
    }
}
