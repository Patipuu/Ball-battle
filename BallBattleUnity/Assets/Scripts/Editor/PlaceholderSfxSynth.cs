using System;
using System.IO;
using System.Text;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Tiny sfxr-style synth for placeholder sounds: mono float samples in [-1, 1] at 44.1 kHz, plus a 16-bit WAV writer.
    /// Deterministic (fixed noise seed) so regenerating gives identical files.
    /// </summary>
    public static class PlaceholderSfxSynth
    {
        public const int SampleRate = 44100;
        const float TwoPi = (float)(Math.PI * 2.0);

        static float[] Buffer(float seconds) => new float[(int)(seconds * SampleRate)];
        static float Env(float t, float attack, float decay) => t < attack ? t / attack : (float)Math.Exp(-(t - attack) / decay);

        /// <summary>"Clack": very short noise burst + bright decaying click. The signature hit sound.</summary>
        public static float[] Hit()
        {
            var s = Buffer(0.09f);
            var rng = new Random(1);
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)SampleRate;
                var noise = (float)(rng.NextDouble() * 2 - 1) * Env(t, 0.0005f, 0.006f);
                var click = ((float)Math.Sin(TwoPi * 950f * t) + 0.5f * (float)Math.Sin(TwoPi * 1900f * t)) * Env(t, 0.001f, 0.022f);
                s[i] = 0.55f * noise + 0.5f * click;
            }
            return Normalize(s, 0.9f);
        }

        /// <summary>Metallic ring: inharmonic partials with long decay.</summary>
        public static float[] Parry()
        {
            var s = Buffer(0.3f);
            var rng = new Random(2);
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)SampleRate;
                var ring = (float)(Math.Sin(TwoPi * 1520f * t) + 0.7 * Math.Sin(TwoPi * 2390f * t) + 0.45 * Math.Sin(TwoPi * 3910f * t)) * Env(t, 0.001f, 0.08f);
                var tick = (float)(rng.NextDouble() * 2 - 1) * Env(t, 0.0003f, 0.004f);
                s[i] = 0.5f * ring + 0.6f * tick;
            }
            return Normalize(s, 0.8f);
        }

        /// <summary>Soft low thump for wall/ball bounces (played quietly).</summary>
        public static float[] Wall()
        {
            var s = Buffer(0.06f);
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)SampleRate;
                var f = 140f - 60f * t / 0.06f;
                s[i] = (float)Math.Sin(TwoPi * f * t) * Env(t, 0.001f, 0.015f);
            }
            return Normalize(s, 0.7f);
        }

        /// <summary>Knockout: noise burst with falling square tone.</summary>
        public static float[] Death()
        {
            var s = Buffer(0.6f);
            var rng = new Random(3);
            var phase = 0.0;
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)SampleRate;
                var f = 320.0 * Math.Pow(0.15, t / 0.6);
                phase += f / SampleRate;
                var square = (phase % 1.0) < 0.5 ? 1f : -1f;
                var noise = (float)(rng.NextDouble() * 2 - 1);
                s[i] = (0.35f * square + 0.65f * noise) * Env(t, 0.002f, 0.16f);
            }
            return Normalize(s, 0.85f);
        }

        /// <summary>Victory jingle: square-wave arpeggio C5 E5 G5 C6.</summary>
        public static float[] Win()
        {
            float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };
            const float noteLen = 0.09f;
            var s = Buffer(noteLen * notes.Length + 0.25f);
            for (var i = 0; i < s.Length; i++)
            {
                var t = i / (float)SampleRate;
                var n = Math.Min(notes.Length - 1, (int)(t / noteLen));
                var local = t - n * noteLen;
                var tail = n == notes.Length - 1 ? 0.12f : 0.05f;
                var square = (t * notes[n]) % 1f < 0.5f ? 1f : -1f;
                s[i] = square * Env(local, 0.003f, tail);
            }
            return Normalize(s, 0.5f);
        }

        /// <summary>Scale to <paramref name="peak"/> and add 2 ms fade-in / 4 ms fade-out so clips never click at their ends.</summary>
        public static float[] Normalize(float[] s, float peak)
        {
            var fadeIn = Math.Min(s.Length, SampleRate * 2 / 1000);
            var fadeOut = Math.Min(s.Length, SampleRate * 4 / 1000);
            for (var i = 0; i < fadeIn; i++) s[i] *= i / (float)fadeIn;
            for (var i = 0; i < fadeOut; i++) s[s.Length - 1 - i] *= i / (float)fadeOut;
            var max = 0f;
            foreach (var v in s) max = Math.Max(max, Math.Abs(v));
            if (max <= 0f) return s;
            var k = peak / max;
            for (var i = 0; i < s.Length; i++) s[i] *= k;
            return s;
        }

        /// <summary>16-bit PCM mono WAV.</summary>
        public static byte[] ToWav(float[] samples)
        {
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms))
            {
                var dataBytes = samples.Length * 2;
                w.Write(Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataBytes);
                w.Write(Encoding.ASCII.GetBytes("WAVE"));
                w.Write(Encoding.ASCII.GetBytes("fmt "));
                w.Write(16);                 // PCM chunk size
                w.Write((short)1);           // PCM
                w.Write((short)1);           // mono
                w.Write(SampleRate);
                w.Write(SampleRate * 2);     // byte rate
                w.Write((short)2);           // block align
                w.Write((short)16);          // bits per sample
                w.Write(Encoding.ASCII.GetBytes("data"));
                w.Write(dataBytes);
                foreach (var v in samples)
                    w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, v)) * short.MaxValue));
                w.Flush();
                return ms.ToArray();
            }
        }
    }
}
