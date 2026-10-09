namespace BallBattle.Sim.Traits
{
    /// <summary>A second, shorter blade opposite the first: wider coverage, weaker hits, twice the target for parries. Needs a blade.</summary>
    public sealed class TwinBladeTrait : TraitRule
    {
        public const string TraitId = "twin-blade";
        public TwinBladeTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnSpawn() => Self.TwinBladeScale = TraitTuning.TwinBladeScale[Level - 1];

        public override float ModifyOutgoingDamage(BallState target, float damage, DamageKind kind)
            => kind == DamageKind.Weapon ? damage * TraitTuning.TwinBladeDamageMul[Level - 1] : damage;

        protected override ulong HashState(ulong h) => h;
    }
}