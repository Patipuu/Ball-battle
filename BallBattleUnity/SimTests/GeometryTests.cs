using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class GeometryTests
    {
        const float Tol = 1e-4f;

        [Test]
        public void PointProjectsInsideSegment()
        {
            var c = Geometry.ClosestPointOnSegment(FT.V(0, 0), FT.V(10, 0), FT.V(4, 5));
            Assert.That(c.X.F(), Is.EqualTo(4f).Within(Tol));
            Assert.That(c.Y.F(), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void PointClampsToSegmentEnds()
        {
            var a = Geometry.ClosestPointOnSegment(FT.V(0, 0), FT.V(10, 0), FT.V(-3, 2));
            var b = Geometry.ClosestPointOnSegment(FT.V(0, 0), FT.V(10, 0), FT.V(14, -2));
            Assert.That(a.X.F(), Is.EqualTo(0f).Within(Tol));
            Assert.That(b.X.F(), Is.EqualTo(10f).Within(Tol));
        }

        [Test]
        public void DegenerateSegmentReturnsItsPoint()
        {
            var c = Geometry.ClosestPointOnSegment(FT.V(3, 3), FT.V(3, 3), FT.V(9, 9));
            Assert.That(c.X.F(), Is.EqualTo(3f).Within(Tol));
        }

        [Test]
        public void CrossingSegmentsHaveZeroDistance()
        {
            var d = Geometry.SegmentSegmentClosest(FT.V(-5, 0), FT.V(5, 0), FT.V(0, -5), FT.V(0, 5), out var c1, out var c2);
            Assert.That(FT.Q32(d), Is.EqualTo(0f).Within(Tol));
            Assert.That(c1.X.F(), Is.EqualTo(0f).Within(Tol));
            Assert.That(c2.Y.F(), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void ParallelSegmentsDistance()
        {
            var d = Geometry.SegmentSegmentClosest(FT.V(0, 0), FT.V(10, 0), FT.V(2, 3), FT.V(8, 3), out _, out _);
            Assert.That(FT.Q32(d), Is.EqualTo(9f).Within(Tol));
        }

        [Test]
        public void CollinearDisjointSegmentsMeasureGap()
        {
            var d = Geometry.SegmentSegmentClosest(FT.V(0, 0), FT.V(4, 0), FT.V(7, 0), FT.V(12, 0), out var c1, out var c2);
            Assert.That(FT.Q32(d), Is.EqualTo(9f).Within(Tol));
            Assert.That(c1.X.F(), Is.EqualTo(4f).Within(Tol));
            Assert.That(c2.X.F(), Is.EqualTo(7f).Within(Tol));
        }

        [Test]
        public void EndpointTouchingCountsAsContact()
        {
            var d = Geometry.SegmentSegmentClosest(FT.V(0, 0), FT.V(5, 5), FT.V(5, 5), FT.V(10, 0), out _, out _);
            Assert.That(FT.Q32(d), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void TwoPointsDegenerate()
        {
            var d = Geometry.SegmentSegmentClosest(FT.V(0, 0), FT.V(0, 0), FT.V(3, 4), FT.V(3, 4), out _, out _);
            Assert.That(FT.Q32(d), Is.EqualTo(25f).Within(Tol));
        }

        [Test]
        public void RandomIsDeterministicAndNonZeroSeedSafe()
        {
            var a = new SimRandom(42);
            var b = new SimRandom(42);
            for (var i = 0; i < 1000; i++) Assert.That(a.NextUInt(), Is.EqualTo(b.NextUInt()));

            var z = new SimRandom(0);
            Assert.That(z.NextUInt(), Is.Not.EqualTo(0u));
            for (var i = 0; i < 1000; i++)
            {
                var f = z.NextFix01();
                Assert.That(f.Raw, Is.GreaterThanOrEqualTo(0).And.LessThan(Fix.OneRaw));
            }
        }
    }
}
