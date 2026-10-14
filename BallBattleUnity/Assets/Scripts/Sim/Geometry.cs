namespace BallBattle.Sim
{
    /// <summary>
    /// Closest-point queries used for blade/ball/blade contact. Pure functions, no allocation, integer-only.
    /// Squared distances are Q32.32 longs (exact); segment parameters are Fix in [0, 1].
    /// Degenerate tests: a squared length of 0 (below 1/65536 px²) counts as a point (the float version used 1e-8).
    /// </summary>
    public static class Geometry
    {
        /// <summary>Closest point on segment [a,b] to point p. Degenerate segment (a==b) returns a. t rounds toward zero.</summary>
        public static FixVec2 ClosestPointOnSegment(FixVec2 a, FixVec2 b, FixVec2 p)
        {
            var ab = b - a;
            var lenSq = ab.LengthSq;
            if (lenSq <= 0) return a;
            var t = FixMath.Clamp01(FixMath.RawRatio(FixVec2.Dot(p - a, ab), lenSq));
            return a + ab * t;
        }

        /// <summary>True when the segments are within <paramref name="reach"/> of each other (closest points returned).</summary>
        public static bool SegmentsTouch(FixVec2 a0, FixVec2 a1, FixVec2 b0, FixVec2 b1, Fix reach, out FixVec2 pa, out FixVec2 pb)
            => SegmentSegmentClosest(a0, a1, b0, b1, out pa, out pb) <= FixMath.Sq(reach);

        /// <summary>
        /// Squared distance (Q32.32) between segments [p1,q1] and [p2,q2], with the closest point on each
        /// (Ericson, Real-Time Collision Detection 5.1.9). Crossing segments return 0.
        /// The dot products are taken down to Q16.16 (arithmetic shift) so their products fit a long as Q32.32.
        /// </summary>
        public static long SegmentSegmentClosest(FixVec2 p1, FixVec2 q1, FixVec2 p2, FixVec2 q2, out FixVec2 c1, out FixVec2 c2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = FixVec2.Dot(d1, d1) >> Fix.FractionBits;
            var e = FixVec2.Dot(d2, d2) >> Fix.FractionBits;
            var f = FixVec2.Dot(d2, r) >> Fix.FractionBits;
            Fix s, t;

            if (a <= 0 && e <= 0)
            {
                c1 = p1;
                c2 = p2;
                return (c1 - c2).LengthSq;
            }

            if (a <= 0)
            {
                s = Fix.Zero;
                t = FixMath.Clamp01(FixMath.RawRatio(f, e));
            }
            else
            {
                var c = FixVec2.Dot(d1, r) >> Fix.FractionBits;
                if (e <= 0)
                {
                    t = Fix.Zero;
                    s = FixMath.Clamp01(FixMath.RawRatio(-c, a));
                }
                else
                {
                    var b = FixVec2.Dot(d1, d2) >> Fix.FractionBits;
                    var denom = a * e - b * b;
                    s = denom > 0 ? FixMath.Clamp01(FixMath.RawRatio(b * f - c * e, denom)) : Fix.Zero;
                    var tNum = ((b * s.Raw) >> Fix.FractionBits) + f;
                    t = FixMath.RawRatio(tNum, e);
                    if (t.Raw < 0)
                    {
                        t = Fix.Zero;
                        s = FixMath.Clamp01(FixMath.RawRatio(-c, a));
                    }
                    else if (t.Raw > Fix.OneRaw)
                    {
                        t = Fix.One;
                        s = FixMath.Clamp01(FixMath.RawRatio(b - c, a));
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).LengthSq;
        }
    }
}
