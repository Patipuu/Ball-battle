namespace BallBattle.Sim.Weapons
{
    /// <summary>Sword-like: +1 damage per hit. Simple and steady; weak against heavy parrying.</summary>
    public sealed class BladeRule : WeaponRule
    {
        public const string WeaponId = "blade";

        public Fix CurrentDamage { get; private set; } = WeaponTuning.BladeStartDamage;

        public BladeRule()
        {
            BladeLength = WeaponTuning.BladeLength;
            SpinDegPerTick = WeaponTuning.BladeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "DMG";
        public override Fix StatValue => CurrentDamage;

        char Fin => '-';   // BB-19c: the FINALE lives in the active skill now (Skills/ActiveSkill.cs)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        bool reburnReady;
        int reburnAt;

        // BB-12g FINALE B : the first two hits +0.25.
        public override Fix Damage(in HitContext ctx) => CurrentDamage + (Fin == 'B' && Self.HitCount < 2 ? Fix.Ratio(1, 4) : Fix.Zero);

        /// <summary>BB-12g OPENER : +0.2 damage at the start.</summary>
        internal void ApplyTreeStart() => CurrentDamage += Fix.Ratio(1, 5);

        /// <summary>BB-12g FINALE A : after a parry, the next hit grows +0.5 more; at most once per 6 s.</summary>
        public override void OnParry(BallState other)
        {
            if (Fin != 'A' || Sim.ActiveTick < reburnAt) return;
            reburnReady = true;
            reburnAt = Sim.ActiveTick + 6 * MatchConfig.TicksPerSecond;
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = WeaponTuning.BladeDamagePerHit * (Self?.Tree != null ? Fix.Ratio(100 + 2 * Growth, 100) : Fix.One);   // INSTINCT +2%/rank
            if (Fin == 'B' && Self.HitCount >= 3) growth *= Fix.Ratio(19, 20);
            CurrentDamage += growth;
            if (reburnReady) { CurrentDamage += Fix.Half; reburnReady = false; }
        }

        protected override ulong HashState(ulong h) => Self?.Tree == null ? SimHash.Mix(h, CurrentDamage)
            : SimHash.Mix(SimHash.Mix(SimHash.Mix(h, CurrentDamage), reburnReady), reburnAt);
    }
}
