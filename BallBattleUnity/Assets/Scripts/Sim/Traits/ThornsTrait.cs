namespace BallBattle.Sim.Traits
{
    /// <summary>Sends a share of every melee hit taken back at the attacker (Reflect: skips shields, no hit hooks). Counters strong blades.</summary>
    public sealed class ThornsTrait : TraitRule
    {
        public const string TraitId = "thorns";
        public ThornsTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnHitTaken(BallState attacker, float damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon || Self.Hp <= 0f) return; // a ball struck down by this hit does not strike back
            Sim.DealDamage(Self, attacker, damage * TraitTuning.ThornsReflectPct[Level - 1], DamageKind.Reflect);
        }

        protected override ulong HashState(ulong h) => h;
    }
}