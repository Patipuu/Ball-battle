using System;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>Compiled unchanged by Unity EditMode and the standalone SimTests project.</summary>
    public class FixedMathTests
    {
        [Test]
        public void ArithmeticUsesDocumentedRoundingAndChecksOverflow()
        {
            Assert.That(Fix.FromInt(3).Raw, Is.EqualTo(3 << 16));
            Assert.That(Fix.FromRatio(-1, 3).Raw, Is.EqualTo(-21845));
            Assert.That((Fix.FromRaw(-1) / Fix.FromInt(2)).Raw, Is.Zero); // divide toward zero
            Assert.That((Fix.FromRaw(-1) * Fix.FromRaw(32768)).Raw, Is.EqualTo(-1)); // arithmetic shift
            Assert.That(FixMath.Min(Fix.FromInt(-2), Fix.One), Is.EqualTo(Fix.FromInt(-2)));
            Assert.That(FixMath.Max(Fix.FromInt(-2), Fix.One), Is.EqualTo(Fix.One));
            Assert.That(FixMath.Clamp(Fix.FromInt(4), Fix.Zero, Fix.One), Is.EqualTo(Fix.One));
            Assert.That(FixMath.Abs(Fix.FromInt(-4)), Is.EqualTo(Fix.FromInt(4)));
            Assert.Throws<OverflowException>(() => { var ignored = Fix.FromRaw(int.MaxValue) + Fix.One; });
            Assert.Throws<OverflowException>(() => Fix.FromInt(32768));
        }

        [Test]
        public void IntegerSquareRootHandlesZeroOneLargeAndNonSquare()
        {
            Assert.That(FixMath.Sqrt(0).Raw, Is.Zero);
            Assert.That(FixMath.Sqrt(1L << 32), Is.EqualTo(Fix.One));
            var two = FixMath.Sqrt(2L << 32).Raw;
            Assert.That((long)two * two, Is.LessThanOrEqualTo(2L << 32));
            Assert.That((long)(two + 1) * (two + 1), Is.GreaterThan(2L << 32));
            var veryLarge = (long)int.MaxValue * int.MaxValue;
            Assert.That(FixMath.Sqrt(veryLarge).Raw, Is.EqualTo(int.MaxValue));
            Assert.Throws<OverflowException>(() => FixMath.Sqrt(long.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => FixMath.Sqrt(-1));
        }

        [Test]
        public void TrigUsesTenthsAndAtan2CoversAxesAndQuadrants()
        {
            Assert.That(FixedTrig.Sin(0).Raw, Is.Zero);
            Assert.That(FixedTrig.Cos(0), Is.EqualTo(Fix.One));
            Assert.That(FixedTrig.Sin(900), Is.EqualTo(Fix.One));
            Assert.That(FixedTrig.Cos(900).Raw, Is.Zero);
            Assert.That(FixedTrig.Sin(1800).Raw, Is.Zero);
            Assert.That(FixedTrig.Cos(1800), Is.EqualTo(-Fix.One));
            Assert.That(FixedTrig.Sin(2700), Is.EqualTo(-Fix.One));
            Assert.That(FixedTrig.Cos(2700).Raw, Is.Zero);
            Assert.That(FixedTrig.Sin(3599).Raw, Is.EqualTo(-114));
            Assert.That(FixedTrig.Cos(3599), Is.EqualTo(Fix.One));
            Assert.That(FixedTrig.Atan2(FixedTrig.Sin(3599), FixedTrig.Cos(3599)), Is.EqualTo(3599));
            Assert.That(FixedTrig.Atan2(Fix.Zero, Fix.Zero), Is.Zero);
            Assert.That(FixedTrig.Atan2(Fix.Zero, Fix.One), Is.Zero);
            Assert.That(FixedTrig.Atan2(Fix.One, Fix.Zero), Is.EqualTo(900));
            Assert.That(FixedTrig.Atan2(Fix.Zero, -Fix.One), Is.EqualTo(1800));
            Assert.That(FixedTrig.Atan2(-Fix.One, Fix.Zero), Is.EqualTo(2700));
            Assert.That(FixedTrig.Atan2(Fix.One, Fix.One), Is.EqualTo(450));
            Assert.That(FixedTrig.Atan2(Fix.One, -Fix.One), Is.EqualTo(1350));
            Assert.That(FixedTrig.Atan2(-Fix.One, -Fix.One), Is.EqualTo(2250));
            Assert.That(FixedTrig.Atan2(-Fix.One, Fix.One), Is.EqualTo(3150));
        }

        [Test]
        public void SquaredDistanceAndDotFitRealArenaAndRunSpeedLimits()
        {
            var config = new MatchConfig();
            var arena = config.ArenaAt(0);
            var width = (int)arena.Width;
            var height = (int)arena.Height;
            Assert.That(arena.Width, Is.EqualTo(width));
            Assert.That(arena.Height, Is.EqualTo(height));
            var farCorner = new FixVec2(Fix.FromRatio(width, 2), Fix.FromRatio(height, 2));
            var otherCorner = new FixVec2(Fix.FromRatio(-width, 2), Fix.FromRatio(-height, 2));
            var diagonal = farCorner - otherCorner;
            var distanceSquared = diagonal.LengthSq;
            Assert.That(distanceSquared, Is.EqualTo((long)Fix.FromInt(width).Raw * Fix.FromInt(width).Raw +
                                                    (long)Fix.FromInt(height).Raw * Fix.FromInt(height).Raw));
            Assert.That(diagonal.Length.Raw, Is.GreaterThan(Fix.FromInt(181).Raw));

            // Every possible speed card: initial pick, two per fight, and the one retry pick.
            var maxCards = 2 + RunTuning.Fights * RunTuning.MaxPicksPerFight;
            var cap = (int)Math.Ceiling((config.MaxSpeed + WeaponTuning.BrawlerMaxSpeedBonus) *
                                        (1f + maxCards * RunTuning.SpeedCardPct));
            var fastest = new FixVec2(Fix.FromInt(cap), Fix.FromInt(cap));
            var velocitySquared = fastest.LengthSq;
            var dot = FixVec2.Dot(fastest, fastest);
            Assert.That(dot, Is.EqualTo(velocitySquared));
            Assert.That(distanceSquared, Is.GreaterThan(0));
            Assert.That(velocitySquared, Is.GreaterThan(0));
            Assert.That(long.MaxValue / distanceSquared, Is.GreaterThan(1000));
            Assert.That(long.MaxValue / velocitySquared, Is.GreaterThan(1000));
            TestContext.Out.WriteLine($"arena={width}x{height}; diagonalSqRaw={distanceSquared}; speedCapConservative={cap}; speedSqRaw={velocitySquared}; arenaMargin={long.MaxValue / distanceSquared}; speedMargin={long.MaxValue / velocitySquared}");
        }

        [Test]
        public void RandomIntegerAndFixedRangesAreHalfOpen()
        {
            var rng = new SimRandom(0x6A6A1234u);
            for (var i = 0; i < 10000; i++)
            {
                var n = rng.RangeInt(-7, 13);
                var fixedValue = rng.RangeFix(Fix.FromInt(-7), Fix.FromInt(13));
                Assert.That(n, Is.InRange(-7, 12));
                Assert.That(fixedValue.Raw, Is.InRange(Fix.FromInt(-7).Raw, Fix.FromInt(13).Raw - 1));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.RangeInt(4, 4));
            Assert.Throws<ArgumentOutOfRangeException>(() => rng.RangeFix(Fix.One, Fix.One));
        }

        [Test]
        public void FixedOperationsHaveCrossPlatformGoldenHash()
        {
            var rng = new SimRandom(0x6A6A1234u);
            const ulong offset = 14695981039346656037UL;
            const ulong prime = 1099511628211UL;
            ulong hash = offset;
            for (var i = 0; i < 10000; i++)
            {
                var a = Fix.FromRaw(rng.RangeInt(-1000000, 1000001));
                var b = Fix.FromRaw(rng.RangeInt(65536, 2000001));
                var sum = a + b;
                var product = a * b;
                var quotient = a / b;
                var randomFix = rng.RangeFix(Fix.FromInt(-8), Fix.FromInt(9));
                var angle = rng.RangeInt(0, 3600);
                var sine = FixedTrig.Sin(angle);
                var cosine = FixedTrig.Cos(angle);
                var vector = new FixVec2(a, b);
                var length = vector.Length;
                var angleBack = FixedTrig.Atan2(sine, cosine);
                unchecked
                {
                    hash = (hash ^ (uint)sum.Raw) * prime;
                    hash = (hash ^ (uint)product.Raw) * prime;
                    hash = (hash ^ (uint)quotient.Raw) * prime;
                    hash = (hash ^ (uint)randomFix.Raw) * prime;
                    hash = (hash ^ (uint)sine.Raw) * prime;
                    hash = (hash ^ (uint)cosine.Raw) * prime;
                    hash = (hash ^ (uint)length.Raw) * prime;
                    hash = (hash ^ (uint)angleBack) * prime;
                }
            }
            TestContext.Out.WriteLine($"Fix 10000 hash: 0x{hash:X16}");
            Assert.That(hash, Is.EqualTo(0xB938B2578807CC32UL));
        }
    }
}
