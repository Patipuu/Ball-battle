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

        /// <summary>
        /// Clamp every ball inside the arena. A bounce (event + OnWall) counts only when the ball was moving into the wall, so a wall
        /// sliding onto a ball does not spam it. Spike walls: damage only when a wall it bounced off is touched inside a spike zone
        /// (SpikeZones, from the current rect); a corner touching both spiked walls is one hit; at most one spike hit per ball per tick.
        /// </summary>
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
                ArenaWall? xWall = null, yWall = null;

                if (b.Pos.X - r < arena.Left)
                {
                    b.Pos.X = arena.Left + r;
                    if (b.Vel.X < 0f) { b.Vel.X = -b.Vel.X; bounced = true; xWall = ArenaWall.Left; }
                }
                else if (b.Pos.X + r > arena.Right)
                {
                    b.Pos.X = arena.Right - r;
                    if (b.Vel.X > 0f) { b.Vel.X = -b.Vel.X; bounced = true; xWall = ArenaWall.Right; }
                }

                if (b.Pos.Y - r < arena.Bottom)
                {
                    b.Pos.Y = arena.Bottom + r;
                    if (b.Vel.Y < 0f) { b.Vel.Y = -b.Vel.Y; bounced = true; yWall = ArenaWall.Bottom; }
                    if (b.Vel.Y < Config.MinFloorBounce) b.Vel.Y = Config.MinFloorBounce;
                }
                else if (b.Pos.Y + r > arena.Top)
                {
                    b.Pos.Y = arena.Top - r;
                    if (b.Vel.Y > 0f) { b.Vel.Y = -b.Vel.Y; bounced = true; yWall = ArenaWall.Top; }
                }

                if (!bounced || !Active(b)) continue;
                if (b.Weapon.WallSpeedBoost > 0f) b.Vel = b.Vel * (1f + b.Weapon.WallSpeedBoost);
                var dealt = 0f;
                if (wallDamage > 0f && spikeTick[b.Index] != Tick && HitsSpikeZone(arena, b.Pos, xWall, yWall))
                {
                    spikeTick[b.Index] = Tick;
                    dealt = DealDamage(null, b, wallDamage, DamageKind.Hazard, false);
                }
                Emit(SimEventType.WallBounce, b.Index, -1, dealt, b.Pos);
                if (Active(b)) OnBallBounced(b);
            }
            ResolveDeaths();
        }

        /// <summary>Tick of each ball's last spike hit (max one per ball per tick); -1 = none yet. Not part of the state hash.</summary>
        int[] spikeTick;

        /// <summary>Contact points on the walls bounced off (ball centre projected on the wall) inside a spike zone?</summary>
        bool HitsSpikeZone(in ArenaRect arena, Vec2 pos, ArenaWall? xWall, ArenaWall? yWall)
        {
            var corners = layout.SpikeCorners;
            return (xWall.HasValue && SpikeZones.Contains(arena, xWall.Value, pos.Y, corners))
                || (yWall.HasValue && SpikeZones.Contains(arena, yWall.Value, pos.X, corners));
        }

        /// <summary>Weapon then trait OnWall: obstacles count as walls (Brawler gains from pillars too).</summary>
        void OnBallBounced(BallState b)
        {
            b.Weapon.OnWall();
            foreach (var t in b.Traits) t.OnWall();
        }
    }
}
