using System;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>6a.6b/c: the Fix / FixVec2 / FixMath helpers the converted Sim uses, with their documented rounding.</summary>
    public class FixHelperTests
    {
        static Fix R(int raw) => Fix.FromRaw(raw);

        [Test]
        public void RatioIsNearestTiesAwayFromZero()
        {
            Assert.That(Fix.Ratio(1, 2).Raw, Is.EqualTo(32768));
            Assert.That(Fix.Ratio(1, 3).Raw, Is.EqualTo(21845));          // 21845.33
            Assert.That(Fix.Ratio(2, 3).Raw, Is.EqualTo(43691));          // 43690.67
            Assert.That(Fix.Ratio(-2, 3).Raw, Is.EqualTo(-43691));
            Assert.That(Fix.Ratio(1, 10).Raw, Is.EqualTo(6554));          // 6553.6
            Assert.That(Fix.Ratio(1, 131072).Raw, Is.EqualTo(1));         // exactly half a raw unit: away from zero
            Assert.That(Fix.Ratio(-1, 131072).Raw, Is.EqualTo(-1));
            Assert.That(Fix.Ratio(1, -131072).Raw, Is.EqualTo(-1));
            Assert.That(Fix.FromRatio(2, 3).Raw, Is.EqualTo(43690), "FromRatio keeps rounding toward zero");
            Assert.Throws<DivideByZeroException>(() => Fix.Ratio(1, 0));
        }

        [Test]
        public void MulRoundsTowardNegativeInfinityDivTowardZero()
        {
            Assert.That((R(3) * Fix.Half).Raw, Is.EqualTo(1));            // 1.5 → 1
            Assert.That((R(-3) * Fix.Half).Raw, Is.EqualTo(-2));          // -1.5 → -2
            Assert.That((Fix.FromInt(7) / Fix.FromInt(2)).Raw, Is.EqualTo(229376));
            Assert.That((R(-3) / 2).Raw, Is.EqualTo(-1));                 // -1.5 → -1
            Assert.That((Fix.FromInt(5) * 3), Is.EqualTo(Fix.FromInt(15)));
            Assert.That((Fix)7, Is.EqualTo(Fix.FromInt(7)));
            Assert.That((Fix.FromInt(7) % Fix.FromInt(3)), Is.EqualTo(Fix.One));
        }

        [Test]
        public void OverflowIsCheckedInTestBuilds()
        {
            Assert.Throws<OverflowException>(() => { var _ = Fix.FromInt(30000) * Fix.FromInt(2); });
            Assert.Throws<OverflowException>(() => { var _ = Fix.FromInt(40000); });
        }

        [Test]
        public void MinMaxClampAbsSign()
        {
            Assert.That(FixMath.Min(R(1), R(2)), Is.EqualTo(R(1)));
            Assert.That(FixMath.Max(R(1), R(2)), Is.EqualTo(R(2)));
            Assert.That(FixMath.Clamp(R(5), R(1), R(3)), Is.EqualTo(R(3)));
            Assert.That(FixMath.Clamp(R(-5), R(1), R(3)), Is.EqualTo(R(1)));
            Assert.Throws<ArgumentException>(() => FixMath.Clamp(R(0), R(3), R(1)));
            Assert.That(FixMath.Clamp01(Fix.FromInt(2)), Is.EqualTo(Fix.One));
            Assert.That(FixMath.Clamp01(R(-1)), Is.EqualTo(Fix.Zero));
            Assert.That(FixMath.Abs(R(-7)), Is.EqualTo(R(7)));
            Assert.That((FixMath.Sign(R(-7)), FixMath.Sign(Fix.Zero), FixMath.Sign(R(1))), Is.EqualTo((-1, 0, 1)));
        }

        [Test]
        public void SqrtIsFloorOfExactRoot()
        {
            Assert.That(FixMath.SqrtFix(Fix.FromInt(9)), Is.EqualTo(Fix.FromInt(3)));
            Assert.That(FixMath.SqrtFix(Fix.FromInt(2)).Raw, Is.EqualTo(92681));   // √2 = 1.41421356 → 92681.9 floored
            Assert.That(FixMath.Sqrt(FixMath.Sq(Fix.FromInt(300))), Is.EqualTo(Fix.FromInt(300)));
            Assert.That(FixMath.Sqrt(0), Is.EqualTo(Fix.Zero));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.Sqrt(-1));
            for (var raw = 1; raw < 2_000_000; raw += 9973)
            {
                var r = FixMath.SqrtFix(R(raw)).Raw;
                var q = (long)raw << 16;
                Assert.That((long)r * r, Is.LessThanOrEqualTo(q));
                Assert.That((long)(r + 1) * (r + 1), Is.GreaterThan(q));
            }
        }

        [Test]
        public void RoundingHelpers()
        {
            Assert.That(FixMath.FloorToInt(Fix.Ratio(-1, 2)), Is.EqualTo(-1));
            Assert.That(FixMath.CeilToInt(Fix.Ratio(-1, 2)), Is.EqualTo(0));
            Assert.That(FixMath.CeilToInt(Fix.Ratio(1, 2)), Is.EqualTo(1));
            Assert.That(FixMath.CeilToInt(Fix.FromInt(4)), Is.EqualTo(4));
            Assert.That(FixMath.RoundToEven(Fix.Ratio(5, 2)), Is.EqualTo(2));
            Assert.That(FixMath.RoundToEven(Fix.Ratio(7, 2)), Is.EqualTo(4));
            Assert.That(FixMath.RoundToEven(Fix.Ratio(-5, 2)), Is.EqualTo(-2));
            Assert.That(FixMath.RoundToEven(R(98304 + 1)), Is.EqualTo(2));
            Assert.That(FixMath.CeilDiv(Fix.FromInt(10), Fix.FromInt(4)), Is.EqualTo(3));
            Assert.That(FixMath.CeilDiv(Fix.FromInt(8), Fix.FromInt(4)), Is.EqualTo(2));
            Assert.That(FixMath.CeilDiv(Fix.Zero, Fix.FromInt(4)), Is.EqualTo(0));
            Assert.That(FixMath.Wrap(Fix.FromInt(370), 360), Is.EqualTo(Fix.FromInt(10)));
            Assert.That(FixMath.Wrap(Fix.FromInt(-10), 360), Is.EqualTo(Fix.FromInt(350)));
            Assert.That(FixMath.MulDiv(Fix.FromInt(7), 1, 5).Raw, Is.EqualTo(91750));    // 1.4 × 65536 = 91750.4 → toward zero
            Assert.That(FixMath.MulDiv(Fix.FromInt(-7), 1, 5).Raw, Is.EqualTo(-91750));
        }

        [Test]
        public void SubstepPartsSumExactlyToPositiveAndNegativeRates()
        {
            foreach (var raw in new[] { -123456, -3277, -13, -1, 0, 1, 13, 3277, 123456 })
                foreach (var count in new[] { 2, 3, 7, 32 })
                {
                    var sum = Fix.Zero;
                    for (var i = 0; i < count; i++) sum += FixMath.SubstepPart(Fix.FromRaw(raw), i, count);
                    Assert.That(sum.Raw, Is.EqualTo(raw));
                }
        }

        [Test]
        public void RawRatioHandlesHugeOperandsAndSaturates()
        {
            Assert.That(FixMath.RawRatio(1, 4), Is.EqualTo(Fix.Ratio(1, 4)));
            Assert.That(FixMath.RawRatio(3L << 50, 4L << 50), Is.EqualTo(Fix.Ratio(3, 4)));
            Assert.That(FixMath.RawRatio(long.MaxValue / 2, 1), Is.EqualTo(Fix.MaxValue));
            Assert.That(FixMath.RawRatio(-(long.MaxValue / 2), 1), Is.EqualTo(-Fix.MaxValue));
        }

        [Test]
        public void FormatIsExactDecimalText()
        {
            Assert.That(FixMath.Format(Fix.Ratio(25, 2), 1), Is.EqualTo("12.5"));
            Assert.That(FixMath.Format(Fix.FromInt(3), 1), Is.EqualTo("3"));
            Assert.That(FixMath.Format(Fix.Ratio(1, 10), 1), Is.EqualTo("0.1"));
            Assert.That(FixMath.Format(Fix.Ratio(-3, 2), 0), Is.EqualTo("-2"));
            Assert.That(FixMath.Format(Fix.Ratio(1, 4), 2), Is.EqualTo("0.25"));
            Assert.That(FixMath.Format(Fix.Ratio(1, 100), 1), Is.EqualTo("0"));
        }

        [Test]
        public void VectorHelpers()
        {
            var v = new FixVec2(Fix.FromInt(3), Fix.FromInt(4));
            Assert.That(v.LengthSq, Is.EqualTo(25L << 32));
            Assert.That(v.Length, Is.EqualTo(Fix.FromInt(5)));
            Assert.That(FixVec2.Dot(v, v), Is.EqualTo(25L << 32));
            Assert.That(FixVec2.DotFix(v, FixVec2.Right), Is.EqualTo(Fix.FromInt(3)));
            var n = v.NormalizedOr(FixVec2.Right);
            Assert.That((n.X, n.Y), Is.EqualTo((Fix.Ratio(3, 5), Fix.Ratio(4, 5))).Or.EqualTo((Fix.FromRatio(3, 5), Fix.FromRatio(4, 5))));
            Assert.That(FixVec2.Zero.NormalizedOr(FixVec2.Right), Is.EqualTo(FixVec2.Right));
            Assert.That(FixVec2.AngleTenths(Fix.Ratio(1, 20)), Is.EqualTo(1), "0.05° rounds half up to 0.1°");
            // -0.05 is rounded to -3277 raw (-0.050003...), just below the half-up boundary.
            Assert.That(FixVec2.AngleTenths(Fix.Ratio(-1, 20)), Is.EqualTo(-1));
            Assert.That(FixVec2.AngleTenths(Fix.FromRaw(-3276)), Is.EqualTo(0));
            Assert.That(FixVec2.FromAngleDeg(Fix.FromInt(90)), Is.EqualTo(new FixVec2(Fix.Zero, Fix.One)));
            Assert.That(v / 2, Is.EqualTo(new FixVec2(Fix.Ratio(3, 2), Fix.FromInt(2))));
            Assert.That(-v * 2, Is.EqualTo(new FixVec2(Fix.FromInt(-6), Fix.FromInt(-8))));
        }

        [Test]
        public void LegacyRangeKeepsTheFloatDrawMappingAndConsumesOneDraw()
        {
            foreach (var seed in new uint[] { 42, 10001, 10500, uint.MaxValue })
            {
                var actual = new SimRandom(seed);
                var reference = new SimRandom(seed);
                foreach (var range in new[] { (-15f, 15f), (-34.5f, 69f), (0f, 360f), (5f, 7f), (0f, 0f) })
                {
                    var fraction = (reference.NextUInt() >> 8) * (1f / 16777216f);
                    var expected = FT.X(range.Item1 + (range.Item2 - range.Item1) * fraction);
                    var value = actual.RangeFixOrMin(FT.X(range.Item1), FT.X(range.Item2));
                    Assert.That(Math.Abs((long)value.Raw - expected.Raw), Is.LessThanOrEqualTo(1), "legacy float rounding differs by at most one raw unit");
                    Assert.That(actual.State, Is.EqualTo(reference.State), "exactly one draw, including an empty range");
                }
            }
        }

        [Test]
        public void RandomFixIsInUnitRangeAndDeterministic()
        {
            var a = new SimRandom(42);
            var b = new SimRandom(42);
            for (var i = 0; i < 1000; i++)
            {
                var x = a.NextFix01();
                Assert.That(x, Is.EqualTo(b.NextFix01()));
                Assert.That(x.Raw, Is.InRange(0, Fix.OneRaw - 1));
            }
            var c = new SimRandom(7);
            for (var i = 0; i < 1000; i++) Assert.That(c.RangeFixOrMin(Fix.FromInt(-15), Fix.FromInt(15)).Raw, Is.InRange(-15 << 16, 15 << 16));
        }
    }
}
