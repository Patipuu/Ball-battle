using System;

namespace BallBattle.Sim.Traits
{
    /// <summary>More damage dealt, much less max HP. High risk, high reward.</summary>
    public sealed class GlassCannonTrait : TraitRule
    {
        public const string TraitId = "glass-cannon";
        public GlassCannonTrait(int level = 1) : base(level) { }
        public override string Id => TraitId;

        public override void OnSpawn()
        {
            var scale = Math.Min(Self.MaxHp, Math.Max(Self.MaxHp - TraitTuning.GlassCannonHpLoss[Level - 1], TraitTuning.GlassCannonMinHp)) / Self.MaxHp;
            Self.MaxHp *= scale;
            Self.Hp *= scale; // keeps the HP fraction (Run mode carries it between fights)
        }

        public override float ModifyOutgoingDamage(BallState target, float damage, DamageKind kind)
            => kind == DamageKind.Weapon || kind == DamageKind.Projectile ? damage * TraitTuning.GlassCannonDamageMul[Level - 1] : damage;

        protected override ulong HashState(ulong h) => h;
    }
}