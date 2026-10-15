
namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Engineer: every melee hit drops a turret where the ball stands (max 6, the oldest is replaced). Turrets do not
    /// block movement; all of them shoot at the nearest foe together. A turret the shrinking arena reaches is lost.
    /// </summary>
    public sealed class RigRule : WeaponRule
    {
        public const string WeaponId = "rig";
        readonly FixVec2[] turrets = new FixVec2[WeaponTuning.RigMaxTurrets];
        readonly FixVec2[] scratch = new FixVec2[WeaponTuning.RigMaxTurrets];
        readonly ProjectileSpec shot;
        Fix damage = WeaponTuning.RigMeleeDamage;
        int count;
        int next;
        int timer;

        public RigRule()
        {
            BladeLength = WeaponTuning.RigLength;
            SpinDegPerTick = WeaponTuning.RigSpin;
            shot = new ProjectileSpec
            {
                Radius = WeaponTuning.RigShotRadius,
                Damage = WeaponTuning.RigShotDamage,
                Bounces = 0,
                Hits = 1,
                LifetimeTicks = WeaponTuning.RigShotLifetimeTicks
            };
        }

        public override string Id => WeaponId;
        public override string StatLabel => "RIGS";
        public override Fix StatValue => count;

        public int TurretCount => count;
        public FixVec2 GetTurret(int index) => turrets[index];
        internal void BeginOverclock() => timer = 0;
        int Interval => Self.Skill != null && Self.Skill.Active ? WeaponTuning.RigFireIntervalTicks / 2 : WeaponTuning.RigFireIntervalTicks;
        /// <summary>TAP-3: a tap needs a turret and a third of a charge.</summary>
        internal bool Charged => count > 0 && timer * 3 >= Tap.TapTuning.ChargeThirds * Interval;

        public override Fix Damage(in HitContext ctx) => damage;

        public override void OnHit(in HitContext ctx)
        {
            damage += WeaponTuning.RigDamagePerHit;
            PlaceTurret();
        }

        /// <summary>TAP-1 HIVE: every turret fires now and the regular volley timer restarts. False when there is no turret.</summary>
        internal bool FireAllNow()
        {
            if (count == 0) return false;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return false;
            var interval = Interval;
            var spec = shot;
            spec.Damage = spec.Damage * Fix.Ratio(timer < interval ? timer : interval, interval);   // TAP-3: the charge built so far
            timer = 0;
            for (var i = 0; i < count; i++)
            {
                var dir = (foe.Pos - turrets[i]).NormalizedOr(FixVec2.Right);
                Sim.FireProjectile(Self, turrets[i] + dir * (WeaponTuning.RigTurretRadius + 1), dir * WeaponTuning.RigShotSpeed, spec);
            }
            return true;
        }

        internal void PlaceTurret()
        {
            if (count < turrets.Length) { turrets[count++] = Self.Pos; return; }
            turrets[next] = Self.Pos;
            next = (next + 1) % turrets.Length;
        }

        public override void OnTick()
        {
            DropTurretsOutsideArena();
            if (count == 0) { timer = 0; return; }
            var interval = Interval;
            if (++timer < interval) return;
            timer = 0;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            for (var i = 0; i < count; i++)
            {
                var dir = (foe.Pos - turrets[i]).NormalizedOr(FixVec2.Right);
                Sim.FireProjectile(Self, turrets[i] + dir * (WeaponTuning.RigTurretRadius + 1), dir * WeaponTuning.RigShotSpeed, shot);
            }
            Self.Skill?.TurretVolley();
        }

        void DropTurretsOutsideArena()
        {
            var a = Sim.Arena;
            var m = WeaponTuning.RigTurretRadius;
            var kept = 0;
            var oldest = count == turrets.Length ? next : 0;
            for (var j = 0; j < count; j++)
            {
                var p = turrets[(oldest + j) % turrets.Length];
                if (p.X < a.Left + m || p.X > a.Right - m || p.Y < a.Bottom + m || p.Y > a.Top - m) continue;
                scratch[kept++] = p;
            }
            if (kept == count) return;
            // Keep age order (oldest first) so the next replacement still hits the oldest; clear the tail so equal states hash equal.
            for (var i = 0; i < turrets.Length; i++) turrets[i] = i < kept ? scratch[i] : default;
            count = kept;
            next = 0;
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, damage);
            h = SimHash.Mix(h, count);
            h = SimHash.Mix(h, next);
            h = SimHash.Mix(h, timer);
            for (var i = 0; i < turrets.Length; i++) h = SimHash.Mix(h, turrets[i]);
            return h;
        }
    }
}
