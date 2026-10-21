namespace BallBattle.Sim.Traits
{
    // Five traits added with the daily-quest update. Numbers in NewTraitTuning (index [level - 1]).

    /// <summary>The lower the ball's HP, the harder its hits (melee and shots): up to +MaxBonus at 0 HP. Turns close fights around.</summary>
    public sealed class BerserkerTrait : TraitRule
    {
        public const string TraitId = "berserker";
        public BerserkerTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override Fix ModifyOutgoingDamage(BallState target, Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return damage;
            var missing = Fix.One - FixMath.Min(Fix.One, FixMath.Max(Fix.Zero, Self.HpFraction));
            return damage * (Fix.One + NewTraitTuning.BerserkerMaxBonus[Level - 1] * missing);
        }

        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Melee hits and shots taken are reduced by a flat share. Poison, spikes and hazards are not.</summary>
    public sealed class IronSkinTrait : TraitRule
    {
        public const string TraitId = "iron-skin";
        public IronSkinTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override Fix ModifyIncomingDamage(BallState attacker, Fix damage, DamageKind kind)
            => kind == DamageKind.Weapon || kind == DamageKind.Projectile ? damage * (Fix.One - NewTraitTuning.IronSkinReduction[Level - 1]) : damage;

        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Heals a little every second of the fight.</summary>
    public sealed class RegenTrait : TraitRule
    {
        public const string TraitId = "regen";
        int timer;
        public RegenTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnTick()
        {
            if (Self.Hp <= Fix.Zero) return;
            if (++timer < MatchConfig.TicksPerSecond) return;
            timer = 0;
            Sim.Heal(Self, NewTraitTuning.RegenPerSecond[Level - 1]);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, timer);
    }

    /// <summary>Hits on a foe at or below the threshold HP deal extra damage (melee and shots).</summary>
    public sealed class ExecutionerTrait : TraitRule
    {
        public const string TraitId = "executioner";
        public ExecutionerTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override Fix ModifyOutgoingDamage(BallState target, Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return damage;
            if (target == null || target.HpFraction > NewTraitTuning.ExecutionerThreshold) return damage;
            return damage * (Fix.One + NewTraitTuning.ExecutionerBonus[Level - 1]);
        }

        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>A wall bounce charges the next melee hit: it deals extra damage. The charge waits until a hit lands.</summary>
    public sealed class BankShotTrait : TraitRule
    {
        public const string TraitId = "bank-shot";
        bool charged;
        public BankShotTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;
        public bool Charged => charged;

        public override void OnWall() => charged = true;

        // Pure (shield-blocked hits run it too): the charge is spent in OnHitDealt.
        public override Fix ModifyOutgoingDamage(BallState target, Fix damage, DamageKind kind)
            => charged && kind == DamageKind.Weapon ? damage * (Fix.One + NewTraitTuning.BankShotBonus[Level - 1]) : damage;

        public override void OnHitDealt(BallState target, Fix damage, DamageKind kind)
        {
            if (kind == DamageKind.Weapon) charged = false;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, charged);
    }

    /// <summary>Numbers of the five newer traits. Index [level - 1]. Kept apart from TraitTuning so older replays keep their fingerprint.</summary>
    public static class NewTraitTuning
    {
        /// <summary>Berserker: extra damage at 0 HP (scales linearly with missing HP).</summary>
        public static readonly Fix[] BerserkerMaxBonus = { Fix.Ratio(1, 2), Fix.Ratio(7, 10) };
        /// <summary>Iron Skin: share of melee / shot damage taken that is removed.</summary>
        public static readonly Fix[] IronSkinReduction = { Fix.Ratio(3, 20), Fix.Ratio(11, 50) };
        /// <summary>Regen: HP healed once per second.</summary>
        public static readonly Fix[] RegenPerSecond = { Fix.Ratio(2, 5), Fix.Ratio(3, 5) };
        /// <summary>Executioner: the foe's HP fraction at or below which the bonus applies, and the bonus.</summary>
        public static readonly Fix ExecutionerThreshold = Fix.Ratio(7, 20);
        public static readonly Fix[] ExecutionerBonus = { Fix.Ratio(3, 10), Fix.Ratio(9, 20) };
        /// <summary>Bank Shot: extra damage of the first melee hit after a wall bounce.</summary>
        public static readonly Fix[] BankShotBonus = { Fix.Ratio(3, 10), Fix.Ratio(9, 20) };
    }
}
