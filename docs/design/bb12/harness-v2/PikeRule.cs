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
            (Self.Skill != null && Self.Skill.Active && keystone == 'A' ? Fix.Ratio(23, 25) :
             Self.Skill != null && Self.Skill.Active && keystone == 'B' ? Fix.Ratio(27, 25) : Fix.One);

        internal void ApplyTreeStart()
        {
            baseLength += 1;
            RefreshLength();
        }

        internal void ApplyTreeKeystone()
        {
            keystone = TreeLabParams.Keystone;
            RefreshLength();
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = WeaponTuning.PikeGrowthPerHit *
                (Self == Sim.Balls[0] ? Fix.Ratio(100 + 2 * TreeLabParams.GrowthRank, 100) : Fix.One);
            CurrentDamage += growth;
            baseLength = FixMath.Min(WeaponTuning.PikeMaxLength, baseLength + growth);
            RefreshLength();
        }

        internal void RefreshLength()
        {
            var active = Self?.Skill != null && Self.Skill.Active;
            var length = baseLength * (Fix.One + (active ? Self.Skill.LengthBonus : Fix.Zero));
            if (active && keystone == 'A') length *= Fix.Ratio(27, 25);
            if (active && keystone == 'B') length *= Fix.Ratio(23, 25);
            BladeLength = FixMath.Min(WeaponTuning.PikeMaxLength, length);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, CurrentDamage), baseLength);
    }
}
