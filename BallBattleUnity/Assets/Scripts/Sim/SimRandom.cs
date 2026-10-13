using System;

namespace BallBattle.Sim
{
    /// <summary>
    /// xorshift32 RNG. The only randomness source allowed in Sim (System.Random's algorithm is not
    /// guaranteed stable across runtimes). Same seed → same sequence on every build.
    /// </summary>
    public sealed class SimRandom
    {
        public uint State { get; private set; }

        public SimRandom(uint seed)
        {
            // xorshift has an all-zero fixed point; remap seed 0 to a fixed non-zero constant.
            State = seed == 0 ? 0x9E3779B9u : seed;
        }

        public uint NextUInt()
        {
            var x = State;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            State = x;
            return x;
        }

        /// <summary>Uniform integer in [min, max); rejection removes modulo bias.</summary>
        public int RangeInt(int min, int max)
        {
            if (min >= max) throw new ArgumentOutOfRangeException(nameof(max));
            var width = (uint)((long)max - min);
            return (int)((long)min + SampleBelow(width));
        }

        /// <summary>Uniform Q16.16 raw value in [min, max); all arithmetic is integer.</summary>
        public Fix RangeFix(Fix min, Fix max)
        {
            if (min >= max) throw new ArgumentOutOfRangeException(nameof(max));
            var width = (uint)((long)max.Raw - min.Raw);
            return Fix.FromRaw((int)((long)min.Raw + SampleBelow(width)));
        }

        uint SampleBelow(uint width)
        {
            var threshold = unchecked(0u - width) % width;
            uint value;
            do value = NextUInt(); while (value < threshold);
            return value % width;
        }

        /// <summary>Uniform in [0, 1) with 24-bit resolution (exact in float).</summary>
        public float NextFloat01() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * NextFloat01();

        public bool NextBool() => (NextUInt() & 0x80000000u) != 0;
    }
}
