namespace BallBattle.Sim.Traits
{
    /// <summary>Every body bounce hurts the other ball. Counters Brawler and close brawls.</summary>
    public sealed class SpikyTrait : TraitRule
    {
        public const string TraitId = "spiky";
        int cooldown;
        public SpikyTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnTick()
        {
            if (cooldown > 0) cooldown--;
        }

        public override void OnBodyContact(BallState other)
        {
            if (cooldown > 0) return;
            cooldown = TraitTuning.SpikyCooldownTicks;
            Sim.DealDamage(Self, other, TraitTuning.SpikyContactDamage[Level - 1], DamageKind.Reflect);
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, cooldown);
    }
}
