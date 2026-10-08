namespace BallBattle.Sim.Weapons
{
    /// <summary>Sword-like: +1 damage per hit. Simple and steady; weak against heavy parrying.</summary>
    public sealed class BladeRule : WeaponRule
    {
        public const string WeaponId = "blade";

        public float CurrentDamage { get; private set; } = WeaponTuning.BladeStartDamage;

        public BladeRule()
        {
            BladeLength = WeaponTuning.BladeLength;
            SpinDegPerTick = WeaponTuning.BladeSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "DMG";
        public override float StatValue => CurrentDamage;

        public override float Damage(in HitContext ctx) => CurrentDamage;

        public override void OnHit(in HitContext ctx) => CurrentDamage += WeaponTuning.BladeDamagePerHit;

        protected override ulong HashState(ulong h) => SimHash.Mix(h, CurrentDamage);
    }
}
