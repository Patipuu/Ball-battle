using System;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// The room-code contract for join-by-code: fixed length, restricted alphabet, no confusable
    /// glyphs, and deterministic under a seeded RNG (so the rest of the suite and any 2-device
    /// repro can pin a code).
    /// </summary>
    public class RoomCodeGeneratorTests
    {
        [Test]
        public void Code_is_the_fixed_length()
        {
            Assert.AreEqual(RoomCodeGenerator.CodeLength, RoomCodeGenerator.New(new Random(1)).Length);
        }

        [Test]
        public void Code_uses_only_the_allowed_alphabet()
        {
            var rng = new Random(12345);
            for (int i = 0; i < 500; i++)
            {
                string code = RoomCodeGenerator.New(rng);
                foreach (char c in code)
                    Assert.IsTrue(RoomCodeGenerator.Alphabet.IndexOf(c) >= 0,
                        $"'{c}' in '{code}' is outside the allowed alphabet");
            }
        }

        [Test]
        public void Code_never_contains_a_confusable_glyph()
        {
            var confusable = new[] { '0', 'O', '1', 'I', 'L' };
            var rng = new Random(999);
            for (int i = 0; i < 500; i++)
            {
                string code = RoomCodeGenerator.New(rng);
                Assert.AreEqual(-1, code.IndexOfAny(confusable),
                    $"code '{code}' contains a confusable glyph");
            }
        }

        [Test]
        public void Same_seed_produces_the_same_code()
        {
            Assert.AreEqual(RoomCodeGenerator.New(new Random(42)), RoomCodeGenerator.New(new Random(42)));
        }
    }
}
