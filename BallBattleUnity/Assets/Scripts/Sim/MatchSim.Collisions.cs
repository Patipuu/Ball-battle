using System;

namespace BallBattle.Sim
{
    /// <summary>Contact resolution: blade-blade parry, blade/body hits, ball-ball bounce.</summary>
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
                    if (!Ongoing) return;
                    var a = balls[i];
                    var b = balls[j];
                    if (!a.Alive || !b.Alive) continue;

                    // Touching blades block each other: no hits for this pair while in contact.
                    if (BladesBlocked(a, b)) { ResolveDeaths(); continue; }

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
                    bool abBlocked = false, baBlocked = false;
                    HitContext abCtx = default, baCtx = default;
                    if (abHit) { abCtx = MakeContext(a, b); abDamage = ComputeHitDamage(a, b, SafeDamage(a.Weapon, abCtx), DamageKind.Weapon, out abBlocked); }
                    if (baHit) { baCtx = MakeContext(b, a); baDamage = ComputeHitDamage(b, a, SafeDamage(b.Weapon, baCtx), DamageKind.Weapon, out baBlocked); }

                    // Both HP changes first, then both sets of hooks: a heal-on-hit cannot outrun the other ball's blow.
                    if (abHit) ApplyHit(a, b, abDamage, abBlocked, abCtx, abContact);
                    if (baHit) ApplyHit(b, a, baDamage, baBlocked, baCtx, baContact);
                    if (abHit && !abBlocked) RunHitHooks(a, b, abDamage, DamageKind.Weapon);
                    if (baHit && !baBlocked) RunHitHooks(b, a, baDamage, DamageKind.Weapon);
                    ResolveDeaths();
                }
            }
        }

        /// <summary>True while the two blades touch. Parry effects (spin flip, hitstop, push, hooks, event) fire only when the pair's parry cooldown is over.</summary>
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

            a.Weapon.OnParry(b);
            b.Weapon.OnParry(a);
            foreach (var t in a.Traits) t.OnParry(b);
            foreach (var t in b.Traits) t.OnParry(a);
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

        /// <summary>
        /// Cooldown, hitstop and knockback happen either way. A shield-blocked hit deals no damage, does not
        /// count for the weapon's growth (no OnHit) and emits ShieldBlocked instead of Hit. Hit hooks run later.
        /// </summary>
        void ApplyHit(BallState attacker, BallState target, float damage, bool blocked, in HitContext ctx, Vec2 contact)
        {
            var cooldown = attacker.Weapon.HitCooldownTicks >= 0 ? attacker.Weapon.HitCooldownTicks : Config.HitCooldownTicks;
            attacker.HitCooldown[target.Index] = cooldown;
            HitstopRemaining = Math.Max(HitstopRemaining, Config.HitHitstopTicks);

            // Push away from the contact point (a long blade tip pushes along the swing, not center-to-center).
            var centerDir = (target.Pos - attacker.Pos).NormalizedOr(FallbackNormal);
            var knock = (target.Pos - contact).NormalizedOr(centerDir);
            target.Vel += knock * (Config.HitKnockback * attacker.Weapon.KnockbackScale);

            if (blocked)
            {
                Emit(SimEventType.ShieldBlocked, target.Index, attacker.Index, 0f, contact);
                return;
            }

            attacker.HitCount++;
            attacker.Weapon.OnHit(ctx);
            Emit(SimEventType.Hit, attacker.Index, target.Index, damage, contact);
            Emit(SimEventType.StatChanged, attacker.Index, -1, 0f, attacker.Pos);
            ApplyHitHp(attacker, target, damage);
        }

        /// <summary>Elastic collision weighted by mass (∝ radius²): equal sizes swap normal velocity components.</summary>
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
                    var ma = a.Radius * a.Radius;
                    var mb = b.Radius * b.Radius;
                    var wa = mb / (ma + mb); // share of the correction/impulse taken by a (lighter moves more)
                    var wb = ma / (ma + mb);
                    var overlap = minDist - dist;
                    a.Pos -= normal * (overlap * wa);
                    b.Pos += normal * (overlap * wb);

                    var closing = Vec2.Dot(b.Vel - a.Vel, normal);
                    if (closing < 0f)
                    {
                        a.Vel += normal * (closing * 2f * wa);
                        b.Vel -= normal * (closing * 2f * wb);
                        Emit(SimEventType.BallBounce, a.Index, b.Index, -closing, a.Pos + normal * a.Radius);
                    }
                }
            }
        }
    }
}
