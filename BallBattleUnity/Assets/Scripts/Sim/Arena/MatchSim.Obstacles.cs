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
                    if (!ObstacleActive(k)) continue;   // BB-04: switched off by a Warden step
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
        /// (SpikeZones, from the current rect); a corner touching both spiked walls is one hit; after a spike hit the ball is immune to
        /// spikes for ArenaTuning.SpikeImmunityTicks active ticks (so also at most one per tick).
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
                // Spike zone and (BB-6d) WARDEN's spiked walls: one hit per contact at the higher damage, one shared immunity.
                var zone = wallDamage > Fix.Zero && HitsSpikeZone(arena, b.Pos, xWall, yWall) ? wallDamage : Fix.Zero;
                // BB-6f: check each bounced wall against the last WARDEN step's active window/moved-wall mask.
                var boss = Fix.Zero;
                byte bossWalls = 0;
                if (bossRules.Length > 0)
                {
                    if (xWall.HasValue)
                    {
                        boss = BossWallSpike(b, xWall.Value);
                        if (boss > Fix.Zero) bossWalls |= (byte)(1 << (int)xWall.Value);
                    }
                    if (yWall.HasValue)
                    {
                        var yDamage = BossWallSpike(b, yWall.Value);
                        boss = FixMath.Max(boss, yDamage);
                        if (yDamage > Fix.Zero) bossWalls |= (byte)(1 << (int)yWall.Value);
                    }
                }
                var spike = boss > zone ? boss : zone;
                // BB-12g WRECKER FINALE A takes x0.85 spike damage, B x1.15 (Run tree only).
                if (b.Tree != null && b.Weapon is Weapons.BrawlerRule && b.Tree.Finale != '-')
                    spike *= b.Tree.Finale == 'A' ? Fix.Ratio(17, 20) : Fix.Ratio(23, 20);
                if (spike > Fix.Zero && !SpikeImmune(b.Index))
                {
                    spikeActiveTick[b.Index] = ActiveTick;
                    dealt = DealDamage(null, b, spike, DamageKind.Hazard, false);
                }
                // WARDEN-only source metadata for diagnostics: exact contact walls, one damage source even at corners.
                var variant = dealt > Fix.Zero && boss > Fix.Zero && boss >= zone ? (byte)(128 | bossWalls) : (byte)0;
                Emit(SimEventType.WallBounce, b.Index, -1, dealt, b.Pos, variant);
                if (Active(b)) OnBallBounced(b);
            }
            ResolveDeaths();
        }

        /// <summary>ActiveTick of each ball's last spike hit; -1 = none yet. Part of the state hash (it carries over ticks).</summary>
        int[] spikeActiveTick;

        /// <summary>True while ball <paramref name="index"/> is within SpikeImmunityTicks active ticks of its last spike hit.</summary>
        internal bool SpikeImmune(int index) =>
            spikeActiveTick[index] >= 0 && ActiveTick - spikeActiveTick[index] < ArenaTuning.SpikeImmunityTicks;

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
