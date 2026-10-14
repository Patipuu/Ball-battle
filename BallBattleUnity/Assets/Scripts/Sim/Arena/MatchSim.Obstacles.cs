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
                    if (distSq >= FixMath.Sq(minDist)) continue;

                    var dist = FixMath.Sqrt(distSq);
                    var normal = dist.Raw > 0 ? new FixVec2(delta.X / dist, delta.Y / dist) : FallbackNormal;
                    b.Pos = core + normal * minDist;

                    var into = FixVec2.DotFix(b.Vel, normal);
                    if (into >= Fix.Zero) continue;
                    b.Vel -= normal * (into * 2);
                    if (!Active(b)) continue; // killed by a spike this substep: still pushed out, but no more damage or hooks
                    if (o.Boost > Fix.Zero) b.Vel = b.Vel * (Fix.One + o.Boost);
                    var dealt = o.Damage > Fix.Zero ? DealDamage(null, b, o.Damage, DamageKind.Hazard, false) : Fix.Zero;
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
                    if (b.Vel.X < Fix.Zero) { b.Vel.X = -b.Vel.X; bounced = true; xWall = ArenaWall.Left; }
                }
                else if (b.Pos.X + r > arena.Right)
                {
                    b.Pos.X = arena.Right - r;
                    if (b.Vel.X > Fix.Zero) { b.Vel.X = -b.Vel.X; bounced = true; xWall = ArenaWall.Right; }
                }

                if (b.Pos.Y - r < arena.Bottom)
                {
                    b.Pos.Y = arena.Bottom + r;
                    if (b.Vel.Y < Fix.Zero) { b.Vel.Y = -b.Vel.Y; bounced = true; yWall = ArenaWall.Bottom; }
                    if (b.Vel.Y < Config.MinFloorBounce) b.Vel.Y = Config.MinFloorBounce;
                }
                else if (b.Pos.Y + r > arena.Top)
                {
                    b.Pos.Y = arena.Top - r;
                    if (b.Vel.Y > Fix.Zero) { b.Vel.Y = -b.Vel.Y; bounced = true; yWall = ArenaWall.Top; }
                }

                if (!bounced || !Active(b)) continue;
                if (b.Weapon.WallSpeedBoost > Fix.Zero) b.Vel = b.Vel * (Fix.One + b.Weapon.WallSpeedBoost);
                var dealt = Fix.Zero;
                if (wallDamage > Fix.Zero && spikeTick[b.Index] != Tick && HitsSpikeZone(arena, b.Pos, xWall, yWall))
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
        bool HitsSpikeZone(in ArenaRect arena, FixVec2 pos, ArenaWall? xWall, ArenaWall? yWall)
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
