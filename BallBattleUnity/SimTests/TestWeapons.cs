using BallBattle.Sim;

namespace BallBattle.SimTests
{
    /// <summary>Test doubles for exercising match rules before the real weapons exist (Phase 3).</summary>
    sealed class FixedBladeRule : WeaponRule
    {
        readonly Fix damage;
        public int Parries;
        public int Walls;

        public FixedBladeRule(float damage = 5f, float length = 25f, float spin = 6f)
        {
            this.damage = FT.X(damage);
            BladeLength = FT.X(length);
            SpinDegPerTick = FT.X(spin);
        }

        public override string Id => "test-fixed-blade";
        public override Fix Damage(in HitContext ctx) => damage;
        public override void OnParry(BallState other) => Parries++;
        public override void OnWall() => Walls++;
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>No blade, no body damage: a ball that only moves.</summary>
    sealed class HarmlessRule : WeaponRule
    {
        public int Walls;
        public override string Id => "test-harmless";
        public override bool HasBlade => false;
        public override Fix Damage(in HitContext ctx) => Fix.Zero;
        public override void OnWall() => Walls++;
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Body attacker with fixed damage.</summary>
    sealed class FixedBodyRule : WeaponRule
    {
        readonly Fix damage;
        public FixedBodyRule(float damage) => this.damage = FT.X(damage);
        public override string Id => "test-body";
        public override bool HasBlade => false;
        public override bool BodyAttacks => true;
        public override Fix Damage(in HitContext ctx) => damage;
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>+1 damage per hit (Blade-like), for pacing checks before Phase 3.</summary>
    sealed class ScalingBladeRule : WeaponRule
    {
        Fix damage = 1;
        public ScalingBladeRule(float length = 24f) { BladeLength = FT.X(length); SpinDegPerTick = 6; }
        public override string Id => "test-scaling-blade";
        public override Fix Damage(in HitContext ctx) => damage;
        public override void OnHit(in HitContext ctx) => damage += 1;
        protected override ulong HashState(ulong h) => SimHash.Mix(h, damage);
    }
}
