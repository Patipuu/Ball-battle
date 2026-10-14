namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Bare body: no blade, so it can never parry or be parried. Damage = own speed × a factor that grows with
    /// every hit. Starts below the normal speed cap; hits and wall bounces raise its cap, and every wall bounce
    /// adds a little speed. Against another body only the faster ball lands the blow (MatchSim rule).
    /// </summary>
    public sealed class BrawlerRule : WeaponRule
    {
        public const string WeaponId = "brawler";

        public Fix DamageFactor { get; private set; } = WeaponTuning.BrawlerStartDamagePerSpeed;

        public BrawlerRule()
        {
            MaxSpeedBonus = WeaponTuning.BrawlerStartSpeedBonus;
            WallSpeedBoost = WeaponTuning.BrawlerWallSpeedBoost;
        }

        public override string Id => WeaponId;
        public override bool HasBlade => false;
        public override bool BodyAttacks => true;
        public override string StatLabel => "MAX";

        /// <summary>Actual speed cap (match MaxSpeed + bonus), so the HUD never shows a negative number.</summary>
        public override Fix StatValue => (Config != null ? Config.MaxSpeed : Fix.Zero) + MaxSpeedBonus;

        public override Fix Damage(in HitContext ctx) => ctx.AttackerSpeed * DamageFactor;

        public override void OnHit(in HitContext ctx)
        {
            DamageFactor += WeaponTuning.BrawlerDamagePerSpeedPerHit;
            RaiseCap(WeaponTuning.BrawlerSpeedBonusPerHit);
        }

        public override void OnWall() => RaiseCap(WeaponTuning.BrawlerSpeedBonusPerWall);

        void RaiseCap(Fix amount)
        {
            MaxSpeedBonus += amount;
            if (MaxSpeedBonus > WeaponTuning.BrawlerMaxSpeedBonus) MaxSpeedBonus = WeaponTuning.BrawlerMaxSpeedBonus;
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, DamageFactor);
    }
}