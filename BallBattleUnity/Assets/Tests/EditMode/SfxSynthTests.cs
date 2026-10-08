using System;
using System.Text;
using BallBattle.EditorTools;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;

namespace BallBattle.Tests
{
    public class SfxSynthTests
    {
        static readonly Func<float[]>[] All =
        {
            PlaceholderSfxSynth.Hit, PlaceholderSfxSynth.Parry, PlaceholderSfxSynth.Wall,
            PlaceholderSfxSynth.Death, PlaceholderSfxSynth.Win
        };

        [Test]
        public void SoundsAreShortNonSilentAndNeverClip()
        {
            foreach (var make in All)
            {
                var s = make();
                Assert.That(s.Length, Is.InRange(PlaceholderSfxSynth.SampleRate / 50, PlaceholderSfxSynth.SampleRate));
                var peak = 0f;
                foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
                Assert.That(peak, Is.GreaterThan(0.3f).And.LessThanOrEqualTo(1f));
            }
        }

        [Test]
        public void ClipsStartAndEndSilentSoTheyNeverClick()
        {
            foreach (var make in All)
            {
                var s = make();
                Assert.That(Math.Abs(s[0]), Is.LessThan(1e-4f));
                Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(1e-3f));
            }
        }

        [Test]
        public void SynthIsDeterministic()
        {
            Assert.That(PlaceholderSfxSynth.Hit(), Is.EqualTo(PlaceholderSfxSynth.Hit()));
        }

        [Test]
        public void WavHeaderIsValidPcm16Mono()
        {
            var samples = PlaceholderSfxSynth.Wall();
            var wav = PlaceholderSfxSynth.ToWav(samples);
            Assert.That(Encoding.ASCII.GetString(wav, 0, 4), Is.EqualTo("RIFF"));
            Assert.That(Encoding.ASCII.GetString(wav, 8, 4), Is.EqualTo("WAVE"));
            Assert.That(BitConverter.ToInt16(wav, 22), Is.EqualTo(1), "mono");
            Assert.That(BitConverter.ToInt32(wav, 24), Is.EqualTo(PlaceholderSfxSynth.SampleRate));
            Assert.That(BitConverter.ToInt16(wav, 34), Is.EqualTo(16), "bits");
            Assert.That(BitConverter.ToInt32(wav, 40), Is.EqualTo(samples.Length * 2), "data size");
            Assert.That(wav.Length, Is.EqualTo(44 + samples.Length * 2));
        }

        [Test]
        public void SfxLibraryHasEveryClip()
        {
            var lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            Assert.That(lib, Is.Not.Null, "run BallBattle/Build Scenes");
            Assert.That(lib.Hit, Is.Not.Null);
            Assert.That(lib.Parry, Is.Not.Null);
            Assert.That(lib.Wall, Is.Not.Null);
            Assert.That(lib.Death, Is.Not.Null);
            Assert.That(lib.Win, Is.Not.Null);
        }
    }
}
