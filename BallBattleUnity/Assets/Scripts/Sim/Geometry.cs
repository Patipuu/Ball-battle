namespace BallBattle.Sim
{
    /// <summary>Closest-point queries used for blade/ball/blade contact. Pure functions, no allocation.</summary>
    public static class Geometry
    {
        const float Epsilon = 1e-8f;

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

        /// <summary>Closest point on segment [a,b] to point p. Degenerate segment (a==b) returns a.</summary>
        public static Vec2 ClosestPointOnSegment(Vec2 a, Vec2 b, Vec2 p)
        {
            var ab = b - a;
            var lenSq = ab.LengthSq;
            if (lenSq <= Epsilon) return a;
            var t = Clamp01(Vec2.Dot(p - a, ab) / lenSq);
            return a + ab * t;
        }

        /// <summary>
        /// Squared distance between segments [p1,q1] and [p2,q2], with the closest point on each
        /// (Ericson, Real-Time Collision Detection 5.1.9). Crossing segments return 0.
        /// </summary>
        public static float SegmentSegmentClosest(Vec2 p1, Vec2 q1, Vec2 p2, Vec2 q2, out Vec2 c1, out Vec2 c2)
        {
            var d1 = q1 - p1;
            var d2 = q2 - p2;
            var r = p1 - p2;
            var a = Vec2.Dot(d1, d1);
            var e = Vec2.Dot(d2, d2);
            var f = Vec2.Dot(d2, r);
            float s, t;

            if (a <= Epsilon && e <= Epsilon)
            {
                c1 = p1;
                c2 = p2;
                return (c1 - c2).LengthSq;
            }

            if (a <= Epsilon)
            {
                s = 0f;
                t = Clamp01(f / e);
            }
            else
            {
                var c = Vec2.Dot(d1, r);
                if (e <= Epsilon)
                {
                    t = 0f;
                    s = Clamp01(-c / a);
                }
                else
                {
                    var b = Vec2.Dot(d1, d2);
                    var denom = a * e - b * b;
                    s = denom > Epsilon ? Clamp01((b * f - c * e) / denom) : 0f;
                    t = (b * s + f) / e;
                    if (t < 0f)
                    {
                        t = 0f;
                        s = Clamp01(-c / a);
                    }
                    else if (t > 1f)
                    {
                        t = 1f;
                        s = Clamp01((b - c) / a);
                    }
                }
            }

            c1 = p1 + d1 * s;
            c2 = p2 + d2 * t;
            return (c1 - c2).LengthSq;
        }
    }
}
