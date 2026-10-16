using System;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// BB-SFX4: the match and feedback cue set, synthesised (deterministic, no samples). Design rules:
    /// hits are SHORT (60..220 ms) and WARM (energy mostly 150 Hz..4 kHz, a low-pass on every layer) so many of them in a row
    /// read as distinct thuds instead of a hiss; every weapon keeps one recognisable colour (blade ring, fang tick, pike wood,
    /// brawler boom, volley tick, venom squelch, aegis gong, hive pop). Feedback cues (tap, chest, level up, tree) are
    /// tonal and sit in one key (C major pentatonic) so they never clash with each other.
    /// Every function returns mono samples at <see cref="PlaceholderSfxSynth.SampleRate"/>, normalised.
    /// </summary>
    public static class SfxForge
    {
        const int Sr = PlaceholderSfxSynth.SampleRate;
        const double TwoPi = Math.PI * 2.0;

        /// <summary>Every forged cue: (clip id, generator). Clip ids match AudioIds.</summary>
        public static readonly (string id, Func<float[]> make)[] All =
        {
            ("hit_blade", HitBlade), ("hit_fang", HitFang), ("hit_pike", HitPike), ("hit_brawler", HitBrawler),
            ("hit_volley", HitVolley), ("hit_venom", HitVenom), ("hit_aegis", HitAegis), ("hit_rig", HitRig),
            ("spike_hit", SpikeHit),
            ("tap_strike", TapStrike), ("tap_perfect", TapPerfect), ("tap_miss", TapMiss),
            ("chest_open", ChestOpen), ("chest_item", ChestItem), ("level_up", LevelUp),
            ("tree_add", TreeAdd), ("tree_reset", TreeReset),
            ("hit_chorus", HitChorus), ("hit_serpent", HitSerpent),
            ("chorus_note", ChorusNote), ("chorus_unity", ChorusUnity), ("prey_eat", PreyEat), ("tail_cut", TailCut),
            ("skill_chorus_hymn", SkillHymn), ("skill_serpent_strike", SkillStrike),
        };

        // ------------------------------------------------------------------ building blocks

        sealed class Biquad
        {
            double b0, b1, b2, a1, a2, x1, x2, y1, y2;

            public static Biquad LowPass(double f, double q = 0.707) => Make(f, q, 0);
            public static Biquad BandPass(double f, double q) => Make(f, q, 1);
            public static Biquad HighPass(double f, double q = 0.707) => Make(f, q, 2);

            static Biquad Make(double f, double q, int kind)
            {
                var w = TwoPi * Math.Min(f, Sr * 0.45) / Sr;
                var cs = Math.Cos(w);
                var alpha = Math.Sin(w) / (2 * q);
                double b0, b1, b2;
                switch (kind)
                {
                    case 0: b0 = (1 - cs) / 2; b1 = 1 - cs; b2 = (1 - cs) / 2; break;
                    case 1: b0 = alpha; b1 = 0; b2 = -alpha; break;
                    default: b0 = (1 + cs) / 2; b1 = -(1 + cs); b2 = (1 + cs) / 2; break;
                }
                var a0 = 1 + alpha;
                return new Biquad { b0 = b0 / a0, b1 = b1 / a0, b2 = b2 / a0, a1 = -2 * cs / a0, a2 = (1 - alpha) / a0 };
            }

            /// <summary>Retune keeping the state (for sweeps).</summary>
            public void Retune(Biquad other)
            {
                b0 = other.b0; b1 = other.b1; b2 = other.b2; a1 = other.a1; a2 = other.a2;
            }

            public double Run(double x)
            {
                var y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2;
                x2 = x1; x1 = x; y2 = y1; y1 = y;
                return y;
            }
        }

        static float[] Buf(double seconds) => new float[(int)(seconds * Sr)];

        /// <summary>Attack-then-exponential-decay envelope.</summary>
        static double Env(double t, double attack, double decay) => t < 0 ? 0 : t < attack ? t / attack : Math.Exp(-(t - attack) / decay);

        /// <summary>Sine "body" whose pitch glides exponentially from f0 to f1 over <paramref name="glide"/> s.</summary>
        static void Body(float[] s, double gain, double f0, double f1, double glide, double attack, double decay, double start = 0)
        {
            var phase = 0.0;
            for (var i = (int)(start * Sr); i < s.Length; i++)
            {
                var t = i / (double)Sr - start;
                var f = t < glide ? f0 * Math.Pow(f1 / f0, t / glide) : f1;
                phase += f / Sr;
                s[i] += (float)(gain * Math.Sin(TwoPi * phase) * Env(t, attack, decay));
            }
        }

        /// <summary>Noise through a band-pass (centre may sweep from fc0 to fc1).</summary>
        static void Noise(float[] s, int seed, double gain, double fc0, double fc1, double q, double attack, double decay, double start = 0)
        {
            var rng = new Random(seed);
            var bp = Biquad.BandPass(fc0, q);
            var len = s.Length;
            var norm = Math.Sqrt(q) * 1.5;   // keep loudness roughly constant across Q
            for (var i = (int)(start * Sr); i < len; i++)
            {
                var t = i / (double)Sr - start;
                if ((i & 31) == 0 && fc0 != fc1) bp.Retune(Biquad.BandPass(fc0 * Math.Pow(fc1 / fc0, Math.Min(1, t / (decay * 4))), q));
                var x = rng.NextDouble() * 2 - 1;
                s[i] += (float)(gain * norm * bp.Run(x) * Env(t, attack, decay));
            }
        }

        /// <summary>Partials (frequency, relative gain, decay) - bells, rings, gongs.</summary>
        static void Partials(float[] s, double gain, double attack, double start, params (double f, double g, double decay)[] parts)
        {
            for (var i = (int)(start * Sr); i < s.Length; i++)
            {
                var t = i / (double)Sr - start;
                var v = 0.0;
                foreach (var p in parts) v += p.g * Math.Sin(TwoPi * p.f * t) * Env(t, attack, p.decay);
                s[i] += (float)(gain * v);
            }
        }

        /// <summary>Soft square (sum of 4 odd harmonics) - a gentle chiptune voice.</summary>
        static void Chip(float[] s, double gain, double f, double start, double attack, double decay, double vibrato = 0)
        {
            for (var i = (int)(start * Sr); i < s.Length; i++)
            {
                var t = i / (double)Sr - start;
                var ff = f * (1 + vibrato * Math.Sin(TwoPi * 6 * t));
                var v = Math.Sin(TwoPi * ff * t) + Math.Sin(TwoPi * 3 * ff * t) / 3 + Math.Sin(TwoPi * 5 * ff * t) / 5 * 0.6 + Math.Sin(TwoPi * 7 * ff * t) / 7 * 0.3;
                s[i] += (float)(gain * v * Env(t, attack, decay));
            }
        }

        /// <summary>Short broadband click shaped by a low-pass: the "contact" of a hit.</summary>
        static void Click(float[] s, int seed, double gain, double lowpass, double decay = 0.0025, double start = 0)
        {
            var rng = new Random(seed);
            var lp = Biquad.LowPass(lowpass);
            for (var i = (int)(start * Sr); i < s.Length; i++)
            {
                var t = i / (double)Sr - start;
                if (t > decay * 8) break;
                s[i] += (float)(gain * lp.Run(rng.NextDouble() * 2 - 1) * Env(t, 0.0002, decay));
            }
        }

        static float[] LowPass(float[] s, double f)
        {
            var a = Biquad.LowPass(f);
            var b = Biquad.LowPass(f);
            for (var i = 0; i < s.Length; i++) s[i] = (float)b.Run(a.Run(s[i]));
            return s;
        }

        static float[] HighPass(float[] s, double f)
        {
            var a = Biquad.HighPass(f);
            for (var i = 0; i < s.Length; i++) s[i] = (float)a.Run(s[i]);
            return s;
        }

        /// <summary>Soft clip (tanh) for weight without harsh peaks.</summary>
        static float[] Saturate(float[] s, double drive)
        {
            var k = Math.Tanh(drive);
            for (var i = 0; i < s.Length; i++) s[i] = (float)(Math.Tanh(drive * s[i]) / k);
            return s;
        }

        /// <summary>Tiny room: three short feedback-free echoes, low-passed. Tonal cues only: on short hits the echoes read as a flam.</summary>
        static float[] Room(float[] s, double mix)
        {
            var o = (float[])s.Clone();
            int[] taps = { (int)(0.023 * Sr), (int)(0.037 * Sr), (int)(0.053 * Sr) };
            double[] gains = { 0.5, 0.35, 0.22 };
            var lp = Biquad.LowPass(3500);
            var wet = new float[s.Length];
            for (var i = 0; i < s.Length; i++)
            {
                var v = 0.0;
                for (var k = 0; k < taps.Length; k++) if (i >= taps[k]) v += gains[k] * s[i - taps[k]];
                wet[i] = (float)lp.Run(v);
            }
            for (var i = 0; i < s.Length; i++) o[i] += (float)(mix * wet[i]);
            return o;
        }

        static float[] Done(float[] s, float peak) => PlaceholderSfxSynth.Normalize(s, peak);

        // C major pentatonic notes used by the tonal cues.
        const double C5 = 523.25, D5 = 587.33, E5 = 659.25, G5 = 783.99, A5 = 880.00;
        const double C6 = 1046.50, D6 = 1174.66, E6 = 1318.51, G6 = 1567.98, A6 = 1760.00, C7 = 2093.00;

        // ------------------------------------------------------------------ hits (one per weapon)

        /// <summary>EMBER (blade): a short steel "shing" on a firm thud.</summary>
        public static float[] HitBlade()
        {
            var s = Buf(0.17);
            Click(s, 11, 0.5, 5000);
            Body(s, 0.55, 280, 170, 0.04, 0.001, 0.03);
            Noise(s, 12, 0.6, 3000, 2200, 3, 0.001, 0.022);
            Partials(s, 0.3, 0.001, 0.002, (1870, 1, 0.05), (2810, 0.6, 0.035), (3740, 0.25, 0.02));
            return Done(LowPass(s, 6000), 0.85f);
        }

        /// <summary>FANG: a tiny dry bite "tk" (it fires in fast combos, so it is the shortest and quietest colour).</summary>
        public static float[] HitFang()
        {
            var s = Buf(0.075);
            Click(s, 21, 0.6, 4000, 0.002);
            Body(s, 0.5, 360, 230, 0.02, 0.0008, 0.014);
            Noise(s, 22, 0.7, 2300, 1800, 2.5, 0.0008, 0.012);
            return Done(LowPass(s, 5000), 0.8f);
        }

        /// <summary>ANGLER (pike): a woody "thwack" with a plucked line.</summary>
        public static float[] HitPike()
        {
            var s = Buf(0.16);
            Click(s, 31, 0.45, 3500);
            Body(s, 0.4, 240, 170, 0.04, 0.001, 0.028);
            Noise(s, 32, 0.8, 1500, 1000, 3, 0.001, 0.028);
            Chip(s, 0.22, 330, 0.004, 0.002, 0.045);
            return Done(LowPass(s, 4200), 0.85f);
        }

        /// <summary>WRECKER (brawler): a heavy, saturated low "boom".</summary>
        public static float[] HitBrawler()
        {
            var s = Buf(0.24);
            Click(s, 41, 0.7, 3000, 0.005);
            Body(s, 0.55, 190, 110, 0.07, 0.002, 0.05);
            Noise(s, 42, 1.3, 1200, 550, 1.2, 0.001, 0.045);
            Saturate(s, 3.0);
            return Done(HighPass(LowPass(s, 3000), 110), 0.9f);
        }

        /// <summary>SPARKLER (volley): a light arrow "tup".</summary>
        public static float[] HitVolley()
        {
            var s = Buf(0.085);
            Click(s, 51, 0.5, 4500, 0.0018);
            Body(s, 0.6, 420, 260, 0.02, 0.0008, 0.018);
            Noise(s, 52, 0.3, 2800, 2000, 5, 0.0008, 0.012);
            return Done(LowPass(s, 5200), 0.8f);
        }

        /// <summary>SPORE (venom): a wet "squelch" (band-passed noise sweeping down).</summary>
        public static float[] HitVenom()
        {
            var s = Buf(0.17);
            Click(s, 61, 0.35, 2500, 0.003);
            Body(s, 0.35, 240, 160, 0.05, 0.001, 0.03);
            Noise(s, 62, 1.0, 1900, 650, 5, 0.004, 0.035);
            return Done(LowPass(s, 3800), 0.85f);
        }

        /// <summary>MIRROR (aegis): a short shield "gong".</summary>
        public static float[] HitAegis()
        {
            var s = Buf(0.24);
            Click(s, 71, 0.4, 3500);
            Body(s, 0.35, 200, 150, 0.04, 0.001, 0.04);
            Partials(s, 0.45, 0.002, 0.001, (523, 1, 0.09), (1307, 0.7, 0.06), (2093, 0.45, 0.04), (787, 0.4, 0.07));
            return Done(LowPass(s, 4500), 0.85f);
        }

        /// <summary>HIVE (rig): a small turret-shot "pop".</summary>
        public static float[] HitRig()
        {
            var s = Buf(0.065);
            Click(s, 81, 0.5, 4200, 0.0018);
            Body(s, 0.6, 520, 310, 0.015, 0.0006, 0.014);
            Noise(s, 82, 0.3, 3000, 2400, 4, 0.0006, 0.008);
            return Done(LowPass(s, 5200), 0.8f);
        }

        /// <summary>Spike wall: a metal "clank" (two inharmonic partials) over a thud.</summary>
        public static float[] SpikeHit()
        {
            var s = Buf(0.16);
            Click(s, 91, 0.5, 4500);
            Body(s, 0.35, 220, 150, 0.03, 0.001, 0.025);
            Partials(s, 0.45, 0.001, 0.001, (930, 1, 0.06), (1480, 0.7, 0.045), (2390, 0.35, 0.03));
            return Done(LowPass(s, 5000), 0.85f);
        }

        // ------------------------------------------------------------------ BB-14 tribute balls

        /// <summary>BGLGXLQ hit: a soft thud with a short bell (C6) on top.</summary>
        public static float[] HitChorus()
        {
            var s = Buf(0.16);
            Click(s, 181, 0.4, 4500);
            Body(s, 0.45, 240, 160, 0.03, 0.001, 0.028);
            Partials(s, 0.3, 0.001, 0.002, (C6, 1, 0.06), (C6 * 2, 0.25, 0.03));
            return Done(LowPass(s, 6000), 0.85f);
        }

        /// <summary>Snake bite: a dry snap (two quick clicks) over a small thud.</summary>
        public static float[] HitSerpent()
        {
            var s = Buf(0.09);
            Click(s, 191, 0.7, 4000, 0.002);
            Click(s, 192, 0.5, 3500, 0.002, 0.018);
            Body(s, 0.45, 300, 190, 0.02, 0.0008, 0.018);
            Noise(s, 193, 0.5, 2500, 1800, 3, 0.0008, 0.012);
            return Done(LowPass(s, 5200), 0.8f);
        }

        /// <summary>A colour lights: one bell note (C6). The director plays it at C D E G A by pitch, so a full ring is a phrase.</summary>
        public static float[] ChorusNote()
        {
            var s = Buf(0.35);
            Partials(s, 0.6, 0.002, 0, (C6, 1, 0.14), (C6 * 2, 0.3, 0.06), (C6 * 3, 0.08, 0.03));
            return Done(Room(LowPass(s, 8000), 0.2), 0.7f);
        }

        /// <summary>UNITY: a bright C-major chord swell (C5 E5 G5 C6) with a soft whoosh.</summary>
        public static float[] ChorusUnity()
        {
            var s = Buf(0.8);
            Noise(s, 201, 0.4, 900, 3000, 1.2, 0.05, 0.12);
            foreach (var f in new[] { C5, E5, G5, C6 }) Chip(s, 0.16, f, 0.0, 0.02, 0.32, 0.004);
            Partials(s, 0.25, 0.005, 0.05, (C7, 1, 0.2), (E6 * 2, 0.5, 0.15));
            return Done(Room(LowPass(s, 7500), 0.3), 0.8f);
        }

        /// <summary>Skill HYMN: two quick bell notes rising a fifth (C6 then G6) over a soft pad swell.</summary>
        public static float[] SkillHymn()
        {
            var s = Buf(0.5);
            foreach (var f in new[] { C5, E5, G5 }) Chip(s, 0.1, f, 0.0, 0.08, 0.25);
            Partials(s, 0.4, 0.002, 0.0, (C6, 1, 0.12), (C6 * 2, 0.2, 0.05));
            Partials(s, 0.45, 0.002, 0.1, (G6, 1, 0.18), (G6 * 2, 0.2, 0.06));
            return Done(Room(LowPass(s, 8000), 0.25), 0.75f);
        }

        /// <summary>Skill STRIKE: a hiss rising into a snap (the lunge).</summary>
        public static float[] SkillStrike()
        {
            var s = Buf(0.4);
            Noise(s, 231, 0.7, 2500, 5000, 2, 0.15, 0.08);
            Click(s, 232, 0.8, 4500, 0.003, 0.22);
            Body(s, 0.5, 260, 160, 0.03, 0.001, 0.03, 0.22);
            return Done(LowPass(s, 6500), 0.8f);
        }

        /// <summary>Snake eats: a short rising "gulp" blip (G5 to C6).</summary>
        public static float[] PreyEat()
        {
            var s = Buf(0.14);
            Body(s, 0.8, G5, C6, 0.06, 0.002, 0.04);
            Click(s, 211, 0.15, 3000, 0.002);
            return Done(LowPass(s, 6000), 0.7f);
        }

        /// <summary>Tail cut: a short crack and a falling tone.</summary>
        public static float[] TailCut()
        {
            var s = Buf(0.2);
            Click(s, 221, 0.8, 5000, 0.003);
            Noise(s, 222, 0.6, 2400, 900, 2.5, 0.001, 0.03);
            Body(s, 0.5, 520, 180, 0.12, 0.002, 0.06);
            return Done(LowPass(s, 6000), 0.8f);
        }

        // ------------------------------------------------------------------ generic match sounds (PlaceholderSfxSynth)

        /// <summary>Generic hit (a ball with no hit clip of its own): a neutral thud-click.</summary>
        public static float[] Hit()
        {
            var s = Buf(0.1);
            Click(s, 101, 0.5, 4000);
            Body(s, 0.5, 260, 170, 0.03, 0.001, 0.025);
            Noise(s, 102, 0.6, 2000, 1500, 3, 0.001, 0.018);
            return Done(LowPass(s, 5000), 0.85f);
        }

        /// <summary>Parry: a clear two-note bell "ting" (not a hiss), C6 + G6 with a short contact click.</summary>
        public static float[] Parry()
        {
            var s = Buf(0.32);
            Click(s, 111, 0.5, 5000);
            Partials(s, 0.5, 0.001, 0.001, (C6, 1, 0.11), (C6 * 2.76, 0.25, 0.04), (G6, 0.6, 0.08), (G6 * 2.0, 0.15, 0.03));
            return Done(LowPass(s, 7000), 0.8f);
        }

        /// <summary>Knockout: a deep falling boom with a dark noise tail.</summary>
        public static float[] Death()
        {
            var s = Buf(0.6);
            Click(s, 121, 0.5, 2000, 0.005);
            Body(s, 0.5, 200, 90, 0.35, 0.002, 0.12);
            Noise(s, 122, 1.0, 2000, 300, 1.0, 0.003, 0.1);
            Chip(s, 0.3, 392, 0.0, 0.003, 0.08, 0.0);
            Chip(s, 0.3, 262, 0.08, 0.003, 0.08, 0.0);
            Chip(s, 0.3, 196, 0.16, 0.003, 0.14, 0.01);
            Saturate(s, 2.5);
            return Done(Room(HighPass(LowPass(s, 3500), 100), 0.2), 0.85f);
        }

        // ------------------------------------------------------------------ tap (BB-TAP1)

        /// <summary>A tap strike landed: a punchy "whump" with a crack, bigger than a normal hit.</summary>
        public static float[] TapStrike()
        {
            var s = Buf(0.2);
            Click(s, 131, 0.9, 5000, 0.004);
            Body(s, 0.55, 240, 130, 0.06, 0.001, 0.045);
            Noise(s, 132, 1.3, 2000, 1200, 2, 0.001, 0.03);
            Saturate(s, 2.5);
            return Done(HighPass(LowPass(s, 5000), 110), 0.9f);
        }

        /// <summary>PERFECT: a bright rising chime E6 then A6 on top of the strike (tap_strike already plays the impact).</summary>
        public static float[] TapPerfect()
        {
            var s = Buf(0.42);
            Click(s, 141, 0.3, 6000, 0.002);
            Partials(s, 0.35, 0.002, 0.03, (E6, 1, 0.12), (E6 * 2, 0.2, 0.05));
            Partials(s, 0.4, 0.002, 0.09, (A6, 1, 0.16), (A6 * 2, 0.2, 0.06), (A6 * 3, 0.06, 0.03));
            return Done(Room(LowPass(s, 8000), 0.2), 0.85f);
        }

        /// <summary>Missed tap: a soft low "whiff" (air, no impact).</summary>
        public static float[] TapMiss()
        {
            var s = Buf(0.16);
            Noise(s, 151, 0.8, 1300, 380, 2.5, 0.02, 0.03);
            return Done(LowPass(s, 2500), 0.55f);
        }

        // ------------------------------------------------------------------ meta (chest, level up, tree)

        /// <summary>Chest opens: a wooden thunk, then a sparkle run up the pentatonic scale.</summary>
        public static float[] ChestOpen()
        {
            var s = Buf(0.95);
            Click(s, 161, 0.4, 2000, 0.004);
            Body(s, 0.5, 200, 120, 0.06, 0.002, 0.05);
            Noise(s, 162, 0.6, 900, 500, 2, 0.002, 0.04);
            double[] run = { G5, C6, D6, E6, G6, C7 };
            for (var i = 0; i < run.Length; i++)
                Partials(s, 0.22, 0.002, 0.12 + i * 0.055, (run[i], 1, 0.14 + i * 0.03), (run[i] * 2, 0.15, 0.05));
            return Done(Room(LowPass(s, 9000), 0.25), 0.8f);
        }

        /// <summary>One reward row appears: a small coin "ting".</summary>
        public static float[] ChestItem()
        {
            var s = Buf(0.2);
            Partials(s, 0.5, 0.001, 0, (G6, 1, 0.05), (C7, 0.8, 0.09), (C7 * 2, 0.12, 0.03));
            return Done(Room(LowPass(s, 9000), 0.15), 0.7f);
        }

        /// <summary>Ball level up: a chiptune fanfare C5 E5 G5 C6 with a shimmer on the last note.</summary>
        public static float[] LevelUp()
        {
            var s = Buf(0.9);
            double[] notes = { C5, E5, G5, C6 };
            for (var i = 0; i < notes.Length; i++)
            {
                var last = i == notes.Length - 1;
                Chip(s, 0.3, notes[i], i * 0.08, 0.004, last ? 0.28 : 0.06, last ? 0.006 : 0);
            }
            Partials(s, 0.2, 0.01, 0.24, (C7, 1, 0.25), (E6 * 2, 0.4, 0.2));
            return Done(Room(LowPass(s, 6500), 0.25), 0.75f);
        }

        /// <summary>A tree node is bought: a quick upward "plink" A5 then E6.</summary>
        public static float[] TreeAdd()
        {
            var s = Buf(0.22);
            Click(s, 171, 0.25, 4000, 0.002);
            Partials(s, 0.45, 0.001, 0.0, (A5, 1, 0.05), (A5 * 2, 0.2, 0.02));
            Partials(s, 0.45, 0.001, 0.05, (E6, 1, 0.1), (E6 * 2, 0.2, 0.04));
            return Done(Room(LowPass(s, 8000), 0.15), 0.7f);
        }

        /// <summary>The tree is reset: three soft notes falling (G6 E6 C6).</summary>
        public static float[] TreeReset()
        {
            var s = Buf(0.36);
            double[] notes = { G5, E5, C5 };
            for (var i = 0; i < notes.Length; i++) Chip(s, 0.25, notes[i] * 2, i * 0.07, 0.003, 0.07);
            return Done(Room(HighPass(LowPass(s, 4000), 150), 0.2), 0.6f);
        }
    }
}
