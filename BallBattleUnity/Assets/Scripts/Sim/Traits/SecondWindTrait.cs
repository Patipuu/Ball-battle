using System;

namespace BallBattle.Sim.Traits
{
    /// <summary>Once per fight: at or below 30% HP heal a lump sum; a lethal blow is turned into that heal. Turns fights around.</summary>
    public sealed class SecondWindTrait : TraitRule
    {
        public const string TraitId = "second-wind";
        bool used;
        public SecondWindTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnTick()
        {
            if (used || Self.Hp <= 0f || Self.HpFraction > TraitTuning.SecondWindThreshold) return;
            used = true;
            Sim.Heal(Self, TraitTuning.SecondWindHeal[Level - 1]);
        }

        public override bool TryPreventDeath()
        {
            if (used) return false;
            used = true;
            Self.Hp = Math.Min(Self.MaxHp, TraitTuning.SecondWindHeal[Level - 1]);
            return true;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, used);
    }
}