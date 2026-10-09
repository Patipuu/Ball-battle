using System;

namespace BallBattle.Sim
{
    /// <summary>
    /// Projectiles: move each substep; a non-owner blade deflects them (the shot then belongs to the deflector,
    /// so the deflector's damage bonus and traits apply to it); a non-owner ball takes a Projectile hit through
    /// the damage pipeline; walls and obstacles bounce them while bounces last. Shots vanish when their owner dies.
    /// Shots fired mid-tick (by hit hooks) are not in that tick's substep budget; keep such shots slow.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>Fires a projectile owned by <paramref name="owner"/>. Returns the slot, or -1 (match over, owner down or pool full).</summary>
        public int FireProjectile(BallState owner, Vec2 pos, Vec2 vel, in ProjectileSpec spec)
        {
            if (!Ongoing || !Active(owner)) return -1;
            var slot = projectiles.Spawn(owner.Index, pos, vel, spec);
            if (slot >= 0) Emit(SimEventType.ProjectileFired, owner.Index, slot, 0f, pos);
            return slot;
        }

        /// <summary>Substeps needed so no projectile moves farther than its radius plus the thinnest blade/obstacle half-width.</summary>
        int ProjectileSubsteps(float minBladeThickness)
        {
            if (projectiles.ActiveCount == 0) return 0;
            var halfBlade = minBladeThickness < float.MaxValue ? minBladeThickness * 0.5f : float.MaxValue;
            var margin = MathF.Min(halfBlade, layout.MinObstacleRadius);
            if (margin == float.MaxValue) margin = 0f; // nothing thin to cross: budget by the shot's own radius
            var n = 0;
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                ref readonly var p = ref projectiles.Get(i);
                if (!p.Active) continue;
                var step = MathF.Max(p.Radius + margin, 0.5f);
                var need = (int)MathF.Ceiling(p.Vel.Length / step);
                if (need > n) n = need;
            }
            return n;
        }

        void IntegrateProjectiles(float dt)
        {
            if (projectiles.ActiveCount == 0) return;
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                ref var p = ref projectiles.At(i);
                if (!p.Active) continue;
                p.Vel.Y -= Config.Gravity * p.GravityScale * dt;
                p.Pos += p.Vel * dt;
            }
        }

        void ResolveProjectileContacts()
        {
            if (!Ongoing || projectiles.ActiveCount == 0) return;
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                ref var p = ref projectiles.At(i);
                if (!p.Active) continue;
                if (TryDeflect(ref p, i)) continue; // a blade in the way protects its ball
                HitBalls(ref p, i);
                if (p.Active) BounceProjectile(ref p, i);
            }
            ResolveDeaths();
        }

        bool TryDeflect(ref Projectile p, int slot)
        {
            foreach (var b in balls)
            {
                if (!Active(b) || b.Index == p.Owner || !b.Weapon.HasBlade) continue;
                var closest = Geometry.ClosestPointOnSegment(b.BladeStart, b.BladeEnd, p.Pos);
                var reach = p.Radius + b.Weapon.BladeThickness * 0.5f;
                var delta = p.Pos - closest;
                if (delta.LengthSq > reach * reach) continue;

                var normal = delta.NormalizedOr((-p.Vel).NormalizedOr(FallbackNormal));
                var into = Vec2.Dot(p.Vel, normal);
                if (into < 0f) p.Vel -= normal * (2f * into);
                p.Pos = closest + normal * reach;
                p.Owner = b.Index;
                p.HitMask = 0;
                Emit(SimEventType.ProjectileDeflected, b.Index, slot, 0f, closest);
                b.Weapon.OnDeflect(slot);
                foreach (var t in b.Traits) t.OnDeflect(slot);
                return true;
            }
            return false;
        }

        void HitBalls(ref Projectile p, int slot)
        {
            var owner = balls[p.Owner];
            foreach (var b in balls)
            {
                // A pending-dead ball is skipped: shots fly through it for the rest of this pass.
                if (!Active(b) || b.Index == p.Owner || (p.HitMask & (1 << b.Index)) != 0) continue;
                var delta = p.Pos - b.Pos;
                var reach = b.Radius + p.Radius;
                if (delta.LengthSq > reach * reach) continue;

                var contact = b.Pos + delta.NormalizedOr(FallbackNormal) * b.Radius;
                var damage = ComputeHitDamage(owner, b, p.Damage > 0f ? p.Damage : 0f, DamageKind.Projectile, out var blocked);
                b.Vel += p.Vel.NormalizedOr(FallbackNormal) * (Config.HitKnockback * Config.ProjectileKnockbackScale * (1f - b.KnockbackResist));
                Emit(SimEventType.ProjectileHit, owner.Index, b.Index, damage, contact);
                if (blocked)
                {
                    Emit(SimEventType.ShieldBlocked, b.Index, owner.Index, 0f, contact);
                }
                else
                {
                    ApplyHitHp(owner, b, damage);
                    RunHitHooks(owner, b, damage, DamageKind.Projectile);
                }

                p.HitMask |= 1 << b.Index;
                if (--p.HitsLeft <= 0)
                {
                    projectiles.Remove(slot);
                    return;
                }
            }
        }

        /// <summary>
        /// Walls and obstacles: reflect while bounces last, otherwise remove. A bounce is spent only when the
        /// shot moves into the surface (a wall sliding onto it just pushes it), at most one per substep.
        /// </summary>
        void BounceProjectile(ref Projectile p, int slot)
        {
            var bounced = false;
            var a = Arena;
            var r = p.Radius;
            if (p.Pos.X - r < a.Left) { p.Pos.X = a.Left + r; if (p.Vel.X < 0f) { p.Vel.X = -p.Vel.X; bounced = true; } }
            else if (p.Pos.X + r > a.Right) { p.Pos.X = a.Right - r; if (p.Vel.X > 0f) { p.Vel.X = -p.Vel.X; bounced = true; } }
            if (p.Pos.Y - r < a.Bottom) { p.Pos.Y = a.Bottom + r; if (p.Vel.Y < 0f) { p.Vel.Y = -p.Vel.Y; bounced = true; } }
            else if (p.Pos.Y + r > a.Top) { p.Pos.Y = a.Top - r; if (p.Vel.Y > 0f) { p.Vel.Y = -p.Vel.Y; bounced = true; } }

            for (var k = 0; k < layout.ObstacleCount; k++)
            {
                var o = layout.Get(k);
                var core = o.ClosestCore(p.Pos);
                var delta = p.Pos - core;
                var minDist = r + o.Radius;
                if (delta.LengthSq >= minDist * minDist) continue;
                var normal = delta.NormalizedOr(FallbackNormal);
                p.Pos = core + normal * minDist;
                var into = Vec2.Dot(p.Vel, normal);
                if (into >= 0f) continue;
                p.Vel -= normal * (2f * into);
                bounced = true;
            }

            if (!bounced) return;
            if (p.BouncesLeft > 0) p.BouncesLeft--;
            else projectiles.Remove(slot);
        }

        void TickProjectileLifetimes()
        {
            if (!Ongoing || projectiles.ActiveCount == 0) return;
            for (var i = 0; i < ProjectilePool.Capacity; i++)
            {
                ref var p = ref projectiles.At(i);
                if (p.Active && --p.TicksLeft <= 0) projectiles.Remove(i);
            }
        }
    }
}
