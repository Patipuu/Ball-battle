namespace BallBattle.Sim.Weapons
{
    /// <summary>Spear-like: slow spin; every hit adds length and damage. Long reach late, but a long shaft is easy to parry.</summary>
    public sealed class PikeRule : WeaponRule
    {
        public const string WeaponId = "pike";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.PikeStartDamage;
        Fix baseLength = WeaponTuning.PikeStartLength;
        char keystone;

        public PikeRule()
        {
            BladeLength = WeaponTuning.PikeStartLength;
            SpinDegPerTick = WeaponTuning.PikeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "LEN";
        public override Fix StatValue => BladeLength;
        public override string StatText => FixMath.Format(BladeLength, 0);

        public override Fix Damage(in HitContext ctx) => CurrentDamage *
            (keystone == 'A' ? Fix.Ratio(4, 5) : keystone == 'B' ? Fix.Ratio(5, 4) : Fix.One);

        internal void ApplyTreeStart()
        {
            baseLength += 3;
            RefreshLength();
        }

        internal void ApplyTreeKeystone()
        {
            keystone = TreeLabParams.Keystone;
            baseLength *= keystone == 'A' ? Fix.Ratio(6, 5) : Fix.Ratio(17, 20);
            RefreshLength();
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = WeaponTuning.PikeGrowthPerHit *
                (Self == Sim.Balls[0] ? Fix.Ratio(100 + 4 * TreeLabParams.GrowthRank, 100) : Fix.One);
            CurrentDamage += growth;
            baseLength = FixMath.Min(WeaponTuning.PikeMaxLength, baseLength + growth);
            RefreshLength();
        }

        internal void RefreshLength() => BladeLength = FixMath.Min(WeaponTuning.PikeMaxLength,
            baseLength * (Fix.One + (Self?.Skill != null && Self.Skill.Active ? Self.Skill.LengthBonus : Fix.Zero)));

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, CurrentDamage), baseLength);
    }
}
