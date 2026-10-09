namespace BallBattle.Sim.Traits
{
    /// <summary>Every Nth parry grows the weapon's stat (as if it had landed a hit). Needs a blade to parry with.</summary>
    public sealed class ParryMasterTrait : TraitRule
    {
        public const string TraitId = "parry-master";
        int parries;
        public ParryMasterTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnParry(BallState other)
        {
            if (++parries < TraitTuning.ParryMasterEvery[Level - 1]) return;
            parries = 0;
            Self.Weapon.OnHit(new HitContext(Self.Vel.Length, 0f));
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, parries);
    }
}