using System;

namespace BallBattle.Sim
{
    /// <summary>Arena corners that can carry spike zones (flags).</summary>
    [Flags]
    public enum SpikeCorner : byte
    {
        None = 0,
        TopLeft = 1,
        TopRight = 2,
        BottomLeft = 4,
        BottomRight = 8,
        All = TopLeft | TopRight | BottomLeft | BottomRight
    }

    public enum ArenaWall : byte { Left, Right, Bottom, Top }

    /// <summary>
    /// Spike zones of the spike-walls arena: from each spiked corner, ArenaTuning.SpikeZoneNum/SpikeZoneDen of the CURRENT
    /// wall length along both walls meeting there. Integer fraction, compared by cross-multiplying (no division).
    /// </summary>
    public static class SpikeZones
    {
        /// <summary>
        /// Is a wall contact inside a spike zone? <paramref name="along"/> = contact coordinate along that wall
        /// (Y for Left/Right, X for Bottom/Top), inside the current rect <paramref name="r"/>.
        /// </summary>
        public static bool Contains(in ArenaRect r, ArenaWall wall, float along, SpikeCorner corners)
        {
            const int num = ArenaTuning.SpikeZoneNum, den = ArenaTuning.SpikeZoneDen;
            var w = r.Width * num;
            var h = r.Height * num;
            switch (wall)
            {
                case ArenaWall.Left:
                    return (Has(corners, SpikeCorner.TopLeft) && (r.Top - along) * den <= h)
                        || (Has(corners, SpikeCorner.BottomLeft) && (along - r.Bottom) * den <= h);
                case ArenaWall.Right:
                    return (Has(corners, SpikeCorner.TopRight) && (r.Top - along) * den <= h)
                        || (Has(corners, SpikeCorner.BottomRight) && (along - r.Bottom) * den <= h);
                case ArenaWall.Top:
                    return (Has(corners, SpikeCorner.TopLeft) && (along - r.Left) * den <= w)
                        || (Has(corners, SpikeCorner.TopRight) && (r.Right - along) * den <= w);
                default:
                    return (Has(corners, SpikeCorner.BottomLeft) && (along - r.Left) * den <= w)
                        || (Has(corners, SpikeCorner.BottomRight) && (r.Right - along) * den <= w);
            }
        }

        public static bool Has(SpikeCorner set, SpikeCorner c) => (set & c) != 0;
    }

    public enum ObstacleShape : byte
    {
        /// <summary>Disc at A with Radius.</summary>
        Circle,
        /// <summary>Capsule from A to B, half-thickness Radius.</summary>
        Segment
    }

    /// <summary>Static obstacle inside the arena. Balls and projectiles bounce off it like a wall; blades pass over it.</summary>
    public readonly struct Obstacle
    {
        public readonly ObstacleShape Shape;
        public readonly Vec2 A;
        public readonly Vec2 B;
        public readonly float Radius;
        /// <summary>Damage to a ball on each bounce (spikes). 0 = harmless.</summary>
        public readonly float Damage;
        /// <summary>Fraction of speed added on each bounce (bumper). The speed cap still applies.</summary>
        public readonly float Boost;

        public Obstacle(ObstacleShape shape, Vec2 a, Vec2 b, float radius, float damage = 0f, float boost = 0f)
        {
            Shape = shape;
            A = a;
            B = b;
            Radius = radius;
            Damage = damage;
            Boost = boost;
        }

        public static Obstacle Circle(Vec2 center, float radius, float damage = 0f, float boost = 0f)
            => new Obstacle(ObstacleShape.Circle, center, center, radius, damage, boost);

        public static Obstacle Segment(Vec2 a, Vec2 b, float halfThickness, float damage = 0f, float boost = 0f)
            => new Obstacle(ObstacleShape.Segment, a, b, halfThickness, damage, boost);

        /// <summary>Closest point of the obstacle's core (center or segment) to p.</summary>
        public Vec2 ClosestCore(Vec2 p) => Shape == ObstacleShape.Circle ? A : Geometry.ClosestPointOnSegment(A, B, p);

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, (int)Shape);
            h = SimHash.Mix(h, A);
            h = SimHash.Mix(h, B);
            h = SimHash.Mix(h, Radius);
            h = SimHash.Mix(h, Damage);
            return SimHash.Mix(h, Boost);
        }
    }

    /// <summary>
    /// Arena contents beyond the (shrinking) rectangle from MatchConfig: static obstacles and wall hazards.
    /// Immutable once built, so one layout can be shared by many matches.
    /// </summary>
    public sealed class ArenaLayout
    {
        public static readonly ArenaLayout Empty = new ArenaLayout(Array.Empty<Obstacle>());

        readonly Obstacle[] obstacles;
        public int ObstacleCount => obstacles.Length;

        /// <summary>Smallest obstacle radius / half-thickness (float.MaxValue when there are none).</summary>
        public readonly float MinObstacleRadius = float.MaxValue;

        /// <summary>Damage to a ball bouncing off an arena wall inside a spike zone (spike walls). 0 = none.</summary>
        public readonly float WallDamage;

        /// <summary>Corners with spike zones (SpikeZones); only used when WallDamage &gt; 0.</summary>
        public readonly SpikeCorner SpikeCorners;

        public ArenaLayout(Obstacle[] obstacles, float wallDamage = 0f, SpikeCorner spikeCorners = ArenaTuning.SpikeCorners)
        {
            this.obstacles = obstacles != null ? (Obstacle[])obstacles.Clone() : Array.Empty<Obstacle>();
            WallDamage = wallDamage;
            SpikeCorners = spikeCorners;
            foreach (var o in this.obstacles)
                if (o.Radius < MinObstacleRadius) MinObstacleRadius = o.Radius;
        }

        public Obstacle Get(int index) => obstacles[index];

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, WallDamage);
            if (WallDamage > 0f) h = SimHash.Mix(h, (int)SpikeCorners);   // only spike layouts: every other layout hash is unchanged
            h = SimHash.Mix(h, obstacles.Length);
            for (var i = 0; i < obstacles.Length; i++) h = obstacles[i].HashInto(h);
            return h;
        }
    }
}
