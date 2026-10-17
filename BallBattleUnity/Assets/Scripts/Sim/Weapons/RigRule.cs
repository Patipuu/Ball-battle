
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

        char Fin => '-';   // BB-19c: the FINALE lives in the active skill now (Skills/ActiveSkill.cs)
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        bool reserveActive;
        FixVec2 reservePos;

        /// <summary>Turrets that fire (BB-12g: + the OPENER reserve turret until its first volley).</summary>
        public int TurretCount => count + (reserveActive ? 1 : 0);
        public FixVec2 GetTurret(int index) => turrets[index];
        internal void BeginOverclock() => timer = 0;
        /// <summary>BB-12g OPENER: one reserve turret at the start position, gone after its first volley.</summary>
        internal void ApplyTreeStart() { reservePos = Self.Pos; reserveActive = true; }

        /// <summary>Volley interval: INSTINCT -1 tick per rank, FINALE A x1.1 / B x0.9 (Run tree only), halved by OVERCLOCK.</summary>
        int Interval
        {
            get
            {
                if (Self.Tree == null) return Self.Skill != null && Self.Skill.Active ? WeaponTuning.RigFireIntervalTicks / 2 : WeaponTuning.RigFireIntervalTicks;
                var interval = WeaponTuning.RigFireIntervalTicks - Growth;
                if (Fin == 'A') interval = interval * 11 / 10;
                if (Fin == 'B') interval = interval * 9 / 10;
                if (Self.Skill != null && Self.Skill.Active) interval /= 2;
                return interval;
            }
        }
        /// <summary>FINALE A: shots x1.05 / B: x0.9.</summary>
        ProjectileSpec Shot
        {
            get
            {
                var spec = shot;
                if (Fin == 'A') spec.Damage *= Fix.Ratio(21, 20);
                else if (Fin == 'B') spec.Damage *= Fix.Ratio(87, 100);   // BB-12g tuning 1: 0.90 -> 0.87 (HIVE best build 73.5%)
                return spec;
            }
        }
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
            var spec = Shot;
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
            if (reserveActive)
            {
                var a = Sim.Arena;
                var m = WeaponTuning.RigTurretRadius;
                if (reservePos.X < a.Left + m || reservePos.X > a.Right - m || reservePos.Y < a.Bottom + m || reservePos.Y > a.Top - m)
                    reserveActive = false;
            }
            if (TurretCount == 0) { timer = 0; return; }
            var interval = Interval;
            if (++timer < interval) return;
            timer = 0;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            var spec = Self.Tree == null ? shot : Shot;
            for (var i = 0; i < count; i++)
            {
                var dir = (foe.Pos - turrets[i]).NormalizedOr(FixVec2.Right);
                Sim.FireProjectile(Self, turrets[i] + dir * (WeaponTuning.RigTurretRadius + 1), dir * WeaponTuning.RigShotSpeed, spec);
            }
            if (reserveActive)
            {
                var dir = (foe.Pos - reservePos).NormalizedOr(FixVec2.Right);
                Sim.FireProjectile(Self, reservePos + dir * (WeaponTuning.RigTurretRadius + 1), dir * WeaponTuning.RigShotSpeed, spec);
                reserveActive = false;
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
            if (Self?.Tree != null)
            {
                h = SimHash.Mix(h, reserveActive ? 1 : 0);
                if (reserveActive) h = SimHash.Mix(h, reservePos);
            }
            return h;
        }
    }
}
