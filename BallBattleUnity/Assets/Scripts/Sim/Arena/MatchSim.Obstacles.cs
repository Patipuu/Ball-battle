using System;

namespace BallBattle.Sim
{
    /// <summary>Ball vs static obstacles and the (possibly shrinking) arena walls.</summary>
    public sealed partial class MatchSim
    {
        /// <summary>Push balls out of obstacles. A bounce (boost, damage, OnWall hooks, event) counts only when the ball was moving into it.</summary>
        void ResolveObstacles()
        {
            var count = layout.ObstacleCount;
            if (count == 0 || !Ongoing) return;

            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                for (var k = 0; k < count; k++)
                {
                    var o = layout.Get(k);
                    var core = o.ClosestCore(b.Pos);
                    var delta = b.Pos - core;
                    var minDist = b.Radius + o.Radius;
                    var distSq = delta.LengthSq;
                    if (distSq >= minDist * minDist) continue;

                    var dist = MathF.Sqrt(distSq);
                    var normal = dist > 1e-6f ? delta * (1f / dist) : FallbackNormal;
                    b.Pos = core + normal * minDist;

                    var into = Vec2.Dot(b.Vel, normal);
                    if (into >= 0f) continue;
                    b.Vel -= normal * (2f * into);
                    if (!Active(b)) continue; // killed by a spike this substep: still pushed out, but no more damage or hooks
                    if (o.Boost > 0f) b.Vel = b.Vel * (1f + o.Boost);
                    var dealt = o.Damage > 0f ? DealDamage(null, b, o.Damage, DamageKind.Hazard, false) : 0f;
                    Emit(SimEventType.ObstacleHit, b.Index, k, dealt, b.Pos - normal * b.Radius);
                    if (Active(b)) OnBallBounced(b);
                }
            }
            ResolveDeaths();
        }

        /// <summary>Clamp every ball inside the arena. A bounce (event + OnWall) counts only when the ball was moving into the wall, so a wall sliding onto a ball does not spam it.</summary>
        void ResolveWalls()
        {
            if (!Ongoing) return;
            var arena = Arena;
            var wallDamage = layout.WallDamage;
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

                if (!bounced || !Active(b)) continue;
                if (b.Weapon.WallSpeedBoost > 0f) b.Vel = b.Vel * (1f + b.Weapon.WallSpeedBoost);
                var dealt = wallDamage > 0f ? DealDamage(null, b, wallDamage, DamageKind.Hazard, false) : 0f;
                Emit(SimEventType.WallBounce, b.Index, -1, dealt, b.Pos);
                if (Active(b)) OnBallBounced(b);
            }
            ResolveDeaths();
        }

        /// <summary>Weapon then trait OnWall: obstacles count as walls (Brawler gains from pillars too).</summary>
        void OnBallBounced(BallState b)
        {
            b.Weapon.OnWall();
            foreach (var t in b.Traits) t.OnWall();
        }
    }
}
