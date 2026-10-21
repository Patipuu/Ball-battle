namespace BallBattle.Sim.Weapons
{
    /// <summary>Sword-like: +1 damage per hit. Simple and steady; weak against heavy parrying.</summary>
    public sealed class BladeRule : WeaponRule
    {
        public const string WeaponId = "blade";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.BladeStartDamage;
        char keystone;

        public BladeRule()
        {
            BladeLength = WeaponTuning.BladeLength;
            SpinDegPerTick = WeaponTuning.BladeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "DMG";
        public override Fix StatValue => CurrentDamage;

        public override Fix Damage(in HitContext ctx) => CurrentDamage;

        internal void ApplyTreeStart() => CurrentDamage += Fix.Half;

        internal void ApplyTreeKeystone()
        {
            keystone = TreeLabParams.Keystone;
            CurrentDamage *= keystone == 'A' ? Fix.Ratio(4, 5) : Fix.Ratio(5, 4);
        }

        public override void OnHit(in HitContext ctx) => CurrentDamage += WeaponTuning.BladeDamagePerHit *
            (Self == Sim.Balls[0] ? Fix.Ratio(100 + 5 * TreeLabParams.GrowthRank, 100) : Fix.One) *
            (keystone == 'A' ? Fix.Ratio(27, 20) : keystone == 'B' ? Fix.Ratio(3, 4) : Fix.One);

        protected override ulong HashState(ulong h) => SimHash.Mix(h, CurrentDamage);
    }
}
