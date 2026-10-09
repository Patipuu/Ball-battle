namespace BallBattle.Sim.Traits
{
    /// <summary>Every hit landed by a blade adds a poison stack, unless the target already carries this many stacks (any source). Counters slow healing.</summary>
    public sealed class PoisonTipTrait : TraitRule
    {
        public const string TraitId = "poison-tip";
        public PoisonTipTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnHitDealt(BallState target, float damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon && kind != DamageKind.Projectile) return;
            if (target.Status.PoisonStackCount >= TraitTuning.PoisonTipMaxStacks[Level - 1]) return;
            Sim.ApplyPoison(target, Self, TraitTuning.PoisonTipDps[Level - 1], TraitTuning.PoisonTipSeconds[Level - 1] * MatchConfig.TicksPerSecond);
        }

        protected override ulong HashState(ulong h) => h;
    }
}