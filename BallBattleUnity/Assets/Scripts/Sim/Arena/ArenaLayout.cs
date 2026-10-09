using System;

namespace BallBattle.Sim
{
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

        /// <summary>Damage to a ball each time it bounces off an arena wall (spike walls). 0 = none.</summary>
        public readonly float WallDamage;

        public ArenaLayout(Obstacle[] obstacles, float wallDamage = 0f)
        {
            this.obstacles = obstacles != null ? (Obstacle[])obstacles.Clone() : Array.Empty<Obstacle>();
            WallDamage = wallDamage;
            foreach (var o in this.obstacles)
                if (o.Radius < MinObstacleRadius) MinObstacleRadius = o.Radius;
        }

        public Obstacle Get(int index) => obstacles[index];

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, WallDamage);
            h = SimHash.Mix(h, obstacles.Length);
            for (var i = 0; i < obstacles.Length; i++) h = obstacles[i].HashInto(h);
            return h;
        }
    }
}
