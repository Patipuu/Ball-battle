using System;

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

        public int Arrows { get; private set; } = 1;
        public float ArrowDamage => arrow.Damage;

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
        public override float StatValue => Arrows;

        public override float Damage(in HitContext ctx) => WeaponTuning.VolleyMeleeDamage;

        public override void OnHit(in HitContext ctx)
        {
            Arrows = Math.Min(WeaponTuning.VolleyMaxArrows, Arrows + 1);
            arrow.Damage += WeaponTuning.VolleyArrowDamagePerHit;
        }

        public override void OnTick()
        {
            if (++timer < WeaponTuning.VolleyIntervalTicks) return;
            timer = 0;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            var aim = (foe.Pos - Self.Pos).NormalizedOr(new Vec2(1f, 0f));
            var aimDeg = MathF.Atan2(aim.Y, aim.X) * (180f / MathF.PI);
            for (var k = 0; k < Arrows; k++)
            {
                var offset = (k - (Arrows - 1) * 0.5f) * WeaponTuning.VolleySpreadDeg;
                var dir = Vec2.FromAngleDeg(aimDeg + offset);
                Sim.FireProjectile(Self, Self.Pos + dir * (Self.Radius + 3f), dir * WeaponTuning.VolleyArrowSpeed, arrow);
            }
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Arrows), timer), arrow.Damage);
    }
}