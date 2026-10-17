
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

        char Fin => '-';   // BB-19c: the FINALE lives in the active skill now (Skills/ActiveSkill.cs)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        bool firstVolleyExtra;

        /// <summary>BB-12g OPENER: the first volley has one extra arrow at 50% damage.</summary>
        internal void ApplyTreeStart() => firstVolleyExtra = true;

        public override void OnHit(in HitContext ctx)
        {
            Arrows = Arrows + 1 < WeaponTuning.VolleyMaxArrows ? Arrows + 1 : WeaponTuning.VolleyMaxArrows;
            arrow.Damage += Self?.Tree != null ? WeaponTuning.VolleyArrowDamagePerHit + Fix.Ratio(Growth, 100) : WeaponTuning.VolleyArrowDamagePerHit;   // INSTINCT +0.01/rank
        }

        public override void OnTick()
        {
            if (++timer < WeaponTuning.VolleyIntervalTicks) return;
            timer = 0;
            FireVolley(Fix.Zero);
        }

        /// <summary>TAP-3 SPARKLER: the tap fires the volley now with the charge built so far (damage x timer/interval) and the regular
        /// timer restarts, so tapping moves arrows in time but adds none. Needs a third of a charge.</summary>
        internal bool Charged => timer * 3 >= Tap.TapTuning.ChargeThirds * WeaponTuning.VolleyIntervalTicks;
        internal void FireNow()
        {
            var charge = Fix.Ratio(timer < WeaponTuning.VolleyIntervalTicks ? timer : WeaponTuning.VolleyIntervalTicks, WeaponTuning.VolleyIntervalTicks);
            timer = 0;
            FireVolley(Fix.Zero, charge * Tap.TapTuning.VolleyTapDamage);
        }

        internal void FireVolley(Fix extraDamage) => FireVolley(extraDamage, Fix.One);

        void FireVolley(Fix extraDamage, Fix charge)
        {
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            var aim = (foe.Pos - Self.Pos).NormalizedOr(FixVec2.Right);
            // BB-12g FINALE A: one arrow fewer (min 1) / B: one more (max 9); the old total damage is shared by the new count
            // (B x1.05 and a narrower fan). OPENER: the first volley has one more arrow at 50%. No tree: exactly as before.
            var fin = Fin;
            var count = fin == 'A' ? (Arrows > 1 ? Arrows - 1 : 1) : fin == 'B' ? (Arrows < 9 ? Arrows + 1 : 9) : Arrows;
            var bonus = firstVolleyExtra;
            firstVolleyExtra = false;
            var total = count + (bonus ? 1 : 0);
            for (var k = 0; k < total; k++)
            {
                var offset = WeaponTuning.VolleySpreadDeg * (2 * k - (total - 1)) / 2;   // (k - (n-1)/2) × spread, exact for whole/half steps
                if (fin == 'B' && count > 1 && !bonus) offset = offset * (Arrows - 1) * 4 / ((count - 1) * 5);
                var rotation = FixVec2.FromAngleDeg(offset);
                var dir = new FixVec2(
                    aim.X * rotation.X - aim.Y * rotation.Y,
                    aim.X * rotation.Y + aim.Y * rotation.X);
                var spec = arrow;
                spec.Damage += extraDamage;
                if (bonus && k == count) spec.Damage = spec.Damage * Fix.Half;
                else if (fin == 'B') spec.Damage = spec.Damage * Fix.Ratio(Arrows, count) * Fix.Ratio(21, 20);
                else if (fin == 'A') spec.Damage = spec.Damage * Fix.Ratio(Arrows, count);
                if (charge != Fix.One) spec.Damage = spec.Damage * charge;
                Sim.FireProjectile(Self, Self.Pos + dir * (Self.Radius + 3), dir * WeaponTuning.VolleyArrowSpeed, spec);
            }
        }

        protected override ulong HashState(ulong h) => Self?.Tree == null ? SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Arrows), timer), arrow.Damage)
            : SimHash.Mix(SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Arrows), timer), arrow.Damage), firstVolleyExtra);
    }
}
