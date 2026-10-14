using System;

namespace BallBattle.Sim
{
    /// <summary>
    /// Signed Q16.16 in an int: the only number type of the simulation besides integers (6a.6b/c).
    /// Rounding, all deterministic and integer-only:
    /// - <c>Fix * Fix</c>: exact product, low 16 bits dropped by an arithmetic right shift (rounds toward negative infinity).
    /// - <c>Fix / Fix</c>, <c>Fix / int</c>: C# integer division (rounds toward zero).
    /// - <c>Fix * int</c>, <c>+</c>, <c>-</c>, <c>int → Fix</c>: exact (overflow checked in test/Editor builds).
    /// - <see cref="Ratio"/>: nearest Q16.16 to n/d, ties away from zero (used for every non-integer tuning number).
    /// - <see cref="FromRatio"/>: n/d rounded toward zero (kept as before for the FixedMath golden).
    /// Range ±32767.99998; squared lengths and dot products therefore stay in long Q32.32 (FixVec2, FixMath.Sq).
    /// </summary>
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        public const int FractionBits = 16;
        public const int OneRaw = 1 << FractionBits;
        public static readonly Fix Zero = FromRaw(0);
        public static readonly Fix One = FromRaw(OneRaw);
        public static readonly Fix Half = FromRaw(OneRaw / 2);
        public static readonly Fix MaxValue = FromRaw(int.MaxValue);
        /// <summary>Smallest positive value (1 raw unit = 1/65536).</summary>
        public static readonly Fix Epsilon = FromRaw(1);

        public int Raw { get; }

        Fix(int raw) => Raw = raw;

        public static Fix FromRaw(int raw) => new Fix(raw);
        public static Fix FromInt(int value) => new Fix(Narrow((long)value << FractionBits));
        public static Fix FromRatio(int numerator, int denominator) =>
            new Fix(Narrow(((long)numerator << FractionBits) / denominator));

        /// <summary>Nearest Q16.16 to <paramref name="numerator"/>/<paramref name="denominator"/>; ties away from zero.</summary>
        public static Fix Ratio(long numerator, long denominator)
        {
            if (denominator == 0) throw new DivideByZeroException();
            var n = numerator << FractionBits;
            var q = n / denominator;
            var r = n % denominator;
            if (r != 0 && 2 * (r < 0 ? -r : r) >= (denominator < 0 ? -denominator : denominator)) q += (n < 0) == (denominator < 0) ? 1 : -1;
            return new Fix(Narrow(q));
        }

        public static implicit operator Fix(int value) => FromInt(value);

        // Boundary for the View only. Simulation code must use Raw/integer operations.
        public float ToFloat() => Raw / (float)OneRaw;

        static int Narrow(long value)
        {
#if DEBUG || UNITY_EDITOR
            return checked((int)value);
#else
            return unchecked((int)value);
#endif
        }

        static int AddRaw(int a, int b)
        {
#if DEBUG || UNITY_EDITOR
            return checked(a + b);
#else
            return unchecked(a + b);
#endif
        }

        static int SubRaw(int a, int b)
        {
#if DEBUG || UNITY_EDITOR
            return checked(a - b);
#else
            return unchecked(a - b);
#endif
        }

        public static Fix operator +(Fix a, Fix b) => new Fix(AddRaw(a.Raw, b.Raw));
        public static Fix operator -(Fix a, Fix b) => new Fix(SubRaw(a.Raw, b.Raw));
        public static Fix operator -(Fix a) => new Fix(SubRaw(0, a.Raw));
        public static Fix operator *(Fix a, Fix b) => new Fix(Narrow(((long)a.Raw * b.Raw) >> FractionBits));
        public static Fix operator /(Fix a, Fix b) => new Fix(Narrow(((long)a.Raw << FractionBits) / b.Raw));
        public static Fix operator *(Fix a, int b) => new Fix(Narrow((long)a.Raw * b));
        public static Fix operator *(int a, Fix b) => new Fix(Narrow((long)a * b.Raw));
        public static Fix operator /(Fix a, int b) => new Fix(a.Raw / b);
        public static Fix operator %(Fix a, Fix b) => new Fix(a.Raw % b.Raw);

        public static bool operator ==(Fix a, Fix b) => a.Raw == b.Raw;
        public static bool operator !=(Fix a, Fix b) => a.Raw != b.Raw;
        public static bool operator <(Fix a, Fix b) => a.Raw < b.Raw;
        public static bool operator >(Fix a, Fix b) => a.Raw > b.Raw;
        public static bool operator <=(Fix a, Fix b) => a.Raw <= b.Raw;
        public static bool operator >=(Fix a, Fix b) => a.Raw >= b.Raw;

