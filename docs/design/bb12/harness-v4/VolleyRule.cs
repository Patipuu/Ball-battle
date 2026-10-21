
namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Archer: a stubby blade, and every second a fan of arrows flies at the nearest foe. Every melee hit adds
    /// an arrow (max 8) and makes arrows hit harder. Strong in big arenas, weak once the arena shrinks; a parry knocks arrows back.
    /// </summary>
    public sealed class VolleyRule : WeaponRule
    {
        public const string WeaponId = "volley";
        ProjectileSpec arrow;
        int timer;
        char keystone;
        bool firstVolleyExtra;

        public int Arrows { get; private set; } = 1;
        public Fix ArrowDamage => arrow.Damage;

        public VolleyRule()
        {
            BladeLength = WeaponTuning.VolleyLength;
            SpinDegPerTick = WeaponTuning.VolleySpin;
            arrow = new ProjectileSpec
            {
                Radius = WeaponTuning.VolleyArrowRadius,
                Damage = WeaponTuning.VolleyArrowDamage,
                Bounces = 1,
                Hits = 1,
                LifetimeTicks = WeaponTuning.VolleyArrowLifetimeTicks
            };
        }

        public override string Id => WeaponId;
        public override string StatLabel => "ARROWS";
        public override Fix StatValue => Arrows;

        public override Fix Damage(in HitContext ctx) => WeaponTuning.VolleyMeleeDamage;

        internal void ApplyTreeStart() => firstVolleyExtra = true;
        internal void ApplyTreeKeystone() => keystone = TreeLabParams.Keystone;

        public override void OnHit(in HitContext ctx)
        {
            Arrows = Arrows + 1 < WeaponTuning.VolleyMaxArrows ? Arrows + 1 : WeaponTuning.VolleyMaxArrows;
            arrow.Damage += WeaponTuning.VolleyArrowDamagePerHit +
                (Self == Sim.Balls[0] ? Fix.Ratio(TreeLabParams.GrowthRank, 100) : Fix.Zero);
        }

        public override void OnTick()
        {
            if (++timer < WeaponTuning.VolleyIntervalTicks) return;
            timer = 0;
            FireVolley(Fix.Zero);
        }

        internal void FireVolley(Fix extraDamage)
        {
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            var aim = (foe.Pos - Self.Pos).NormalizedOr(FixVec2.Right);
            var count = keystone == 'A' ? System.Math.Max(1, Arrows - 1) :
                keystone == 'B' ? System.Math.Min(9, Arrows + 1) : Arrows;
            var bonus = firstVolleyExtra;
            firstVolleyExtra = false;
            for (var k = 0; k < count + (bonus ? 1 : 0); k++)
            {
                var total = count + (bonus ? 1 : 0);
                var offset = WeaponTuning.VolleySpreadDeg * (2 * k - (total - 1)) / 2;
                if (keystone == 'B' && count > 1 && !bonus)
                    offset = offset * (Arrows - 1) * 4 / ((count - 1) * 5);
                var rotation = FixVec2.FromAngleDeg(offset);
                var dir = new FixVec2(
                    aim.X * rotation.X - aim.Y * rotation.Y,
                    aim.X * rotation.Y + aim.Y * rotation.X);
                var spec = arrow;
                var original = arrow.Damage + extraDamage;
                spec.Damage = bonus && k == count ? original * Fix.Half :
                    keystone == 'B' ? original * Fix.Ratio(Arrows, count) * Fix.Ratio(21, 20) :
                    keystone == 'A' ? original * Fix.Ratio(Arrows, count) : original;
                Sim.FireProjectile(Self, Self.Pos + dir * (Self.Radius + 3), dir * WeaponTuning.VolleyArrowSpeed, spec);
            }
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Arrows), timer), arrow.Damage), firstVolleyExtra);
    }
}
