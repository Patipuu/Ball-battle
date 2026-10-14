namespace BallBattle.Sim
{
    /// <summary>
    /// Q16.16 vector, the Sim's position/velocity type (units = native pixels, per tick). Fields are mutable like the old
    /// float Vec2 so the sim can clamp one axis in place. Dot and squared length stay Q32.32 in long (exact).
    /// </summary>
    public struct FixVec2
    {
        public Fix X;
        public Fix Y;
        public static readonly FixVec2 Zero = new FixVec2(Fix.Zero, Fix.Zero);
        public static readonly FixVec2 Right = new FixVec2(Fix.One, Fix.Zero);

        public FixVec2(Fix x, Fix y) { X = x; Y = y; }

        /// <summary>Squared length, Q32.32 (exact).</summary>
        public long LengthSq => Dot(this, this);
        /// <summary>Length, floor of the exact square root.</summary>
        public Fix Length => FixMath.Sqrt(LengthSq);

        /// <summary>Dot product, Q32.32 (exact).</summary>
        public static long Dot(FixVec2 a, FixVec2 b)
        {
#if DEBUG || UNITY_EDITOR
            return checked((long)a.X.Raw * b.X.Raw + (long)a.Y.Raw * b.Y.Raw);
#else
            return unchecked((long)a.X.Raw * b.X.Raw + (long)a.Y.Raw * b.Y.Raw);
#endif
        }

        /// <summary>Dot product as Fix: Q32.32 shifted right 16 (rounds toward negative infinity).</summary>
        public static Fix DotFix(FixVec2 a, FixVec2 b) => Fix.FromRaw(checked((int)(Dot(a, b) >> Fix.FractionBits)));

        /// <summary>
        /// Unit vector (each axis = axis / length, rounded toward zero), or <paramref name="fallback"/> when the length is 0
        /// (below 1/65536 px: the float version used 1e-6 px).
        /// </summary>
        public FixVec2 NormalizedOr(FixVec2 fallback)
        {
            var len = Length;
            return len.Raw > 0 ? new FixVec2(X / len, Y / len) : fallback;
        }

        /// <summary>Direction of <paramref name="degrees"/> from the 0.1° table (FixedTrig), angle rounded to the nearest 0.1° (half up).</summary>
        public static FixVec2 FromAngleDeg(Fix degrees)
        {
            var tenths = AngleTenths(degrees);
            return new FixVec2(FixedTrig.Cos(tenths), FixedTrig.Sin(tenths));
        }

        /// <summary>Degrees → nearest 0.1° step (half up), as the float table lookup did.</summary>
        public static int AngleTenths(Fix degrees) => (int)(((long)degrees.Raw * 10 + Fix.OneRaw / 2) >> Fix.FractionBits);

        public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new FixVec2(a.X + b.X, a.Y + b.Y);
        public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new FixVec2(a.X - b.X, a.Y - b.Y);
        public static FixVec2 operator -(FixVec2 a) => new FixVec2(-a.X, -a.Y);
        public static FixVec2 operator *(FixVec2 a, Fix scalar) => new FixVec2(a.X * scalar, a.Y * scalar);
        public static FixVec2 operator *(Fix scalar, FixVec2 a) => a * scalar;
        public static FixVec2 operator *(FixVec2 a, int scalar) => new FixVec2(a.X * scalar, a.Y * scalar);
        /// <summary>Per axis, rounds toward zero.</summary>
        public static FixVec2 operator /(FixVec2 a, int divisor) => new FixVec2(a.X / divisor, a.Y / divisor);

        public override string ToString() => $"({X.Raw}, {Y.Raw}) raw";
    }
}
