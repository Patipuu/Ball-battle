using System;

namespace BallBattle.Sim
{
    /// <summary>Minimal 2D vector for the simulation (UnityEngine.Vector2 is not allowed in Sim). Units = native pixels.</summary>
    public struct Vec2
    {
        public float X;
        public float Y;

        public Vec2(float x, float y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec2 Zero = new Vec2(0f, 0f);

        public float LengthSq => X * X + Y * Y;
        public float Length => MathF.Sqrt(X * X + Y * Y);

        /// <summary>Unit vector, or <paramref name="fallback"/> when the length is ~0.</summary>
        public Vec2 NormalizedOr(Vec2 fallback)
        {
            var len = Length;
            return len > 1e-6f ? new Vec2(X / len, Y / len) : fallback;
        }

        public static Vec2 FromAngleDeg(float degrees)
        {
            var rad = degrees * (MathF.PI / 180f);
            return new Vec2(MathF.Cos(rad), MathF.Sin(rad));
        }

        public static float Dot(Vec2 a, Vec2 b) => a.X * b.X + a.Y * b.Y;

        public static Vec2 operator +(Vec2 a, Vec2 b) => new Vec2(a.X + b.X, a.Y + b.Y);
        public static Vec2 operator -(Vec2 a, Vec2 b) => new Vec2(a.X - b.X, a.Y - b.Y);
        public static Vec2 operator -(Vec2 a) => new Vec2(-a.X, -a.Y);
        public static Vec2 operator *(Vec2 a, float s) => new Vec2(a.X * s, a.Y * s);
        public static Vec2 operator *(float s, Vec2 a) => new Vec2(a.X * s, a.Y * s);

        public override string ToString() => $"({X:0.###}, {Y:0.###})";
    }
}
