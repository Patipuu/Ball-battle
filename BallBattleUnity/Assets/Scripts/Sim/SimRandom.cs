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

        /// <summary>Uniform in [0, 1) with 16-bit resolution: one NextUInt, its top 16 bits as Q16.16 raw.</summary>
        public Fix NextFix01() => Fix.FromRaw((int)(NextUInt() >> 16));

        /// <summary>
        /// Legacy simulation Range: one draw, its upper 24 bits interpolated across the range, rounded to nearest
        /// raw value (ties away). Keep this separate from the unbiased integer RangeFix: modulo/rejection changes
        /// both the spawn mapping and sometimes the draw count compared with the original float simulation.
        /// An empty range still consumes that draw, as the old min + (max-min)*NextFloat01 did.
        /// </summary>
        public Fix RangeFixOrMin(Fix min, Fix max)
        {
            var fraction = NextUInt() >> 8;
            if (min >= max) return min;
            const long scale = 1L << 24;
            var numerator = (long)min.Raw * scale + ((long)max.Raw - min.Raw) * fraction;
            var raw = numerator < 0 ? -((-numerator + scale / 2) / scale) : (numerator + scale / 2) / scale;
            return Fix.FromRaw((int)(raw >= max.Raw ? (long)max.Raw - 1 : raw));
        }

        public bool NextBool() => (NextUInt() & 0x80000000u) != 0;
    }
}
