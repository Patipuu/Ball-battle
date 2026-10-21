namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Dagger-like: small damage that creeps up a little per hit; every hit spins it faster (the gain shrinks 2% per hit) and its
    /// own hit cooldown is short, so it lands many hits and parries slow blades. Weak against bodies (no parry possible).
    /// </summary>
    public sealed class FangRule : WeaponRule
    {
        public const string WeaponId = "fang";

        public Fix SpinGain { get; private set; } = WeaponTuning.FangStartSpinGain;
        public Fix CurrentDamage { get; private set; } = WeaponTuning.FangStartDamage;
        char keystone;
        bool firstHitBonus;
        bool wasFrenzied;
        int momentumUntil;
        int momentumReadyAt;
        int furyReadyAt;

        public FangRule()
        {
            BladeLength = WeaponTuning.FangLength;
            SpinDegPerTick = WeaponTuning.FangStartSpin;
            HitCooldownTicks = WeaponTuning.FangHitCooldownTicks;
            KnockbackScale = WeaponTuning.FangKnockbackScale;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "SPIN";
        public override Fix StatValue => SpinDegPerTick;

        public override Fix Damage(in HitContext ctx) => CurrentDamage *
            (keystone == 'B' && Self.Skill != null && Self.Skill.Active && Sim.ActiveTick >= furyReadyAt
                ? Fix.Ratio(11, 10) : Fix.One);

        internal void ApplyTreeStart() => firstHitBonus = true;
        internal void ApplyTreeKeystone() => keystone = TreeLabParams.Keystone;

        public override void OnTick()
        {
            var frenzied = Self.Skill != null && Self.Skill.Active;
            if (keystone == 'A' && wasFrenzied && !frenzied && Sim.ActiveTick >= momentumReadyAt)
            {
                momentumUntil = Sim.ActiveTick + 2 * MatchConfig.TicksPerSecond;
                momentumReadyAt = Sim.ActiveTick + 12 * MatchConfig.TicksPerSecond;
            }
            wasFrenzied = frenzied;
        }

        public override void OnHit(in HitContext ctx)
        {
            CurrentDamage += WeaponTuning.FangDamagePerHit;
            var active = Self?.Skill != null && Self.Skill.Active;
            var gain = SpinGain * (active ? Self.Skill.SpinGainMultiplier : Fix.One);
            if (keystone == 'A' && Sim.ActiveTick < momentumUntil) gain *= Fix.Ratio(3, 2);
            SpinDegPerTick += gain;
            if (firstHitBonus) { SpinDegPerTick += 2; firstHitBonus = false; }
            var cap = WeaponTuning.FangMaxSpin;
            if (SpinDegPerTick > cap) SpinDegPerTick = cap;
            if (!active)
                SpinGain *= Self == Sim.Balls[0] && TreeLabParams.GrowthRank > 0
                    ? TreeLabParams.GrowthRank == 1 ? Fix.Ratio(93, 100) : Fix.Ratio(94, 100)
                    : WeaponTuning.FangSpinGainDecay;
            if (keystone == 'B' && active && Sim.ActiveTick >= furyReadyAt)
                furyReadyAt = Sim.ActiveTick + 3 * MatchConfig.TicksPerSecond;
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(SimHash.Mix(h, SpinGain), CurrentDamage);
            h = SimHash.Mix(h, firstHitBonus); h = SimHash.Mix(h, wasFrenzied);
            h = SimHash.Mix(h, momentumUntil); h = SimHash.Mix(h, momentumReadyAt);
            return SimHash.Mix(h, furyReadyAt);
        }
    }
}
