using System;

namespace BallBattle.Sim
{
    /// <summary>
    /// Signed Q16.16 in an int. Division rounds toward zero (C# integer division);
    /// multiplication discards the low 16 bits with an arithmetic right shift,
    /// therefore negative products round toward negative infinity. Addition,
    /// subtraction and narrowing casts are checked in test/Editor builds.
    /// </summary>
    public readonly struct Fix : IEquatable<Fix>, IComparable<Fix>
    {
        public const int FractionBits = 16;
        public const int OneRaw = 1 << FractionBits;
        public static readonly Fix Zero = FromRaw(0);
        public static readonly Fix One = FromRaw(OneRaw);

        public int Raw { get; }

        Fix(int raw) => Raw = raw;

        public static Fix FromRaw(int raw) => new Fix(raw);
        public static Fix FromInt(int value) => new Fix(Narrow((long)value << FractionBits));
        public static Fix FromRatio(int numerator, int denominator) =>
            new Fix(Narrow(((long)numerator << FractionBits) / denominator));

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

    /// <summary>Integer-only helpers; Sqrt accepts Q32.32 and returns Q16.16.</summary>
    public static class FixMath
    {
        public static Fix Min(Fix a, Fix b) => a <= b ? a : b;
        public static Fix Max(Fix a, Fix b) => a >= b ? a : b;
        public static Fix Clamp(Fix value, Fix min, Fix max)
        {
            if (min > max) throw new ArgumentException("min must not exceed max");
            return Max(min, Min(value, max));
        }
        public static Fix Abs(Fix value) => value.Raw < 0 ? -value : value;

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
    }
}
