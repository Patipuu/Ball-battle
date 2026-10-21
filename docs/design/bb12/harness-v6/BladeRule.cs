namespace BallBattle.Sim.Weapons
{
    /// <summary>Sword-like: +1 damage per hit. Simple and steady; weak against heavy parrying.</summary>
    public sealed class BladeRule : WeaponRule
    {
        public const string WeaponId = "blade";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.BladeStartDamage;
        char keystone;
        bool reburnReady;
        int reburnAt;

        public BladeRule()
        {
            BladeLength = WeaponTuning.BladeLength;
            SpinDegPerTick = WeaponTuning.BladeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "DMG";
        public override Fix StatValue => CurrentDamage;

        public override Fix Damage(in HitContext ctx) => CurrentDamage +
            (keystone == 'B' && Self.HitCount < 2 ? Fix.Ratio(1, 4) : Fix.Zero);

        internal void ApplyTreeStart() => CurrentDamage += Fix.Ratio(1, 5);

        internal void ApplyTreeKeystone()
        {
            keystone = TreeLabParams.Keystone;
        }

        public override void OnParry(BallState other)
        {
            if (keystone != 'A' || Sim.ActiveTick < reburnAt) return;
            reburnReady = true;
            reburnAt = Sim.ActiveTick + 6 * MatchConfig.TicksPerSecond;
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = WeaponTuning.BladeDamagePerHit *
                (TreeLabParams.Player(Self) ? Fix.Ratio(100 + 2 * TreeLabParams.GrowthRank, 100) : Fix.One);
            if (keystone == 'B' && Self.HitCount >= 3) growth *= Fix.Ratio(19, 20);
            CurrentDamage += growth;
            if (reburnReady) { CurrentDamage += Fix.Half; reburnReady = false; }
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(SimHash.Mix(h, CurrentDamage), reburnReady), reburnAt);
    }
}
