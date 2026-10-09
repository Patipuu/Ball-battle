using System;

namespace BallBattle.Sim.Traits
{
    /// <summary>Bigger (heavier in collisions), resists knockback, slower, more max HP. Counters Brawler; loses to range.</summary>
    public sealed class HeavyTrait : TraitRule
    {
        public const string TraitId = "heavy";
        public HeavyTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnSpawn()
        {
            var i = Level - 1;
            var target = Math.Min(Self.Radius * TraitTuning.HeavyRadiusScale[i], Math.Max(Self.Radius, TraitTuning.HeavyMaxRadius[i]));
            var grow = target - Self.Radius;
            Self.Radius += grow;
            Self.BladeShift += grow;
            Self.Bonus.SpeedPct += TraitTuning.HeavySpeedPct[i];
            Self.KnockbackResist = TraitTuning.HeavyKnockbackResist[i];
            var scale = 1f + TraitTuning.HeavyMaxHpPct[i];
            Self.MaxHp *= scale;
            Self.Hp *= scale; // keeps the HP fraction, so Run mode carries no free HP out of the fight
        }

        protected override ulong HashState(ulong h) => h;
    }
}
