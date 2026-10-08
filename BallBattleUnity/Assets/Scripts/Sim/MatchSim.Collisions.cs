using System;

namespace BallBattle.Sim
{
    /// <summary>Contact resolution: blade-blade parry, blade/body hits, ball-ball bounce, walls.</summary>
    public sealed partial class MatchSim
    {
        static readonly Vec2 FallbackNormal = new Vec2(1f, 0f);

        void ResolveWeaponContacts()
        {
            var n = balls.Length;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var a = balls[i];
                    var b = balls[j];
                    if (!a.Alive || !b.Alive) continue;

                    // Touching blades block each other: no hits for this pair while in contact.
                    if (BladesBlocked(a, b)) continue;

                    // Detect both directions before applying either, so a trade is symmetric:
                    // ball order never decides who strikes first, and a double knockout is a draw.
                    var abHit = DetectHit(a, b, out var abContact);
                    var baHit = DetectHit(b, a, out var baContact);
                    if (!abHit && !baHit) continue;

                    // Body vs body: only the faster ball lands the blow (equal speed: both), so mirror brawls are not mostly draws.
                    if (abHit && baHit && a.Weapon.BodyAttacks && b.Weapon.BodyAttacks && !a.Weapon.HasBlade && !b.Weapon.HasBlade)
                    {
                        var sa = a.Vel.LengthSq;
                        var sb = b.Vel.LengthSq;
                        if (sa > sb) baHit = false;
                        else if (sb > sa) abHit = false;
                    }

                    float abDamage = 0f, baDamage = 0f;
                    HitContext abCtx = default, baCtx = default;
                    if (abHit) { abCtx = MakeContext(a, b); abDamage = SafeDamage(a.Weapon, abCtx); }
                    if (baHit) { baCtx = MakeContext(b, a); baDamage = SafeDamage(b.Weapon, baCtx); }

                    if (abHit) ApplyHit(a, b, abDamage, abCtx, abContact);
                    if (baHit) ApplyHit(b, a, baDamage, baCtx, baContact);

                    if (abHit) KillIfDead(b, a);
                    if (baHit) KillIfDead(a, b);
                    if (!a.Alive || !b.Alive) EndIfDecided();
                    if (Outcome != MatchOutcome.Ongoing) return;
                }
            }
        }

        /// <summary>True while the two blades touch. Parry effects (spin flip, hitstop, push, event) fire only when the pair's parry cooldown is over.</summary>
        bool BladesBlocked(BallState a, BallState b)
        {
            if (!a.Weapon.HasBlade || !b.Weapon.HasBlade) return false;

            var distSq = Geometry.SegmentSegmentClosest(a.BladeStart, a.BladeEnd, b.BladeStart, b.BladeEnd, out var pa, out var pb);
            var reach = (a.Weapon.BladeThickness + b.Weapon.BladeThickness) * 0.5f;
            if (distSq > reach * reach) return false;
            if (parryCooldown[a.Index, b.Index] > 0) return true;

            a.SpinDir = -a.SpinDir;
            b.SpinDir = -b.SpinDir;
            parryCooldown[a.Index, b.Index] = Config.ParryCooldownTicks;
            parryCooldown[b.Index, a.Index] = Config.ParryCooldownTicks;
            HitstopRemaining = Math.Max(HitstopRemaining, Config.ParryHitstopTicks);

            // Push the balls apart so the blades do not stay locked together.
            var normal = (b.Pos - a.Pos).NormalizedOr(FallbackNormal);
            a.Vel -= normal * Config.ParryPush;
            b.Vel += normal * Config.ParryPush;

            a.Weapon.OnParry();
            b.Weapon.OnParry();
            Emit(SimEventType.Parry, a.Index, b.Index, 0f, (pa + pb) * 0.5f);
            return true;
        }

        /// <summary>Does attacker's blade (or body) touch target right now and is it off cooldown? No side effects.</summary>
        bool DetectHit(BallState attacker, BallState target, out Vec2 contact)
        {
            contact = default;
            if (attacker.HitCooldown[target.Index] > 0) return false;

            var w = attacker.Weapon;
            if (w.HasBlade)
            {
                var closest = Geometry.ClosestPointOnSegment(attacker.BladeStart, attacker.BladeEnd, target.Pos);
                var reach = target.Radius + w.BladeThickness * 0.5f;
                if ((closest - target.Pos).LengthSq > reach * reach) return false;
                contact = closest;
                return true;
            }

            if (w.BodyAttacks)
            {
                var delta = target.Pos - attacker.Pos;
                var reach = attacker.Radius + target.Radius + Config.BodyContactSlop;
                if (delta.LengthSq > reach * reach) return false;
                contact = attacker.Pos + delta.NormalizedOr(FallbackNormal) * attacker.Radius;
                return true;
            }

            return false;
        }

        static HitContext MakeContext(BallState attacker, BallState target)
            => new HitContext(attacker.Vel.Length, (attacker.Vel - target.Vel).Length);

        static float SafeDamage(WeaponRule w, in HitContext ctx)
        {
            var d = w.Damage(ctx);
            return d > 0f ? d : 0f;
        }

        void ApplyHit(BallState attacker, BallState target, float damage, in HitContext ctx, Vec2 contact)
        {
            target.Hp -= damage;
            attacker.HitCount++;
            var cooldown = attacker.Weapon.HitCooldownTicks >= 0 ? attacker.Weapon.HitCooldownTicks : Config.HitCooldownTicks;
            attacker.HitCooldown[target.Index] = cooldown;
            attacker.Weapon.OnHit(ctx);
            HitstopRemaining = Math.Max(HitstopRemaining, Config.HitHitstopTicks);

            // Push away from the contact point (a long blade tip pushes along the swing, not center-to-center).
            var centerDir = (target.Pos - attacker.Pos).NormalizedOr(FallbackNormal);
            var knock = (target.Pos - contact).NormalizedOr(centerDir);
            target.Vel += knock * (Config.HitKnockback * attacker.Weapon.KnockbackScale);

            Emit(SimEventType.Hit, attacker.Index, target.Index, damage, contact);
            Emit(SimEventType.StatChanged, attacker.Index, -1, 0f, attacker.Pos);
        }

        void KillIfDead(BallState target, BallState killer)
        {
            if (!target.Alive || target.Hp > 0f) return;
            target.Hp = 0f;
            target.Alive = false;
            Emit(SimEventType.Death, target.Index, killer.Index, 0f, target.Pos);
        }

        /// <summary>Equal-mass elastic collision: swap velocity components along the contact normal.</summary>
        void ResolveBallCollisions()
        {
            var n = balls.Length;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var a = balls[i];
                    var b = balls[j];
                    if (!a.Alive || !b.Alive) continue;

                    var delta = b.Pos - a.Pos;
                    var minDist = a.Radius + b.Radius;
                    var distSq = delta.LengthSq;
                    if (distSq >= minDist * minDist) continue;

                    var dist = MathF.Sqrt(distSq);
                    var normal = dist > 1e-6f ? delta * (1f / dist) : FallbackNormal;
                    var half = (minDist - dist) * 0.5f;
                    a.Pos -= normal * half;
                    b.Pos += normal * half;

                    var closing = Vec2.Dot(b.Vel - a.Vel, normal);
                    if (closing < 0f)
                    {
                        a.Vel += normal * closing;
                        b.Vel -= normal * closing;
                        Emit(SimEventType.BallBounce, a.Index, b.Index, -closing, a.Pos + normal * a.Radius);
                    }
                }
            }
        }

        /// <summary>Clamp every ball inside the (possibly shrinking) arena. A bounce (event + OnWall) counts only when the ball was moving into the wall, so a wall sliding onto a ball does not spam it.</summary>
        void ResolveWalls()
        {
            var arena = Arena;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var r = b.Radius;
                var bounced = false;

                if (b.Pos.X - r < arena.Left)
                {
                    b.Pos.X = arena.Left + r;
                    if (b.Vel.X < 0f) { b.Vel.X = -b.Vel.X; bounced = true; }
                }
                else if (b.Pos.X + r > arena.Right)
                {
                    b.Pos.X = arena.Right - r;
                    if (b.Vel.X > 0f) { b.Vel.X = -b.Vel.X; bounced = true; }
                }

                if (b.Pos.Y - r < arena.Bottom)
                {
                    b.Pos.Y = arena.Bottom + r;
                    if (b.Vel.Y < 0f) { b.Vel.Y = -b.Vel.Y; bounced = true; }
                    if (b.Vel.Y < Config.MinFloorBounce) b.Vel.Y = Config.MinFloorBounce;
                }
                else if (b.Pos.Y + r > arena.Top)
                {
                    b.Pos.Y = arena.Top - r;
                    if (b.Vel.Y > 0f) { b.Vel.Y = -b.Vel.Y; bounced = true; }
                }

                if (bounced)
                {
                    if (b.Weapon.WallSpeedBoost > 0f) b.Vel = b.Vel * (1f + b.Weapon.WallSpeedBoost);
                    b.Weapon.OnWall();
                    Emit(SimEventType.WallBounce, b.Index, -1, 0f, b.Pos);
                }
            }
        }
    }
}
