using System;
using System.Collections.Generic;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// BB-MUSIC1: the whole music set and the jingles, composed in code (deterministic) in ONE key family - A minor and its
    /// relative C major - so a track, the next track and any jingle never clash when they meet. Loops are rendered twice and
    /// the second pass is kept, so reverb and note tails run across the loop point (seamless). Tempos and bar counts:
    /// menu 100 bpm 16 bars, battle 132 bpm 16 bars, boss 144 bpm 16 bars, result 88 bpm 8 bars (a calm bed, never a stutter).
    /// </summary>
    public static class MusicForge
    {
        const int Sr = PlaceholderSfxSynth.SampleRate;
        const double TwoPi = Math.PI * 2.0;

        /// <summary>Every music clip and jingle: (clip id, generator). Ids match AudioIds.</summary>
        public static readonly (string id, Func<float[]> make)[] All =
        {
            ("music_menu", Menu), ("music_battle", Battle), ("music_boss", Boss), ("music_result", Result),
            ("fight_start", FightStart), ("fight_win", FightWin), ("fight_lose", FightLose),
            ("run_win", RunWin), ("run_over", RunOver), ("unlock", Unlock), ("retry_banner", RetryBanner),
        };

        // ================================================================== notes and patterns

        static double Hz(int midi) => 440.0 * Math.Pow(2.0, (midi - 69) / 12.0);

        static readonly Dictionary<char, int> Pc = new Dictionary<char, int> { { 'C', 0 }, { 'D', 2 }, { 'E', 4 }, { 'F', 5 }, { 'G', 7 }, { 'A', 9 }, { 'B', 11 } };

        /// <summary>"A4" "C#5" "Bb3" to MIDI.</summary>
        static int Midi(string n)
        {
            var p = Pc[n[0]];
            var i = 1;
            if (n[i] == '#') { p++; i++; }
            else if (n[i] == 'b') { p--; i++; }
            return (int.Parse(n.Substring(i)) + 1) * 12 + p;
        }

        struct Note
        {
            public double Start, Length;   // in steps (16th notes)
            public int Midi;
            public double Vel;
        }

        /// <summary>
        /// A melody line: space-separated "NOTE:steps" ("A4:2"), "-:steps" for a rest; "NOTE:steps:vel" for a velocity.
        /// Steps are 16th notes. Returns the notes and the total length in steps.
        /// </summary>
        static List<Note> Line(string s, double offset = 0)
        {
            var list = new List<Note>();
            var t = offset;
            foreach (var tok in s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var parts = tok.Split(':');
                var len = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                if (parts[0] != "-")
                    list.Add(new Note { Start = t, Length = len, Midi = Midi(parts[0]), Vel = parts.Length > 2 ? double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture) : 1.0 });
                t += len;
            }
            return list;
        }

        /// <summary>Chord symbols to MIDI triads (root position around the given octave): "Am", "F", "C", "G", "E", "Bb", "Gm", "Dm", "Em".</summary>
        static int[] Chord(string sym, int octave)
        {
            var minor = sym.EndsWith("m");
            var root = sym.TrimEnd('m');
            var r = Midi(root + octave);
            return new[] { r, r + (minor ? 3 : 4), r + 7 };
        }

        // ================================================================== instruments (render into a track buffer)

        sealed class Track
        {
            public readonly float[] Buf;
            public Track(int n) { Buf = new float[n]; }
        }

        static double Env(double t, double len, double a, double d, double s, double r)
        {
            // ADSR on a note of length len (seconds); release after len.
            if (t < 0) return 0;
            double v;
            if (t < a) v = t / a;
            else if (t < a + d) v = 1 - (1 - s) * (t - a) / d;
            else v = s;
            if (t > len) v *= Math.Max(0, 1 - (t - len) / r);
            return v;
        }

        /// <summary>Pulse wave lead/arp (duty 0.125..0.5), soft vibrato after 120 ms.</summary>
        static void Pulse(Track tr, double t0, double len, double f, double vel, double duty, double a, double d, double s, double r, double vib = 0)
        {
            var i0 = (int)(t0 * Sr);
            var n = (int)((len + r) * Sr);
            var ph = 0.0;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                var ff = f * (1 + (t > 0.12 ? vib * Math.Sin(TwoPi * 5.5 * t) : 0));
                ph += ff / Sr;
                ph -= Math.Floor(ph);
                var v = ph < duty ? 1.0 : -1.0;
                tr.Buf[i0 + k] += (float)(v * vel * Env(t, len, a, d, s, r));
            }
        }

        /// <summary>Triangle bass.</summary>
        static void Tri(Track tr, double t0, double len, double f, double vel, double r = 0.03)
        {
            var i0 = (int)(t0 * Sr);
            var n = (int)((len + r) * Sr);
            var ph = 0.0;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                ph += f / Sr;
                ph -= Math.Floor(ph);
                var v = 4 * Math.Abs(ph - 0.5) - 1;
                tr.Buf[i0 + k] += (float)(v * vel * Env(t, len, 0.004, 0.08, 0.8, r));
            }
        }

        /// <summary>Soft pad: three slightly detuned saws per note, slow attack (filtered later).</summary>
        static void Pad(Track tr, double t0, double len, double f, double vel)
        {
            var i0 = (int)(t0 * Sr);
            var n = (int)((len + 0.6) * Sr);
            double p1 = 0, p2 = 0.33, p3 = 0.66;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                p1 += f * 0.997 / Sr; p2 += f / Sr; p3 += f * 1.004 / Sr;
                p1 -= Math.Floor(p1); p2 -= Math.Floor(p2); p3 -= Math.Floor(p3);
                var v = (2 * p1 - 1) + (2 * p2 - 1) + (2 * p3 - 1);
                tr.Buf[i0 + k] += (float)(v / 3 * vel * Env(t, len, 0.25, 0.3, 0.85, 0.6));
            }
        }

        /// <summary>Bell / chime: sine with a quiet 2nd and 3rd partial, exponential decay.</summary>
        static void Bell(Track tr, double t0, double f, double vel, double decay)
        {
            var i0 = (int)(t0 * Sr);
            var n = (int)(decay * 6 * Sr);
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                var e = Math.Exp(-t / decay) * Math.Min(1, t / 0.002);
                var v = Math.Sin(TwoPi * f * t) + 0.3 * Math.Sin(TwoPi * 2 * f * t) * Math.Exp(-t / (decay * 0.4)) + 0.12 * Math.Sin(TwoPi * 3.01 * f * t) * Math.Exp(-t / (decay * 0.25));
                tr.Buf[i0 + k] += (float)(v * vel * e);
            }
        }

        static void Kick(Track tr, double t0, double vel)
        {
            var i0 = (int)(t0 * Sr);
            var n = (int)(0.22 * Sr);
            var ph = 0.0;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                var f = 50 + 110 * Math.Exp(-t / 0.035);
                ph += f / Sr;
                tr.Buf[i0 + k] += (float)(Math.Sin(TwoPi * ph) * Math.Exp(-t / 0.07) * vel + (k < 90 ? 0.25 * vel * Math.Sin(Math.PI * k / 90.0) : 0));
            }
        }

        static void Snare(Track tr, double t0, double vel, int seed)
        {
            var rng = new Random(seed);
            var i0 = (int)(t0 * Sr);
            var n = (int)(0.18 * Sr);
            double lp = 0;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                var noise = rng.NextDouble() * 2 - 1;
                lp += 0.45 * (noise - lp);
                var tone = Math.Sin(TwoPi * 190 * t) * Math.Exp(-t / 0.03);
                tr.Buf[i0 + k] += (float)(vel * (0.7 * lp * Math.Exp(-t / 0.05) + 0.4 * tone));
            }
        }

        static void Hat(Track tr, double t0, double vel, int seed, double decay = 0.025)
        {
            var rng = new Random(seed);
            var i0 = (int)(t0 * Sr);
            var n = (int)(decay * 5 * Sr);
            double prev = 0;
            for (var k = 0; k < n && i0 + k < tr.Buf.Length; k++)
            {
                var t = k / (double)Sr;
                var x = rng.NextDouble() * 2 - 1;
                var hp = x - prev;   // crude high-pass
                prev = x;
                tr.Buf[i0 + k] += (float)(0.5 * hp * vel * Math.Exp(-t / decay));
            }
        }

        // ================================================================== processing

        static void LowPass(float[] s, double f)
        {
            var a = 1 - Math.Exp(-TwoPi * f / Sr);
            double y1 = 0, y2 = 0;
            for (var i = 0; i < s.Length; i++)
            {
                y1 += a * (s[i] - y1);
                y2 += a * (y1 - y2);
                s[i] = (float)y2;
            }
        }

        static void HighPass(float[] s, double f)
        {
            var a = Math.Exp(-TwoPi * f / Sr);
            double x1 = 0, y = 0;
            for (var i = 0; i < s.Length; i++)
            {
                y = a * (y + s[i] - x1);
                x1 = s[i];
                s[i] = (float)y;
            }
        }

        /// <summary>Small Schroeder reverb (4 combs + 2 all-passes), returns the wet signal only.</summary>
        static float[] Reverb(float[] dry, double size = 1.0, double damp = 0.35)
        {
            int[] combs = { (int)(1116 * size), (int)(1188 * size), (int)(1277 * size), (int)(1356 * size) };
            int[] aps = { 556, 441 };
            var wet = new float[dry.Length];
            foreach (var len in combs)
            {
                var buf = new double[len];
                var idx = 0;
                double store = 0;
                for (var i = 0; i < dry.Length; i++)
                {
                    var o = buf[idx];
                    store = o * (1 - damp) + store * damp;
                    buf[idx] = dry[i] * 0.25 + store * 0.8;
                    idx = (idx + 1) % len;
                    wet[i] += (float)o;
                }
            }
            foreach (var len in aps)
            {
                var buf = new double[len];
                var idx = 0;
                for (var i = 0; i < wet.Length; i++)
                {
                    var b = buf[idx];
                    var o = -wet[i] + b;
                    buf[idx] = wet[i] + b * 0.5;
                    idx = (idx + 1) % len;
                    wet[i] = (float)o;
                }
            }
            return wet;
        }

        static void Add(float[] mix, float[] src, double gain)
        {
            for (var i = 0; i < mix.Length && i < src.Length; i++) mix[i] += (float)(src[i] * gain);
        }

        /// <summary>Gentle bus compression (tanh) and normalisation to a target RMS with a peak ceiling.</summary>
        static float[] Master(float[] s, double rmsDb, double ceiling = 0.89)
        {
            double sum = 0;
            foreach (var v in s) sum += v * (double)v;
            var rms = Math.Sqrt(sum / Math.Max(1, s.Length));
            var k = rms > 0 ? Math.Pow(10, rmsDb / 20) / rms : 1;
            for (var i = 0; i < s.Length; i++) s[i] = (float)(Math.Tanh(s[i] * k * 1.1) / 1.1);
            var peak = 0.0;
            foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
            if (peak > ceiling) for (var i = 0; i < s.Length; i++) s[i] = (float)(s[i] * ceiling / peak);
            return s;
        }

        /// <summary>Loop: render 2 passes (the song function writes into buffers of 2x length), keep the second.</summary>
        static float[] SecondHalf(float[] two)
        {
            var half = two.Length / 2;
            var o = new float[half];
            Array.Copy(two, half, o, 0, half);
            return o;
        }

        /// <summary>A one-shot: short fades at both ends.</summary>
        static float[] Fade(float[] s, double fadeOut = 0.05)
        {
            var n = Math.Min(s.Length, (int)(fadeOut * Sr));
            for (var i = 0; i < n; i++) s[s.Length - 1 - i] *= i / (float)n;
            var m = Math.Min(s.Length, Sr / 500);
            for (var i = 0; i < m; i++) s[i] *= i / (float)m;
            return s;
        }

        // ================================================================== song builder

        sealed class Song
        {
            public readonly double Bpm;
            public readonly int Steps;   // song length in 16ths (one pass)
            public readonly int Passes;
            public readonly Track Lead, Arp, Bass, Pads, Bells, Drums;
            public readonly int N;

            public Song(double bpm, int bars, int passes, double tail = 0)
            {
                Bpm = bpm; Steps = bars * 16; Passes = passes;
                N = (int)((Steps * passes * StepSec + tail) * Sr);
                Lead = new Track(N); Arp = new Track(N); Bass = new Track(N); Pads = new Track(N); Bells = new Track(N); Drums = new Track(N);
            }

            public double StepSec => 60.0 / Bpm / 4.0;
            public double T(double step, int pass) => (step + pass * Steps) * StepSec;
        }

        static void LeadLine(Song s, string line, double offset, double vel, double duty = 0.25, double vib = 0.006)
        {
            for (var p = 0; p < s.Passes; p++)
                foreach (var n in Line(line, offset))
                    Pulse(s.Lead, s.T(n.Start, p), n.Length * s.StepSec * 0.92, Hz(n.Midi), vel * n.Vel, duty, 0.006, 0.12, 0.6, 0.08, vib);
        }

        static void BassLine(Song s, string line, double offset, double vel)
        {
            for (var p = 0; p < s.Passes; p++)
                foreach (var n in Line(line, offset))
                    Tri(s.Bass, s.T(n.Start, p), n.Length * s.StepSec * 0.85, Hz(n.Midi), vel * n.Vel);
        }

        /// <summary>One chord per bar from <paramref name="chords"/>, held as a pad.</summary>
        static void PadChords(Song s, string[] chords, int octave, double vel, int barSteps = 16)
        {
            for (var p = 0; p < s.Passes; p++)
                for (var b = 0; b < chords.Length; b++)
                    foreach (var m in Chord(chords[b], octave))
                        Pad(s.Pads, s.T(b * barSteps, p), barSteps * s.StepSec * 0.98, Hz(m), vel);
        }

        /// <summary>Arpeggio over the chords: <paramref name="every"/> steps per note, pattern of chord-tone indices (3 = root an octave up).</summary>
        static void ArpChords(Song s, string[] chords, int octave, double vel, int every, int[] pattern, double duty = 0.125, int barSteps = 16)
        {
            for (var p = 0; p < s.Passes; p++)
                for (var b = 0; b < chords.Length; b++)
                {
                    var c = Chord(chords[b], octave);
                    var tones = new[] { c[0], c[1], c[2], c[0] + 12 };
                    for (int st = 0, i = 0; st < barSteps; st += every, i++)
                    {
                        var m = tones[pattern[i % pattern.Length]];
                        Pulse(s.Arp, s.T(b * barSteps + st, p), every * s.StepSec * 0.6, Hz(m), vel, duty, 0.003, 0.05, 0.35, 0.05);
                    }
                }
        }

        /// <summary>Drum pattern per bar: strings of 16 chars, 'x' hit, 'o' soft hit, '.' rest.</summary>
        static void DrumBars(Song s, int fromBar, int toBar, string kick, string snare, string hat, double vel)
        {
            for (var p = 0; p < s.Passes; p++)
                for (var b = fromBar; b < toBar; b++)
                    for (var st = 0; st < 16; st++)
                    {
                        var t = s.T(b * 16 + st, p);
                        var seed = b * 16 + st + 1;
                        if (kick[st] != '.') Kick(s.Drums, t, vel * (kick[st] == 'x' ? 1 : 0.6));
                        if (snare[st] != '.') Snare(s.Drums, t, vel * (snare[st] == 'x' ? 0.8 : 0.35), seed);
                        if (hat[st] != '.') Hat(s.Drums, t, vel * (hat[st] == 'x' ? 0.5 : 0.28), seed * 7);
                    }
        }

        /// <summary>Mix the song's tracks with fixed per-track gain/filters and a shared reverb send.</summary>
        static float[] Mix(Song s, double lead, double arp, double bass, double pads, double bells, double drums, double verb, double rmsDb, double leadCut = 5000)
        {
            LowPass(s.Lead.Buf, leadCut);
            LowPass(s.Arp.Buf, 4500);
            LowPass(s.Pads.Buf, 1800);
            LowPass(s.Bass.Buf, 1200);
            HighPass(s.Drums.Buf, 30);
            var mix = new float[s.N];
            Add(mix, s.Lead.Buf, lead);
            Add(mix, s.Arp.Buf, arp);
            Add(mix, s.Bass.Buf, bass);
            Add(mix, s.Pads.Buf, pads);
            Add(mix, s.Bells.Buf, bells);
            Add(mix, s.Drums.Buf, drums);
            var send = new float[s.N];
            Add(send, s.Lead.Buf, lead * 0.6);
            Add(send, s.Arp.Buf, arp * 0.8);
            Add(send, s.Pads.Buf, pads * 0.5);
            Add(send, s.Bells.Buf, bells);
            Add(mix, Reverb(send), verb);
            HighPass(mix, 25);
            return Master(mix, rmsDb);
        }

        // ================================================================== the tracks

        /// <summary>MENU: 100 bpm, A minor, calm and friendly. Am F C G | F G Em Am, arp + pad + a singing lead, light beat.</summary>
        public static float[] Menu()
        {
            var s = new Song(100, 16, 2);
            var chords = new[] { "Am", "F", "C", "G", "Am", "F", "C", "G", "F", "G", "Em", "Am", "F", "G", "E", "E" };
            PadChords(s, chords, 3, 0.5);
            ArpChords(s, chords, 4, 0.22, 2, new[] { 0, 1, 2, 3, 2, 1, 2, 1 });
            // bass: root on 1, fifth on the "and" of 3
            var bass = new System.Text.StringBuilder();
            foreach (var c in chords)
            {
                var r = Chord(c, 2)[0];
                bass.Append(Name(r) + ":6 " + Name(r) + ":2 " + Name(r + 7) + ":4 " + Name(r) + ":4 ");
            }
            BassLine(s, bass.ToString(), 0, 0.55);
            // lead enters on bar 5 (bars 1-4 are the intro)
            var leadA = "E5:4 D5:2 C5:2 A4:8 " + "C5:4 D5:2 E5:2 F5:6 E5:2 " + "G5:4 E5:4 C5:4 D5:4 " + "D5:6 B4:2 G4:8 ";
            var leadB = "A4:4 C5:4 F5:6 E5:2 " + "D5:4 B4:4 G5:8 " + "G5:4 E5:2 D5:2 B4:4 G4:4 " + "A4:12 -:4 " + "C5:4 A4:4 F5:4 C5:4 " + "D5:4 G5:4 B4:8 " + "B4:4 D5:4 G#5:4 B5:4 " + "E5:16 ";
            LeadLine(s, leadA + leadB, 4 * 16, 0.32);
            DrumBars(s, 0, 4, "x.......x.......", "................", "..o...o...o...o.", 0.5);
            DrumBars(s, 4, 16, "x.......x..o....", "....x.......x...", "..o...o...o...o.", 0.55);
            return SecondHalf(Mix(s, 0.55, 0.45, 0.8, 0.35, 0, 0.7, 0.25, -15.5));
        }

        /// <summary>BATTLE: 132 bpm, A minor, driving. Eighth-note bass, four-on-the-floor, a short hook.</summary>
        public static float[] Battle()
        {
            var s = new Song(132, 16, 2);
            var chords = new[] { "Am", "Am", "F", "G", "Am", "Am", "F", "E", "F", "G", "Am", "E", "F", "G", "E", "E" };
            PadChords(s, chords, 3, 0.35);
            ArpChords(s, chords, 4, 0.2, 1, new[] { 0, 2, 3, 2 }, 0.125);
            var bass = new System.Text.StringBuilder();
            foreach (var c in chords)
            {
                var r = Chord(c, 2)[0];
                for (var i = 0; i < 8; i++) bass.Append(Name(i % 4 == 3 ? r + 12 : r) + ":2 ");
            }
            BassLine(s, bass.ToString(), 0, 0.6);
            var hook = "A4:2 C5:2 E5:2 A5:4 G5:2 E5:4 " + "D5:2 E5:2 C5:4 A4:8 " + "F5:3 E5:3 D5:2 C5:4 A4:4 " + "B4:3 C5:3 D5:2 E5:8 ";
            var hook2 = "A5:2 G5:2 E5:4 C5:4 E5:4 " + "G5:4 F5:4 E5:4 D5:4 " + "C5:2 D5:2 E5:4 A4:4 C5:4 " + "B4:4 G#4:4 E4:8 ";
            LeadLine(s, hook + hook + hook2 + "A4:2 C5:2 F5:4 E5:4 C5:4 " + "D5:4 B4:4 G4:4 B4:4 " + "G#4:4 B4:4 E5:4 G#5:4 " + "E5:8 -:8 ", 0, 0.3, 0.25, 0.004);
            DrumBars(s, 0, 15, "x...x...x...x...", "....x.......x...", "x.x.x.x.x.x.x.xo", 0.6);
            DrumBars(s, 15, 16, "x...x...x...x.x.", "....x...x.x.xxxx", "x.x.x.x.x.x.x.x.", 0.6);
            return SecondHalf(Mix(s, 0.5, 0.35, 0.85, 0.3, 0, 0.85, 0.18, -14.5));
        }

        /// <summary>BOSS: 144 bpm, A minor with a dark Bb (phrygian) and a big E major turn. Heavier drums, low pulse bass.</summary>
        public static float[] Boss()
        {
            var s = new Song(144, 16, 2);
            var chords = new[] { "Am", "Bb", "Am", "Bb", "Am", "Bb", "Gm", "A", "Dm", "Bb", "Am", "E", "Dm", "Bb", "E", "E" };
            PadChords(s, chords, 3, 0.4);
            ArpChords(s, chords, 3, 0.2, 1, new[] { 0, 1, 2, 1 }, 0.25);
            var bass = new System.Text.StringBuilder();
            foreach (var c in chords)
            {
                var r = Chord(c, 1)[0];
                bass.Append(Name(r) + ":2 " + Name(r) + ":1 " + Name(r) + ":1 " + Name(r + 12) + ":2 " + Name(r) + ":2 " + Name(r) + ":2 " + Name(r) + ":1 " + Name(r) + ":1 " + Name(r + 12) + ":2 " + Name(r + 7) + ":2 ");
            }
            BassLine(s, bass.ToString(), 0, 0.7);
            var riff = "A4:3 A4:1 Bb4:4 A4:4 E4:4 " + "A4:3 C5:1 Bb4:4 A4:2 G4:2 F4:4 ";
            LeadLine(s, riff + riff + riff + "G4:4 Bb4:4 D5:4 C#5:4 " + "C#5:4 E5:4 A5:8 " + "F5:4 D5:4 A4:8 " + "Bb4:4 D5:4 F5:8 " + "E5:4 C5:4 A4:8 " + "G#4:4 B4:4 E5:4 G#5:4 " + "A5:8 F5:8 " + "F5:4 D5:4 Bb4:8 " + "B4:8 G#4:8 " + "E4:16 ", 0, 0.3, 0.5, 0.005);
            DrumBars(s, 0, 14, "x..x..x.x..x..x.", "....x.......x...", "x.x.x.x.x.x.x.x.", 0.65);
            DrumBars(s, 14, 16, "x.x.x.x.x.x.x.x.", "....x...x.xxxxxx", "xxxxxxxxxxxxxxxx", 0.62);
            return SecondHalf(Mix(s, 0.5, 0.3, 0.9, 0.35, 0, 0.9, 0.18, -14, 4200));
        }

        /// <summary>RESULT: 88 bpm, C major (same notes as A minor), a calm bed of pad and slow chimes. No drums, no hook: it sits
        /// under the win/lose jingles and the summary without competing.</summary>
        public static float[] Result()
        {
            var s = new Song(88, 8, 2);
            var chords = new[] { "C", "Am", "F", "G", "C", "Am", "F", "G" };
            PadChords(s, chords, 3, 0.5);
            var bass = new System.Text.StringBuilder();
            foreach (var c in chords) bass.Append(Name(Chord(c, 2)[0]) + ":16 ");
            BassLine(s, bass.ToString(), 0, 0.35);
            // chimes: a slow broken chord, 1 note per beat, soft
            for (var p = 0; p < s.Passes; p++)
                for (var b = 0; b < chords.Length; b++)
                {
                    var c = Chord(chords[b], 5);
                    var order = new[] { c[0], c[2], c[1], c[2] };
                    for (var beat = 0; beat < 4; beat++) Bell(s.Bells, s.T(b * 16 + beat * 4, p), Hz(order[beat]), beat == 0 ? 0.3 : 0.2, 0.5);
                }
            return SecondHalf(Mix(s, 0, 0, 0.6, 0.45, 0.6, 0, 0.35, -19));
        }

        // ================================================================== jingles (all end on A minor or C major)

        static Song Jingle(double bpm, double seconds) => new Song(bpm, 1, 1, Math.Max(0, seconds - 16 * 60.0 / bpm / 4.0));

        static float[] FinishJingle(Song s, double rmsDb) => Fade(Trim(Mix(s, 0.6, 0.5, 0.8, 0.4, 0.6, 0.7, 0.3, rmsDb)), 0.08);

        /// <summary>Cut the silent tail of a jingle (below -50 dB of its peak), keeping 60 ms after the last sound.</summary>
        static float[] Trim(float[] s)
        {
            var peak = 0.0;
            foreach (var v in s) peak = Math.Max(peak, Math.Abs(v));
            var last = s.Length - 1;
            while (last > 0 && Math.Abs(s[last]) < peak * 0.0032) last--;
            var n = Math.Min(s.Length, last + (int)(0.06 * Sr));
            var o = new float[n];
            Array.Copy(s, o, n);
            return o;
        }

        /// <summary>FIGHT START: A minor arpeggio up into a snare roll and a hit.</summary>
        public static float[] FightStart()
        {
            var s = Jingle(150, 0.95);
            foreach (var n in Line("A4:1 C5:1 E5:1 A5:1 C6:1 E6:3"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.5, 0.25, 0.003, 0.05, 0.6, 0.06);
            for (var i = 0; i < 6; i++) Snare(s.Drums, s.T(i, 0), 0.25 + i * 0.08, 900 + i);
            Kick(s.Drums, s.T(8, 0), 1.0);
            Snare(s.Drums, s.T(8, 0), 0.8, 950);
            foreach (var m in Chord("Am", 3)) Pad(s.Pads, s.T(8, 0), 0.3, Hz(m), 0.6);
            return FinishJingle(s, -14);
        }

        /// <summary>FIGHT WIN: bright C major triad up and a held C chord with a chime.</summary>
        public static float[] FightWin()
        {
            var s = Jingle(150, 1.05);
            foreach (var n in Line("C5:1 E5:1 G5:1 C6:5"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.5, 0.25, 0.003, 0.08, 0.7, 0.15, 0.006);
            foreach (var m in Chord("C", 4)) Pad(s.Pads, s.T(3, 0), 0.45, Hz(m), 0.55);
            Tri(s.Bass, s.T(3, 0), 0.45, Hz(Midi("C3")), 0.7);
            Bell(s.Bells, s.T(3, 0), Hz(Midi("G6")), 0.35, 0.25);
            Kick(s.Drums, s.T(3, 0), 0.6);
            return FinishJingle(s, -14);
        }

        /// <summary>FIGHT LOSE: a soft falling line E D C B -> A minor, slower and quieter than the win.</summary>
        public static float[] FightLose()
        {
            var s = Jingle(120, 1.15);
            foreach (var n in Line("E5:2 D5:2 C5:2 B4:2 A4:6"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.4, 0.5, 0.01, 0.1, 0.6, 0.2, 0.008);
            foreach (var m in Chord("Am", 3)) Pad(s.Pads, s.T(8, 0), 0.5, Hz(m), 0.45);
            Tri(s.Bass, s.T(8, 0), 0.5, Hz(Midi("A2")), 0.6);
            return FinishJingle(s, -17);
        }

        /// <summary>RUN WIN: a fanfare G C E G C' over F G C, ending on a big C major chord with sparkles.</summary>
        public static float[] RunWin()
        {
            var s = Jingle(132, 2.3);
            foreach (var n in Line("G4:2 C5:2 E5:2 G5:2 E5:2 G5:2 C6:12"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.5, 0.25, 0.004, 0.1, 0.7, 0.3, 0.006);
            foreach (var (c, at, len) in new[] { ("F", 0, 4), ("G", 4, 6), ("C", 10, 14) })
            {
                foreach (var m in Chord(c, 4)) Pad(s.Pads, s.T(at, 0), len * s.StepSec, Hz(m), 0.5);
                Tri(s.Bass, s.T(at, 0), len * s.StepSec, Hz(Chord(c, 2)[0]), 0.7);
            }
            Kick(s.Drums, s.T(0, 0), 0.6); Kick(s.Drums, s.T(4, 0), 0.6); Kick(s.Drums, s.T(10, 0), 0.9);
            Snare(s.Drums, s.T(10, 0), 0.7, 77);
            double[] sparkle = { 84, 88, 91, 96 };   // C6 E6 G6 C7
            for (var i = 0; i < sparkle.Length; i++) Bell(s.Bells, s.T(11 + i, 0), Hz((int)sparkle[i]), 0.25, 0.35);
            return FinishJingle(s, -15);
        }

        /// <summary>RUN OVER: a slow falling line F E D C over Dm -> Am, ending soft on A minor.</summary>
        public static float[] RunOver()
        {
            var s = Jingle(100, 2.2);
            foreach (var n in Line("F5:3 E5:3 D5:3 C5:3 B4:2 A4:10"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.35, 0.5, 0.02, 0.15, 0.6, 0.35, 0.008);
            foreach (var (c, at, len) in new[] { ("Dm", 0, 6), ("F", 6, 6), ("E", 12, 2), ("Am", 14, 10) })
            {
                foreach (var m in Chord(c, 3)) Pad(s.Pads, s.T(at, 0), len * s.StepSec, Hz(m), 0.45);
                Tri(s.Bass, s.T(at, 0), len * s.StepSec, Hz(Chord(c, 2)[0]), 0.5);
            }
            return FinishJingle(s, -18);
        }

        /// <summary>UNLOCK: three rising chimes E6 G6 C7 with a shimmer.</summary>
        public static float[] Unlock()
        {
            var s = Jingle(150, 1.3);
            Bell(s.Bells, s.T(0, 0), Hz(Midi("E6")), 0.4, 0.3);
            Bell(s.Bells, s.T(1, 0), Hz(Midi("G6")), 0.4, 0.3);
            Bell(s.Bells, s.T(2, 0), Hz(Midi("C7")), 0.5, 0.5);
            foreach (var m in Chord("C", 5)) Pad(s.Pads, s.T(2, 0), 0.4, Hz(m), 0.3);
            return FinishJingle(s, -16);
        }

        /// <summary>RETRY: a two-note call A4 -> E5 and a held A minor, "one more try".</summary>
        public static float[] RetryBanner()
        {
            var s = Jingle(120, 1.2);
            foreach (var n in Line("A4:2 E5:6"))
                Pulse(s.Lead, s.T(n.Start, 0), n.Length * s.StepSec, Hz(n.Midi), 0.45, 0.25, 0.004, 0.1, 0.7, 0.25, 0.006);
            foreach (var m in Chord("Am", 3)) Pad(s.Pads, s.T(2, 0), 0.6, Hz(m), 0.45);
            Snare(s.Drums, s.T(0, 0), 0.4, 31); Snare(s.Drums, s.T(1, 0), 0.5, 32); Kick(s.Drums, s.T(2, 0), 0.8);
            return FinishJingle(s, -15);
        }

        static readonly string[] Names = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        static string Name(int midi) => Names[midi % 12] + (midi / 12 - 1);
    }
}
