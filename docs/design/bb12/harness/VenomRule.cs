
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
        bool primed;
        char keystone;

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

        internal void ApplyTreeStart() => primed = true;
        internal void ApplyTreeKeystone() => keystone = TreeLabParams.Keystone;

        public override void OnHit(in HitContext ctx)
        {
            MaxStacks = MaxStacks + 1 < StatusEffects.MaxPoisonStacks ? MaxStacks + 1 : StatusEffects.MaxPoisonStacks;
            CurrentDamage += WeaponTuning.VenomDamagePerHit;
        }

        public override void OnHitDealt(BallState target, Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return;
            if (target.Status.PoisonStackCount >= MaxStacks) return;
            // BB-BAL3: the Run DAMAGE card (Bonus.DamagePct) also scales SPORE's own poison. Versus has no cards (DamagePct 0): unchanged.
            var growth = Self == Sim.Balls[0] ? Fix.Ratio(100 + 8 * TreeLabParams.GrowthRank, 100) : Fix.One;
            var poisonDamage = WeaponTuning.VenomDps * growth * (Fix.One + Self.Bonus.DamagePct) *
                (keystone == 'A' ? Fix.Ratio(5, 4) : keystone == 'B' ? Fix.Ratio(17, 20) : Fix.One);
            var poisonTicks = WeaponTuning.VenomSeconds * MatchConfig.TicksPerSecond;
            if (keystone == 'A') poisonTicks = poisonTicks * 4 / 5;
            if (keystone == 'B') poisonTicks = poisonTicks * 5 / 4;
            Sim.ApplyPoison(target, Self, poisonDamage, poisonTicks);
            if (primed)
            {
                primed = false;
                if (target.Status.PoisonStackCount < MaxStacks)
                    Sim.ApplyPoison(target, Self, poisonDamage, poisonTicks);
            }
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(SimHash.Mix(h, MaxStacks), CurrentDamage), primed ? 1 : 0);
    }
}
