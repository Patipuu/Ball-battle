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

        /// <summary>BB-12g OPENER: DamageFactor +0.002 at the start.</summary>
        internal void ApplyTreeStart() => DamageFactor += Fix.Ratio(1, 500);
        /// <summary>BB-12g Run-only baseline (every Run, any tree): DamageFactor x0.85 at the start.</summary>
        internal void ApplyRunBaseline() => DamageFactor *= Fix.Ratio(17, 20);
        /// <summary>BB-12g FINALE A: wall boost x0.85 / B: x1.15 (their spike damage change is in MatchSim.Obstacles).</summary>
        internal void ApplyTreeKeystone() => WallSpeedBoost *= Self.Tree.Finale == 'A' ? Fix.Ratio(17, 20) : Fix.Ratio(23, 20);

        public override void OnHit(in HitContext ctx)
        {
            // Run (tree) growth: INSTINCT +2%/rank, then the Run-only x0.90 baseline.
            if (Self?.Tree != null)
                DamageFactor += WeaponTuning.BrawlerDamagePerSpeedPerHit * Fix.Ratio(100 + 2 * Self.Tree.Growth, 100) * Fix.Ratio(9, 10);
            else DamageFactor += WeaponTuning.BrawlerDamagePerSpeedPerHit;
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