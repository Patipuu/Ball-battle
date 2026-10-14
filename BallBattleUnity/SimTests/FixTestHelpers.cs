using System;
using BallBattle.Sim;

namespace BallBattle.SimTests
{
    /// <summary>
    /// 6a.6b/c test-side conversions (tests may use double; the Sim may not). X(v) = nearest Q16.16 to v (ties away from
    /// zero), V(x, y) = vector of two X values, F(fix) = the value as a float for readable asserts.
    /// </summary>
    static class FT
    {
        public static Fix X(double v) => Fix.FromRaw(checked((int)Math.Round(v * Fix.OneRaw, MidpointRounding.AwayFromZero)));
        public static Fix X(Fix v) => v;
        public static Fix X(int v) => Fix.FromInt(v);
        public static FixVec2 V(double x, double y) => new FixVec2(X(x), X(y));
        public static FixVec2 V(double x, Fix y) => new FixVec2(X(x), y);
        public static FixVec2 V(double x, int y) => new FixVec2(X(x), Fix.FromInt(y));
        public static FixVec2 V(Fix x, double y) => new FixVec2(x, X(y));
        public static FixVec2 V(Fix x, Fix y) => new FixVec2(x, y);
        public static FixVec2 V(Fix x, int y) => new FixVec2(x, Fix.FromInt(y));
        public static FixVec2 V(int x, double y) => new FixVec2(Fix.FromInt(x), X(y));
        public static FixVec2 V(int x, Fix y) => new FixVec2(Fix.FromInt(x), y);
        public static FixVec2 V(int x, int y) => new FixVec2(Fix.FromInt(x), Fix.FromInt(y));
        /// <summary>A Q32.32 raw value (squared length / dot) as a double.</summary>
        public static double Q32(long v) => v / 4294967296.0;
        public static float F(this Fix f) => f.ToFloat();
        public static float F(this float f) => f;
        public static float F(this int i) => i;
        public static float F(this double d) => (float)d;
        public static double D(this Fix f) => f.Raw / (double)Fix.OneRaw;
    }
}
