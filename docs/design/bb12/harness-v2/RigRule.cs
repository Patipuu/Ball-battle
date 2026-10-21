
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
        char keystone;
        bool reserveActive;
        FixVec2 reservePos;

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

        public int TurretCount => count + (reserveActive ? 1 : 0);
        internal void ApplyTreeStart() { reservePos = Self.Pos; reserveActive = true; }
        internal void ApplyTreeKeystone() => keystone = TreeLabParams.Keystone;
        public FixVec2 GetTurret(int index) => turrets[index];
        internal void BeginOverclock() => timer = 0;

        public override Fix Damage(in HitContext ctx) => damage;

        public override void OnHit(in HitContext ctx)
        {
            damage += WeaponTuning.RigDamagePerHit;
            PlaceTurret();
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
            var interval = WeaponTuning.RigFireIntervalTicks - (TreeLabParams.Player(Self) ? TreeLabParams.GrowthRank : 0);
            if (keystone == 'A') interval = interval * 11 / 10;
            if (keystone == 'B') interval = interval * 9 / 10;
            if (Self.Skill != null && Self.Skill.Active) interval /= 2;
            if (++timer < interval) return;
            timer = 0;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            var spec = shot;
            spec.Damage *= keystone == 'A' ? Fix.Ratio(11, 10) :
                keystone == 'B' ? Fix.Ratio(9, 10) : Fix.One;
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
            h = SimHash.Mix(h, reserveActive ? 1 : 0);
            if (reserveActive) h = SimHash.Mix(h, reservePos);
            for (var i = 0; i < turrets.Length; i++) h = SimHash.Mix(h, turrets[i]);
            return h;
        }
    }
}
