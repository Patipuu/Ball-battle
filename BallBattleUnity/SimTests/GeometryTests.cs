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
            var c = Geometry.ClosestPointOnSegment(new Vec2(0, 0), new Vec2(10, 0), new Vec2(4, 5));
            Assert.That(c.X, Is.EqualTo(4f).Within(Tol));
            Assert.That(c.Y, Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void PointClampsToSegmentEnds()
        {
            var a = Geometry.ClosestPointOnSegment(new Vec2(0, 0), new Vec2(10, 0), new Vec2(-3, 2));
            var b = Geometry.ClosestPointOnSegment(new Vec2(0, 0), new Vec2(10, 0), new Vec2(14, -2));
            Assert.That(a.X, Is.EqualTo(0f).Within(Tol));
            Assert.That(b.X, Is.EqualTo(10f).Within(Tol));
        }

        [Test]
        public void DegenerateSegmentReturnsItsPoint()
        {
            var c = Geometry.ClosestPointOnSegment(new Vec2(3, 3), new Vec2(3, 3), new Vec2(9, 9));
            Assert.That(c.X, Is.EqualTo(3f).Within(Tol));
        }

        [Test]
        public void CrossingSegmentsHaveZeroDistance()
        {
            var d = Geometry.SegmentSegmentClosest(new Vec2(-5, 0), new Vec2(5, 0), new Vec2(0, -5), new Vec2(0, 5), out var c1, out var c2);
            Assert.That(d, Is.EqualTo(0f).Within(Tol));
            Assert.That(c1.X, Is.EqualTo(0f).Within(Tol));
            Assert.That(c2.Y, Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void ParallelSegmentsDistance()
        {
            var d = Geometry.SegmentSegmentClosest(new Vec2(0, 0), new Vec2(10, 0), new Vec2(2, 3), new Vec2(8, 3), out _, out _);
            Assert.That(d, Is.EqualTo(9f).Within(Tol));
        }

        [Test]
        public void CollinearDisjointSegmentsMeasureGap()
        {
            var d = Geometry.SegmentSegmentClosest(new Vec2(0, 0), new Vec2(4, 0), new Vec2(7, 0), new Vec2(12, 0), out var c1, out var c2);
            Assert.That(d, Is.EqualTo(9f).Within(Tol));
            Assert.That(c1.X, Is.EqualTo(4f).Within(Tol));
            Assert.That(c2.X, Is.EqualTo(7f).Within(Tol));
        }

        [Test]
        public void EndpointTouchingCountsAsContact()
        {
            var d = Geometry.SegmentSegmentClosest(new Vec2(0, 0), new Vec2(5, 5), new Vec2(5, 5), new Vec2(10, 0), out _, out _);
            Assert.That(d, Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void TwoPointsDegenerate()
        {
            var d = Geometry.SegmentSegmentClosest(new Vec2(0, 0), new Vec2(0, 0), new Vec2(3, 4), new Vec2(3, 4), out _, out _);
            Assert.That(d, Is.EqualTo(25f).Within(Tol));
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
                var f = z.NextFloat01();
                Assert.That(f, Is.GreaterThanOrEqualTo(0f).And.LessThan(1f));
            }
        }
    }
}
