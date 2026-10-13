using System;
using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>UI-side sound requests (buttons, screens). AudioDirector listens; with no director nothing plays.</summary>
    public static class UiAudio
    {
        public static event Action<string> Requested;
        public static void Play(string id)
        {
            if (!string.IsNullOrEmpty(id)) Requested?.Invoke(id);
        }
    }

    /// <summary>Volume steps 0..10: 0 = silent, otherwise dB = 20*log10(v/10) (10 = 0 dB).</summary>
    public static class VolumeCurve
    {
        public const int Max = 10;

        public static float Db(int step) => step <= 0 ? float.NegativeInfinity : 20f * Mathf.Log10(Mathf.Min(step, Max) / (float)Max);

        public static float Linear(int step) => step <= 0 ? 0f : DbToLinear(Db(step));

        public static float DbToLinear(float db) => float.IsNegativeInfinity(db) ? 0f : Mathf.Pow(10f, db / 20f);
    }

    /// <summary>musicVol / sfxVol in the save (SaveData). Reads and writes go through the given store only.</summary>
    public static class VolumeSettings
    {
        public const int DefaultMusic = 6;
        public const int DefaultSfx = 8;

        public static int Clamp(int v) => Mathf.Clamp(v, 0, VolumeCurve.Max);

        public static (int music, int sfx) Load(ISaveStore store)
        {
            var d = store?.Load();
            return d == null ? (DefaultMusic, DefaultSfx) : (Clamp(d.musicVol), Clamp(d.sfxVol));
        }

        /// <summary>Loads the current save, changes only the two volumes, saves it back (progress and run untouched).</summary>
        public static void Save(ISaveStore store, int music, int sfx)
        {
            if (store == null) return;
            var d = store.Load() ?? new SaveData();
            d.musicVol = Clamp(music);
            d.sfxVol = Clamp(sfx);
            store.Save(d);
        }
    }

    /// <summary>
    /// Writes the volumes to the save only when they differ from what was last saved (BB-FIX1a: not on every slider step).
    /// </summary>
    public sealed class VolumeSaver
    {
        readonly ISaveStore store;
        int savedMusic, savedSfx;

        public VolumeSaver(ISaveStore store, int music, int sfx)
        {
            this.store = store;
            savedMusic = music;
            savedSfx = sfx;
        }

        /// <summary>True when it wrote the save.</summary>
        public bool Commit(int music, int sfx)
        {
            if (music == savedMusic && sfx == savedSfx) return false;
            VolumeSettings.Save(store, music, sfx);
            savedMusic = music;
            savedSfx = sfx;
            return true;
        }
    }

    /// <summary>Settings page ↔ audio: sliders change the volume live; the save is written on pointer release and when Settings closes.</summary>
    public static class SettingsAudio
    {
        public static VolumeSaver Bind(MenuView menu, AudioDirector audio, ISaveStore store)
        {
            var saver = new VolumeSaver(store, audio.MusicVolume, audio.SfxVolume);
            var s = menu.Settings;
            s.SetValues(audio.MusicVolume, audio.SfxVolume);
            s.MusicChanged += v => audio.SetMusicVolume(v);
            s.SfxChanged += v => audio.SetSfxVolume(v);
            s.Released += () => saver.Commit(audio.MusicVolume, audio.SfxVolume);
            menu.SettingsClosed += () => saver.Commit(audio.MusicVolume, audio.SfxVolume);
            return saver;
        }
    }

    /// <summary>
    /// Music duck envelope in dB over real (unscaled) time: 0 → DepthDb in Attack, hold, back to 0 in Release.
    /// A new trigger restarts the hold from the current level; it never goes below DepthDb.
    /// </summary>
    public sealed class Ducker
    {
        public const float DepthDb = -2.5f;
        public const float Attack = 0.03f;
        public const float Hold = 0.15f;
        public const float Release = 0.2f;

        float start = float.NegativeInfinity;

        public void Trigger(float now)
        {
            var depth = Mathf.Clamp01(Db(now) / DepthDb);   // 0 = no duck, 1 = full
            start = now - depth * Attack;
        }

        public float Db(float now)
        {
            var x = now - start;
            if (float.IsInfinity(x) || x < 0f) return 0f;
            if (x < Attack) return DepthDb * x / Attack;
            if (x < Attack + Hold) return DepthDb;
            var r = (x - Attack - Hold) / Release;
            return r >= 1f ? 0f : DepthDb * (1f - r);
        }
    }

    /// <summary>
    /// Two-slot music crossfade (equal power) as plain data: the director plays/stops AudioSources on SlotStarted /
    /// SlotStopped and applies Gain[slot]. Requesting the track already playing does nothing.
    /// </summary>
    public sealed class MusicMixer
    {
        public const float CrossfadeSeconds = 0.6f;
        public const float FadeOutSeconds = 0.4f;

        public readonly string[] Track = new string[2];
        public readonly float[] Gain = new float[2];

        public event Action<int, string> SlotStarted;
        public event Action<int> SlotStopped;

        int active = -1, from = -1;
        float fromStart, t, duration;

        /// <summary>Track playing (or fading in); null when silent or fading out.</summary>
        public string Current => active >= 0 ? Track[active] : null;
        public int ActiveSlot => active;
        public int Starts { get; private set; }

        /// <summary>Crossfade to <paramref name="track"/>. False when it is already the current track (no restart).</summary>
        public bool Request(string track)
        {
            if (track == Current) return false;
            if (track == null) { FadeOut(CrossfadeSeconds); return true; }
            var slot = active >= 0 ? 1 - active : (from >= 0 ? 1 - from : 0);
            if (slot == from) StopSlot(from);   // a third track while crossfading: drop the oldest
            if (active >= 0) from = active;     // else a fading-out slot (if any) keeps fading from where it is
            if (from >= 0) fromStart = Gain[from];
            active = slot;
            Track[slot] = track;
            Gain[slot] = 0f;
            t = 0f;
            duration = CrossfadeSeconds;
            Starts++;
            SlotStarted?.Invoke(slot, track);
            return true;
        }

        public void FadeOut(float seconds)
        {
            if (active < 0) return;
            if (from >= 0) StopSlot(from);
            from = active;
            fromStart = Gain[from];
            active = -1;
            t = 0f;
            duration = seconds;
        }

        public void Tick(float dt)
        {
            t = Mathf.Min(t + Mathf.Max(0f, dt), duration);
            var p = duration > 0f ? t / duration : 1f;
            if (active >= 0) Gain[active] = Mathf.Sin(p * Mathf.PI * 0.5f);
            if (from >= 0)
            {
                Gain[from] = fromStart * Mathf.Cos(p * Mathf.PI * 0.5f);
                if (p >= 1f) StopSlot(from);
            }
        }

        void StopSlot(int slot)
        {
            Gain[slot] = 0f;
            Track[slot] = null;
            if (from == slot) from = -1;
            SlotStopped?.Invoke(slot);
        }
    }

    /// <summary>
    /// Per-tick sound gate: a cue id plays once per tick; at most MaxHits distinct hit cues per tick.
    /// Keys are Sim ticks for match sounds and frame numbers for UI sounds (separate gates).
    /// </summary>
    public sealed class CueGate
    {
        public const int MaxHits = 3;

        readonly HashSet<string> ids = new HashSet<string>();
        long key = long.MinValue;
        int hits;

        public bool Allow(string id, long tick)
        {
            if (tick != key)
            {
                key = tick;
                ids.Clear();
                hits = 0;
            }
            if (ids.Contains(id)) return false;
            if (AudioIds.IsHit(id))
            {
                if (hits >= MaxHits) return false;
                hits++;
            }
            ids.Add(id);
            return true;
        }

        public void Reset()
        {
            key = long.MinValue;
            ids.Clear();
            hits = 0;
        }
    }
}