        public bool Equals(Fix other) => Raw == other.Raw;
        public override bool Equals(object obj) => obj is Fix other && Equals(other);
        public override int GetHashCode() => Raw;
        public int CompareTo(Fix other) => Raw.CompareTo(other.Raw);
        public override string ToString() => "raw:" + Raw;
    }

    /// <summary>Integer-only helpers; Sqrt accepts Q32.32 and returns Q16.16. Rounding is stated per method.</summary>
    public static class FixMath
    {
        public static Fix Min(Fix a, Fix b) => a <= b ? a : b;
        public static Fix Max(Fix a, Fix b) => a >= b ? a : b;
        public static Fix Clamp(Fix value, Fix min, Fix max)
        {
            if (min > max) throw new ArgumentException("min must not exceed max");
            return Max(min, Min(value, max));
        }
        public static Fix Clamp01(Fix value) => value.Raw < 0 ? Fix.Zero : (value.Raw > Fix.OneRaw ? Fix.One : value);
        public static Fix Abs(Fix value) => value.Raw < 0 ? -value : value;
        public static int Sign(Fix value) => value.Raw > 0 ? 1 : (value.Raw < 0 ? -1 : 0);

        /// <summary>Square as Q32.32 (exact, never overflows).</summary>
        public static long Sq(Fix value) => (long)value.Raw * value.Raw;

        /// <summary>Floor of the exact integer square root of a nonnegative Q32.32 raw value.</summary>
        public static Fix Sqrt(long q32Raw)
        {
            if (q32Raw < 0) throw new ArgumentOutOfRangeException(nameof(q32Raw));
            ulong remainder = (ulong)q32Raw;
            ulong root = 0;
            ulong bit = 1UL << 62;
            while (bit > remainder) bit >>= 2;
            while (bit != 0)
            {
                if (remainder >= root + bit)
                {
                    remainder -= root + bit;
                    root = (root >> 1) + bit;
                }
                else root >>= 1;
                bit >>= 2;
            }
            if (root > int.MaxValue) throw new OverflowException("Q16.16 square root exceeds Fix range");
            return Fix.FromRaw((int)root);
        }

        /// <summary>Square root of a nonnegative Fix (floor).</summary>
        public static Fix SqrtFix(Fix value) => Sqrt((long)value.Raw << Fix.FractionBits);

        /// <summary><paramref name="a"/> × num / den with a long intermediate (no overflow for arena values); rounds toward zero.</summary>
        public static Fix MulDiv(Fix a, long num, long den) => Fix.FromRaw(checked((int)((long)a.Raw * num / den)));

        /// <summary>
        /// One fraction of a tick, as the difference of cumulative quotients (toward zero). For a constant rate,
        /// all parts sum to that rate exactly; dividing each part independently would drop the remainder N times.
        /// </summary>
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static Fix SubstepPart(Fix rate, int index, int count)
        {
            if (count <= 0 || index < 0 || index >= count) throw new ArgumentOutOfRangeException(nameof(index));
            var before = (long)rate.Raw * index / count;
            var after = (long)rate.Raw * (index + 1) / count;
            return Fix.FromRaw((int)(after - before));
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
        public static FixVec2 SubstepPart(FixVec2 rate, int index, int count) =>
            new FixVec2(SubstepPart(rate.X, index, count), SubstepPart(rate.Y, index, count));

        /// <summary>
        /// num/den as Fix for two raw values of the same Q format (Q16.16 or Q32.32, any long). Both are shifted right
        /// (arithmetic, deterministic) until they fit 2^46, then divided rounding toward zero; saturates to the Fix range.
        /// </summary>
        public static Fix RawRatio(long num, long den)
        {
            if (den == 0) throw new DivideByZeroException();
            const long limit = 1L << 46;
            while (num > limit || num < -limit || den > limit || den < -limit)
            {
                num >>= 1;
                den >>= 1;
            }
            if (den == 0) return num >= 0 ? Fix.MaxValue : -Fix.MaxValue;
            var q = (num << Fix.FractionBits) / den;
            if (q > int.MaxValue) return Fix.MaxValue;
            if (q < -int.MaxValue) return -Fix.MaxValue;
            return Fix.FromRaw((int)q);
        }

        /// <summary>Largest integer &lt;= value.</summary>
        public static int FloorToInt(Fix value) => value.Raw >> Fix.FractionBits;

        /// <summary>Smallest integer &gt;= value.</summary>
        public static int CeilToInt(Fix value) => -((-value.Raw) >> Fix.FractionBits);

        /// <summary>Nearest integer, ties to even (same as System.Math.Round's default, used for whole HP).</summary>
        public static int RoundToEven(Fix value)
        {
            var floor = value.Raw >> Fix.FractionBits;
            var frac = value.Raw & (Fix.OneRaw - 1);
            if (frac > Fix.OneRaw / 2) return floor + 1;
            if (frac < Fix.OneRaw / 2) return floor;
            return (floor & 1) == 0 ? floor : floor + 1;
        }

        /// <summary>Smallest integer n with n × <paramref name="step"/> &gt;= <paramref name="total"/> (both &gt;= 0, step &gt; 0).</summary>
        public static int CeilDiv(Fix total, Fix step) => (int)(((long)total.Raw + step.Raw - 1) / step.Raw);

        /// <summary>Value wrapped into [0, period).</summary>
        public static Fix Wrap(Fix value, Fix period)
        {
            var r = value.Raw % period.Raw;
            return Fix.FromRaw(r < 0 ? r + period.Raw : r);
        }

        /// <summary>
        /// Decimal text with at most <paramref name="decimals"/> digits (0-2), half away from zero, trailing zeros dropped
        /// ("12.5", "3"). Integer-only, so the HUD shows exactly what the sim holds.
        /// </summary>
        public static string Format(Fix value, int decimals)
        {
            var scale = decimals <= 0 ? 1 : (decimals == 1 ? 10 : 100);
            var scaled = (value.Raw < 0 ? -(long)value.Raw : value.Raw) * scale;
            var whole = (scaled + Fix.OneRaw / 2) >> Fix.FractionBits;   // half away from zero
            var neg = value.Raw < 0 && whole != 0;
            var ip = whole / scale;
            var fp = whole % scale;
            var s = (neg ? "-" : "") + ip.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (fp == 0) return s;
            var digits = scale == 10 ? fp.ToString(System.Globalization.CultureInfo.InvariantCulture)
                                     : fp.ToString("00", System.Globalization.CultureInfo.InvariantCulture).TrimEnd('0');
            return s + "." + digits;
        }
    }
}
