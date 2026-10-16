namespace BallBattle.Sim.Weapons
{
    /// <summary>Spear-like: slow spin; every hit adds length and damage. Long reach late, but a long shaft is easy to parry.</summary>
    public sealed class PikeRule : WeaponRule
    {
        public const string WeaponId = "pike";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.PikeStartDamage;
        Fix baseLength = WeaponTuning.PikeStartLength;

        public PikeRule()
        {
            BladeLength = WeaponTuning.PikeStartLength;
            SpinDegPerTick = WeaponTuning.PikeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "LEN";
        public override Fix StatValue => BladeLength;
        public override string StatText => FixMath.Format(BladeLength, 0);

        char Fin => Self?.Tree != null ? Self.Tree.Finale : '-';   // BB-12g FINALE ('-' = none / no tree)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;

        // BB-12g FINALE A (Quăng xa) / B (Giật cần): while LUNGE is active, damage x0.92 / x1.08 (length x1.08 / x0.92).
        public override Fix Damage(in HitContext ctx) => Self?.Tree == null || Self.Skill == null || !Self.Skill.Active ? CurrentDamage
            : CurrentDamage * (Fin == 'A' ? Fix.Ratio(23, 25) : Fin == 'B' ? Fix.Ratio(27, 25) : Fix.One);

        /// <summary>BB-12g OPENER (Thả câu): the hook starts 1 longer.</summary>
        internal void ApplyTreeStart()
        {
            baseLength += 1;
            RefreshLength();
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = Self?.Tree != null ? WeaponTuning.PikeGrowthPerHit * Fix.Ratio(100 + 2 * Growth, 100) : WeaponTuning.PikeGrowthPerHit;   // INSTINCT +2%/rank
            CurrentDamage += growth;
            baseLength = FixMath.Min(WeaponTuning.PikeMaxLength, baseLength + growth);
            RefreshLength();
        }

        internal void RefreshLength()
        {
            var active = Self?.Skill != null && Self.Skill.Active;
            var length = baseLength * (Fix.One + (active ? Self.Skill.LengthBonus : Fix.Zero)
                + (Self?.Tap != null ? Self.Tap.LengthBonus : Fix.Zero));   // TAP-1 ANGLER cast (exact + 0 without a tap)
            if (active && Fin == 'A') length *= Fix.Ratio(27, 25);
            if (active && Fin == 'B') length *= Fix.Ratio(23, 25);
            BladeLength = FixMath.Min(WeaponTuning.PikeMaxLength, length);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, CurrentDamage), baseLength);
    }
}
