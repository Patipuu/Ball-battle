using System;

namespace BallBattle.Sim
{
    /// <summary>FNV-1a 64-bit over exact bit patterns, for replay/determinism checks.</summary>
    public static class SimHash
    {
        public const ulong Seed = 14695981039346656037UL;
        const ulong Prime = 1099511628211UL;

        public static ulong Mix(ulong h, uint value)
        {
            for (var i = 0; i < 4; i++)
            {
                h ^= (byte)(value >> (i * 8));
                h *= Prime;
            }
            return h;
        }

        public static ulong Mix(ulong h, int value) => Mix(h, unchecked((uint)value));

        public static ulong Mix(ulong h, float value) => Mix(h, BitConverter.SingleToInt32Bits(value));

        public static ulong Mix(ulong h, bool value) => Mix(h, value ? 1 : 0);

        public static ulong Mix(ulong h, Vec2 v) => Mix(Mix(h, v.X), v.Y);

        /// <summary>Mixes a string's UTF-16 code units (no allocation).</summary>
        public static ulong Mix(ulong h, string s)
        {
            if (s == null) return Mix(h, -1);
            h = Mix(h, s.Length);
            for (var i = 0; i < s.Length; i++) h = Mix(h, (int)s[i]);
            return h;
        }
    }
}
