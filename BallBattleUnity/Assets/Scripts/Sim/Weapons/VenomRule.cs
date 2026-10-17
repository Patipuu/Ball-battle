
namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Poisoner: a wide, slow blade. Hits poison the target while it carries fewer stacks than Venom allows;
    /// every hit raises that allowance by one (max 8). Beats healing and Second Wind; loses to fast burst damage.
    /// </summary>
    public sealed class VenomRule : WeaponRule
    {
        public const string WeaponId = "venom";

        public int MaxStacks { get; private set; } = 1;
        public Fix CurrentDamage { get; private set; } = WeaponTuning.VenomMeleeDamage;

        public VenomRule()
        {
            BladeLength = WeaponTuning.VenomLength;
            BladeThickness = WeaponTuning.VenomThickness;
            SpinDegPerTick = WeaponTuning.VenomSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "VENOM";
        public override Fix StatValue => MaxStacks;

        public override Fix Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx)
        {
            MaxStacks = MaxStacks + 1 < StatusEffects.MaxPoisonStacks ? MaxStacks + 1 : StatusEffects.MaxPoisonStacks;
            CurrentDamage += WeaponTuning.VenomDamagePerHit;
        }

        char Fin => '-';   // BB-19c: the FINALE lives in the active skill now (Skills/ActiveSkill.cs)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        int primedHits;

        /// <summary>BB-12g OPENER: the first two poisoning hits may add one more stack.</summary>
        internal void ApplyTreeStart() => primedHits = 2;

        public override void OnHitDealt(BallState target, Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return;
            if (Self.Tree == null)
            {
                if (target.Status.PoisonStackCount >= MaxStacks) return;
                // BB-BAL3: the Run DAMAGE card (Bonus.DamagePct) also scales SPORE's own poison. Versus has no cards (DamagePct 0): unchanged.
                Sim.ApplyPoison(target, Self, WeaponTuning.VenomDps * (Fix.One + Self.Bonus.DamagePct), WeaponTuning.VenomSeconds * MatchConfig.TicksPerSecond);
                return;
            }
            var extra = primedHits > 0;
            if (extra) primedHits--;
            if (target.Status.PoisonStackCount >= MaxStacks) return;
            // INSTINCT +3% dps/rank; FINALE A dps x1.10, time /1.10; FINALE B dps x0.95, time x20/19.
            var poisonDamage = WeaponTuning.VenomDps * Fix.Ratio(100 + 3 * Growth, 100) * (Fix.One + Self.Bonus.DamagePct) *
                (Fin == 'A' ? Fix.Ratio(11, 10) : Fin == 'B' ? Fix.Ratio(19, 20) : Fix.One);
            var poisonTicks = WeaponTuning.VenomSeconds * MatchConfig.TicksPerSecond;
            if (Fin == 'A') poisonTicks = poisonTicks * 10 / 11;
            if (Fin == 'B') poisonTicks = poisonTicks * 20 / 19;
            Sim.ApplyPoison(target, Self, poisonDamage, poisonTicks);
            if (extra && target.Status.PoisonStackCount < MaxStacks) Sim.ApplyPoison(target, Self, poisonDamage, poisonTicks);
        }

        protected override ulong HashState(ulong h) => Self?.Tree == null ? SimHash.Mix(SimHash.Mix(h, MaxStacks), CurrentDamage)
            : SimHash.Mix(SimHash.Mix(SimHash.Mix(h, MaxStacks), CurrentDamage), primedHits);
    }
}