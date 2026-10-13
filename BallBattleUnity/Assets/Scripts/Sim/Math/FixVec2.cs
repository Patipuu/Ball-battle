namespace BallBattle.Sim
{
    /// <summary>Q16.16 vector. Dot and squared length stay Q32.32 in long.</summary>
    public readonly struct FixVec2
    {
        public readonly Fix X;
        public readonly Fix Y;
        public static readonly FixVec2 Zero = new FixVec2(Fix.Zero, Fix.Zero);

        public FixVec2(Fix x, Fix y) { X = x; Y = y; }

        public long LengthSq => Dot(this, this);
        public Fix Length => FixMath.Sqrt(LengthSq);

        public static long Dot(FixVec2 a, FixVec2 b)
        {
#if DEBUG || UNITY_EDITOR
            return checked((long)a.X.Raw * b.X.Raw + (long)a.Y.Raw * b.Y.Raw);
#else
            return unchecked((long)a.X.Raw * b.X.Raw + (long)a.Y.Raw * b.Y.Raw);
#endif
        }

        public static FixVec2 operator +(FixVec2 a, FixVec2 b) => new FixVec2(a.X + b.X, a.Y + b.Y);
        public static FixVec2 operator -(FixVec2 a, FixVec2 b) => new FixVec2(a.X - b.X, a.Y - b.Y);
        public static FixVec2 operator *(FixVec2 a, Fix scalar) => new FixVec2(a.X * scalar, a.Y * scalar);
        public static FixVec2 operator *(Fix scalar, FixVec2 a) => a * scalar;
    }
}
