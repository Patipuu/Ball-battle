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

        char Fin => Self?.Tree != null ? Self.Tree.Finale : '-';   // BB-12g FINALE ('-' = none / no tree)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        bool firstHitBonus;
        bool wasFrenzied;
        int momentumUntil;
        int momentumReadyAt;
        int furyReadyAt;

        // BB-12g FINALE B (Máu chiến): the first hit in FRENZY +10%, again after 3 s.
        public override Fix Damage(in HitContext ctx) => CurrentDamage *
            (Fin == 'B' && Self.Skill != null && Self.Skill.Active && Sim.ActiveTick >= furyReadyAt ? Fix.Ratio(11, 10) : Fix.One);

        /// <summary>BB-12g OPENER (Vào guồng): the first hit adds 2 spin.</summary>
        internal void ApplyTreeStart() => firstHitBonus = true;

        /// <summary>BB-12g FINALE A (Chưa hạ nhiệt): 2 s after FRENZY ends, hits gain +50% spin; again after 12 s.</summary>
        public override void OnTick()
        {
            if (Self?.Tree == null) return;
            var frenzied = Self.Skill != null && Self.Skill.Active;
            if (Fin == 'A' && wasFrenzied && !frenzied && Sim.ActiveTick >= momentumReadyAt)
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
            if (Fin == 'A' && Sim.ActiveTick < momentumUntil) gain *= Fix.Ratio(3, 2);
            SpinDegPerTick += gain;
            if (firstHitBonus) { SpinDegPerTick += 2; firstHitBonus = false; }
            if (SpinDegPerTick > WeaponTuning.FangMaxSpin) SpinDegPerTick = WeaponTuning.FangMaxSpin;
            // INSTINCT (Quán tính): the spin gain decays less (0.92 -> 0.93 / 0.94).
            if (!active) SpinGain *= Growth == 1 ? Fix.Ratio(93, 100) : Growth >= 2 ? Fix.Ratio(94, 100) : WeaponTuning.FangSpinGainDecay;
            if (Fin == 'B' && active && Sim.ActiveTick >= furyReadyAt) furyReadyAt = Sim.ActiveTick + 3 * MatchConfig.TicksPerSecond;
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(SimHash.Mix(h, SpinGain), CurrentDamage);
            if (Self?.Tree == null) return h;
            h = SimHash.Mix(h, firstHitBonus); h = SimHash.Mix(h, wasFrenzied);
            h = SimHash.Mix(h, momentumUntil); h = SimHash.Mix(h, momentumReadyAt);
            return SimHash.Mix(h, furyReadyAt);
        }
    }
}
