using System;

namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// Engineer: every melee hit drops a turret where the ball stands (max 6, the oldest is replaced). Turrets do not
    /// block movement; all of them shoot at the nearest foe together. A turret the shrinking arena reaches is lost.
    /// </summary>
    public sealed class RigRule : WeaponRule
    {
        public const string WeaponId = "rig";
        readonly Vec2[] turrets = new Vec2[WeaponTuning.RigMaxTurrets];
        readonly Vec2[] scratch = new Vec2[WeaponTuning.RigMaxTurrets];
        readonly ProjectileSpec shot;
        float damage = WeaponTuning.RigMeleeDamage;
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
        public override float StatValue => count;

        public int TurretCount => count;
        public Vec2 GetTurret(int index) => turrets[index];

        public override float Damage(in HitContext ctx) => damage;

        public override void OnHit(in HitContext ctx)
        {
            damage += WeaponTuning.RigDamagePerHit;
            if (count < turrets.Length) { turrets[count++] = Self.Pos; return; }
            turrets[next] = Self.Pos;
            next = (next + 1) % turrets.Length;
        }

        public override void OnTick()
        {
            DropTurretsOutsideArena();
            if (count == 0) { timer = 0; return; }
            if (++timer < WeaponTuning.RigFireIntervalTicks) return;
            timer = 0;
            var foe = WeaponAim.NearestFoe(Sim, Self);
            if (foe == null) return;
            for (var i = 0; i < count; i++)
            {
                var dir = (foe.Pos - turrets[i]).NormalizedOr(new Vec2(1f, 0f));
                Sim.FireProjectile(Self, turrets[i] + dir * (WeaponTuning.RigTurretRadius + 1f), dir * WeaponTuning.RigShotSpeed, shot);
            }
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