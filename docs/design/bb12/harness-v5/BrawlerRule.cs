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

        internal void ApplyTreeStart() => DamageFactor += Fix.Ratio(1, 500);
        internal void ApplyRunBaseline() => DamageFactor *= Fix.Ratio(9, 10);

        internal void ApplyTreeKeystone()
        {
            if (TreeLabParams.Keystone == 'A')
            {
                WallSpeedBoost *= Fix.Ratio(17, 20);
            }
            else
            {
                WallSpeedBoost *= Fix.Ratio(23, 20);
            }
        }

        public override void OnHit(in HitContext ctx)
        {
            var growth = Self == Sim.Balls[0] ? Fix.Ratio(100 + 2 * TreeLabParams.GrowthRank, 100) : Fix.One;
            DamageFactor += WeaponTuning.BrawlerDamagePerSpeedPerHit * growth *
                (TreeLabParams.Player(Self) ? Fix.Ratio(19, 20) : Fix.One);
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
