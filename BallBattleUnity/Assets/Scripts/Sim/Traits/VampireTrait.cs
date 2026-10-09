namespace BallBattle.Sim.Traits
{
    /// <summary>Heals a share of the damage of every hit it lands. Counters slow poison; loses to burst and reflect.</summary>
    public sealed class VampireTrait : TraitRule
    {
        public const string TraitId = "vampire";
        public VampireTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnHitDealt(BallState target, float damage, DamageKind kind)
            => Sim.Heal(Self, damage * TraitTuning.VampireHealPct[Level - 1]);

        protected override ulong HashState(ulong h) => h;
    }
}
