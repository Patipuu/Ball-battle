using System;
using BallBattle.Sim;

namespace BallBattle.Tests
{
    /// <summary>Test-side helpers for the fixed-point Sim (6a.6b/c): X = nearest Q16.16 to a decimal value (ties away from zero).</summary>
    public static class TFix
    {
        public static Fix X(double v) => Fix.FromRaw(checked((int)Math.Round(v * Fix.OneRaw, MidpointRounding.AwayFromZero)));
        public static FixVec2 V(double x, double y) => new FixVec2(X(x), X(y));
        public static float F(this Fix f) => f.ToFloat();
        public static float F(this float f) => f;
        public static float F(this int i) => i;
        public static float F(this double d) => (float)d;
    }
}
