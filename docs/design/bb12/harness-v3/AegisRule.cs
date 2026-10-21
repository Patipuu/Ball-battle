
namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Counter-fighter: a broad shield blade that barely scratches. When it parries, the blow it stopped comes back at
    /// the attacker, and so does part of a body blow (Reflect damage); it absorbs part of a shot's damage. Each time it lands a hit
    /// the shield widens a little. Loses to poison and shots.
    /// BB-6d: its two clocks are now combat counters. Every guard (a parry against a blade or a blade hit taken: AegisGuardMelee
    /// points; a body blow taken: AegisGuardBody; a shot taken: AegisGuardShot) mends AegisMendPerGuard HP per point (was
    /// AegisRegenPerSecond per second) and counts toward rage (was the active-tick clock): after AegisRageStartGuards points each
    /// further point adds 1/AegisRageRampGuards of the base damage. Power follows how much fighting happened, not how long the arena
    /// kept the balls apart.
    /// </summary>
    public sealed class AegisRule : WeaponRule
    {
        public const string WeaponId = "aegis";

        Fix guards;
        char keystone;

        /// <summary>Guard points so far this match.</summary>
        public Fix Guards => guards;

        public AegisRule()
        {
            BladeLength = WeaponTuning.AegisLength;
            BladeThickness = WeaponTuning.AegisThickness;
            SpinDegPerTick = WeaponTuning.AegisSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "WIDTH";
        public override Fix StatValue => BladeThickness;

        internal void ApplyTreeStart() => BladeThickness += Fix.Ratio(1, 4);
        internal void ApplyRunBaseline() => BladeThickness += Fix.Ratio(1, 4);
        internal void ApplyTreeKeystone()
        {
            keystone = TreeLabParams.Keystone;
        }

        public override Fix Damage(in HitContext ctx)
        {
            var d = WeaponTuning.AegisChipDamage + (BladeThickness - WeaponTuning.AegisThickness) * WeaponTuning.AegisDamagePerWidth;
            var rage = guards - WeaponTuning.AegisRageStartGuards;
            if (rage > WeaponTuning.AegisRageMaxGuards) rage = WeaponTuning.AegisRageMaxGuards;   // no runaway against a stream of shots
            return rage > Fix.Zero ? d + d * rage / WeaponTuning.AegisRageRampGuards : d;   // d × (1 + rage / ramp)
        }

        /// <summary>A guard worth <paramref name="points"/>: mend a little per point and move the rage counter.</summary>
        void Guard(Fix points, bool mend = true)
        {
            if (points <= Fix.Zero) return;
            guards += points;
            if (mend) Sim.Heal(Self, WeaponTuning.AegisMendPerGuard * points);
        }

        public override void OnHit(in HitContext ctx) => BladeThickness = FixMath.Min(WeaponTuning.AegisMaxThickness,
            BladeThickness + WeaponTuning.AegisWidthPerHit *
            (Self == Sim.Balls[0] ? Fix.Ratio(100 + 5 * TreeLabParams.GrowthRank, 100) : Fix.One));

        /// <summary>A body blow (bladeless attacker, e.g. Brawler) cannot be parried, so part of it is sent back.</summary>
        public override void OnHitTaken(BallState attacker, Fix damage, DamageKind kind)
        {
            if (Self.Hp <= Fix.Zero) return;
            if (kind == DamageKind.Projectile) Guard(WeaponTuning.AegisGuardShot);
            else if (kind == DamageKind.Weapon)
                Guard(attacker.Weapon.HasBlade ? WeaponTuning.AegisGuardMelee : WeaponTuning.AegisGuardBody, !(attacker.Weapon is AegisRule));   // no mend in a mirror
            if (kind == DamageKind.Projectile)
            {
                var absorb = WeaponTuning.AegisShotAbsorbPct +
                    (keystone == 'A' ? Fix.Ratio(1, 20) : keystone == 'B' ? -Fix.Ratio(1, 20) : Fix.Zero);
                Sim.Heal(Self, damage * absorb);
                return;
            }
            if (kind != DamageKind.Weapon || attacker.Weapon.HasBlade) return;
            // BB-BAL3: the Run DAMAGE card (Bonus.DamagePct) also scales MIRROR's reflects. Versus has no cards (DamagePct 0): unchanged.
            Sim.DealDamage(Self, attacker, damage * (Self.Skill != null && Self.Skill.Active ? Fix.Ratio(3, 4) : WeaponTuning.AegisBodyReflectPct) * (Fix.One + Self.Bonus.DamagePct) * ReflectScale, DamageKind.Reflect);
        }

        public override void OnParry(BallState other)
        {
            if (Self.Hp <= Fix.Zero || !other.Weapon.HasBlade) return;
            // Between two Aegis the parry counts AegisGuardMirror points: their rage must end the mirror (only mirror matches use it).
            var mirror = other.Weapon is AegisRule;
            Guard(mirror ? WeaponTuning.AegisGuardMirror : WeaponTuning.AegisGuardMelee, !mirror);   // a mirror only rages: mending each other would never end it
            if (other.Weapon is AegisRule) return; // two Aegis would only trade reflects until both fall
            var blow = other.Weapon.Damage(new HitContext(other.Vel.Length, Fix.Zero));
            Sim.DealDamage(Self, other, blow * (Self.Skill != null && Self.Skill.Active ? Self.Skill.ParryReflect : WeaponTuning.AegisReflectPct) * (Fix.One + Self.Bonus.DamagePct) * ReflectScale, DamageKind.Reflect);
        }

        Fix ReflectScale => keystone == 'A' ? Fix.Ratio(19, 20) : keystone == 'B' ? Fix.Ratio(11, 10) : Fix.One;

        // The width (BladeThickness) is hashed by the base class; the guard counter is hashed here.
        protected override ulong HashState(ulong h) => SimHash.Mix(h, guards);
    }
}
