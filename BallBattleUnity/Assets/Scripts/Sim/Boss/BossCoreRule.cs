namespace BallBattle.Sim.Boss
{
    /// <summary>The seven authored bosses attack through their BossRule, never through a random player weapon.</summary>
    public sealed class BossCoreRule : WeaponRule
    {
        public const string WeaponId = "boss_core";
        readonly string bossId;
        public BossCoreRule(string bossId = null)
        {
            this.bossId = bossId;
            HitCooldownTicks = 36;
        }
        public override string Id => WeaponId;
        public override bool HasBlade => false;
        public override bool BodyAttacks => true;
        public override Fix Damage(in HitContext ctx) => BossTuning.ContactDamage(bossId);
        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(h, bossId ?? ""), BossTuning.ContactDamage(bossId));
    }
}
