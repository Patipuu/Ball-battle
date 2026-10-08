using System;
using System.Text;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Generates short, typeable room codes for join-by-code. The alphabet drops the visually
    /// confusable glyphs (0/O, 1/I/L) so a code read off one screen types unambiguously into
    /// another. Six characters over the 31-glyph alphabet is ~8.9e8 combinations — collision is
    /// negligible at this player scale, so v1 does not retry on collision (a future guard if the
    /// active-room count ever grows large).
    /// </summary>
    public static class RoomCodeGenerator
    {
        /// <summary>A–Z minus I/L/O, plus 2–9 (drops 0/1). 31 glyphs.</summary>
        public const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

        public const int CodeLength = 6;

        /// <summary>
        /// A new random code. Takes the RNG so tests are deterministic; production passes a fresh
        /// <see cref="Random"/> via <see cref="New()"/>.
        /// </summary>
        public static string New(Random rng)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            var sb = new StringBuilder(CodeLength);
            for (int i = 0; i < CodeLength; i++)
                sb.Append(Alphabet[rng.Next(Alphabet.Length)]);
            return sb.ToString();
        }

        /// <summary>A new random code from a fresh RNG (production callers).</summary>
        public static string New() => New(new Random());
    }
}
