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
        public static bool Contains(in ArenaRect r, ArenaWall wall, Fix along, SpikeCorner corners)
        {
            // Integer cross-multiply on raw Q16.16 in long: distance × Den <= length × Num (inclusive), no division.
            const int num = ArenaTuning.SpikeZoneNum, den = ArenaTuning.SpikeZoneDen;
            var w = (long)r.Width.Raw * num;
            var h = (long)r.Height.Raw * num;
            switch (wall)
            {
                case ArenaWall.Left:
                    return (Has(corners, SpikeCorner.TopLeft) && (long)(r.Top - along).Raw * den <= h)
                        || (Has(corners, SpikeCorner.BottomLeft) && (long)(along - r.Bottom).Raw * den <= h);
                case ArenaWall.Right:
                    return (Has(corners, SpikeCorner.TopRight) && (long)(r.Top - along).Raw * den <= h)
                        || (Has(corners, SpikeCorner.BottomRight) && (long)(along - r.Bottom).Raw * den <= h);
                case ArenaWall.Top:
                    return (Has(corners, SpikeCorner.TopLeft) && (long)(along - r.Left).Raw * den <= w)
                        || (Has(corners, SpikeCorner.TopRight) && (long)(r.Right - along).Raw * den <= w);
                default:
                    return (Has(corners, SpikeCorner.BottomLeft) && (long)(along - r.Left).Raw * den <= w)
                        || (Has(corners, SpikeCorner.BottomRight) && (long)(r.Right - along).Raw * den <= w);
            }
        }

        public static bool Has(SpikeCorner set, SpikeCorner c) => (set & c) != 0;

        /// <summary>Zone length along a wall: wall length × Num / Den, rounded toward zero (the view draws these).</summary>
        public static Fix ZoneLength(Fix wallLength) => FixMath.MulDiv(wallLength, ArenaTuning.SpikeZoneNum, ArenaTuning.SpikeZoneDen);

        /// <summary>
        /// The spike zones of rect <paramref name="r"/> as wall segments [From, To] (X for Top/Bottom, Y for Left/Right), in
        /// corner order TL, TR, BL, BR; two per spiked corner. The View draws exactly these (no separate math).
        /// </summary>
        public static SpikeSegment[] Segments(in ArenaRect r, SpikeCorner corners)
        {
            var zx = ZoneLength(r.Width);
            var zy = ZoneLength(r.Height);
            var n = 0;
            for (var c = 1; c <= 8; c <<= 1) if (Has(corners, (SpikeCorner)c)) n += 2;
            var list = new SpikeSegment[n];
            var i = 0;
            void Add(ArenaWall wall, Fix from, Fix to) => list[i++] = new SpikeSegment { Wall = wall, From = from, To = to };
            if (Has(corners, SpikeCorner.TopLeft)) { Add(ArenaWall.Top, r.Left, r.Left + zx); Add(ArenaWall.Left, r.Top - zy, r.Top); }
            if (Has(corners, SpikeCorner.TopRight)) { Add(ArenaWall.Top, r.Right - zx, r.Right); Add(ArenaWall.Right, r.Top - zy, r.Top); }
            if (Has(corners, SpikeCorner.BottomLeft)) { Add(ArenaWall.Bottom, r.Left, r.Left + zx); Add(ArenaWall.Left, r.Bottom, r.Bottom + zy); }
            if (Has(corners, SpikeCorner.BottomRight)) { Add(ArenaWall.Bottom, r.Right - zx, r.Right); Add(ArenaWall.Right, r.Bottom, r.Bottom + zy); }
            return list;
        }
    }

    /// <summary>One spike zone along one wall: [From, To] along it (X for Top/Bottom, Y for Left/Right).</summary>
    public struct SpikeSegment
    {
        public ArenaWall Wall;
        public Fix From;
        public Fix To;
        public Fix Length => To - From;
        public bool Horizontal => Wall == ArenaWall.Top || Wall == ArenaWall.Bottom;
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
        public readonly FixVec2 A;
        public readonly FixVec2 B;
        public readonly Fix Radius;
        /// <summary>Damage to a ball on each bounce (spikes). 0 = harmless.</summary>
        public readonly Fix Damage;
        /// <summary>Fraction of speed added on each bounce (bumper). The speed cap still applies.</summary>
        public readonly Fix Boost;

        public Obstacle(ObstacleShape shape, FixVec2 a, FixVec2 b, Fix radius, Fix damage = default, Fix boost = default)
        {
            Shape = shape;
            A = a;
            B = b;
            Radius = radius;
            Damage = damage;
            Boost = boost;
        }

        public static Obstacle Circle(FixVec2 center, Fix radius, Fix damage = default, Fix boost = default)
            => new Obstacle(ObstacleShape.Circle, center, center, radius, damage, boost);

        public static Obstacle Segment(FixVec2 a, FixVec2 b, Fix halfThickness, Fix damage = default, Fix boost = default)
            => new Obstacle(ObstacleShape.Segment, a, b, halfThickness, damage, boost);

        /// <summary>Closest point of the obstacle's core (center or segment) to p.</summary>
        public FixVec2 ClosestCore(FixVec2 p) => Shape == ObstacleShape.Circle ? A : Geometry.ClosestPointOnSegment(A, B, p);

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

        /// <summary>Smallest obstacle radius / half-thickness (Fix.MaxValue when there are none).</summary>
        public readonly Fix MinObstacleRadius = Fix.MaxValue;

        /// <summary>Damage to a ball bouncing off an arena wall inside a spike zone (spike walls). 0 = none.</summary>
        public readonly Fix WallDamage;

        /// <summary>Corners with spike zones (SpikeZones); only used when WallDamage &gt; 0.</summary>
        public readonly SpikeCorner SpikeCorners;

        public ArenaLayout(Obstacle[] obstacles, Fix wallDamage = default, SpikeCorner spikeCorners = ArenaTuning.SpikeCorners)
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
            if (WallDamage > Fix.Zero) h = SimHash.Mix(h, (int)SpikeCorners);   // only spike layouts: every other layout hash is unchanged
            h = SimHash.Mix(h, obstacles.Length);
            for (var i = 0; i < obstacles.Length; i++) h = obstacles[i].HashInto(h);
            return h;
        }
    }
}
