using System;
using System.Collections.Generic;
using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// All game audio (BB-03). Driven only by View-side events: the arena's SimEventRaised / MatchStarted / MatchEnded,
    /// screen changes (SetScreen from GameController), Run flow events and UiAudio requests. Never reads or changes the sim.
    /// Cues: one SfxPlayer pool of 12 voices (clips from SfxLibrary). Music: 2 AudioSources crossfaded by MusicMixer,
    /// pitch always 1 (never tied to Time.timeScale or the arena's x2), ducked by Ducker on real (unscaled) time.
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const int CueVoices = 12;
        public const float PitchJitter = 0.03f;
        public const float UnlockSpacing = 0.25f;
        /// <summary>Win/lose cue waits for the knockout slow-mo (FxView, 0.5 s) like the old win sound did.</summary>
        public const float ResultCueDelay = 0.5f;
        public const float PauseDuckDb = -6f;
        public const float SliderClickGap = 0.08f;

        public struct PlayedCue
        {
            public string Id;
            public float Time;
            public float Volume;
            public float Pitch;
        }

        /// <summary>Most entries kept in <see cref="Played"/>.</summary>
        public const int PlayedCap = 256;

        /// <summary>The last <see cref="PlayedCap"/> cues the director decided to play, oldest first (tests and debugging). Always on, bounded.</summary>
        public readonly List<PlayedCue> Played = new List<PlayedCue>(PlayedCap);

        /// <summary>Real time in seconds; tests replace it.</summary>
        public Func<float> Clock = () => Time.unscaledTime;

        public MusicMixer Music { get; } = new MusicMixer();
        public Ducker Duck { get; } = new Ducker();
        public int MusicVolume { get; private set; } = VolumeSettings.DefaultMusic;
        public int SfxVolume { get; private set; } = VolumeSettings.DefaultSfx;
        /// <summary>Music -6 dB while true (no pause screen exists yet; ready for one).</summary>
        public bool Paused { get; set; }
        public MusicScreen Screen => screen;
        public SfxLibrary Library => library;

        SfxLibrary library;
        ISaveStore store;
        SfxPlayer cues;
        readonly AudioSource[] music = new AudioSource[2];
        readonly CueGate simGate = new CueGate();
        readonly CueGate uiGate = new CueGate();
        readonly List<(string id, float due, float volume)> scheduled = new List<(string, float, float)>();
        readonly System.Random rng = new System.Random(0x0B03);   // isolated from UnityEngine.Random (Versus seeds use it)
        MusicScreen screen = MusicScreen.None;
        string pendingResult;
        float pendingResultAt;
        float lastSliderClick = float.NegativeInfinity;
        int uiFrame;

        public static AudioDirector Create(Transform parent, SfxLibrary library, ISaveStore store)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<AudioDirector>();
            d.library = library;
            d.store = store;
            d.cues = SfxPlayer.Create(go.transform, CueVoices);
            for (var i = 0; i < d.music.Length; i++)
            {
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = true;
                src.spatialBlend = 0f;
                src.pitch = 1f;
                src.volume = 0f;
                src.ignoreListenerPause = true;
                d.music[i] = src;
            }
            d.Music.SlotStarted += d.OnSlotStarted;
            d.Music.SlotStopped += d.OnSlotStopped;
            var (m, s) = VolumeSettings.Load(store);
            d.MusicVolume = m;
            d.SfxVolume = s;
            UiAudio.Requested += d.OnUiRequested;
            AudioLog.AnnounceOnce();
            d.ApplyMusic(d.Clock());
            return d;
        }

        void OnDestroy() => UiAudio.Requested -= OnUiRequested;

        public AudioSource MusicSource(int slot) => music[slot];

        public string HitCueFor(string weaponId) => AudioIds.HitCue(weaponId, library != null ? (Func<string, bool>)library.Has : null);

        /// <summary>Hit cue of a Hit / ProjectileHit event: the attacker's weapon (Hit.A) or the shot's owner (ProjectileHit.A), so a
        /// HIVE turret shot sounds as hit_rig and a SPARKLER arrow as hit_volley. A deflected shot belongs to the deflector.</summary>
        public static string HitCueFor(SimEvent e, MatchSim sim, Func<string, bool> hasClip)
        {
            var id = sim != null && e.A >= 0 && e.A < sim.Balls.Count ? sim.Balls[e.A].Weapon.Id : null;
            return AudioIds.HitCue(id, hasClip);
        }

        // ---------------- cues ----------------

        /// <summary>A match cue for Sim tick <paramref name="tick"/> (deduped per tick, max 3 hit cues per tick).
        /// <paramref name="maxPitch"/> caps the pitch after the random jitter.</summary>
        public bool RequestCue(string id, int tick, float volume = 0.8f, float pitch = 1f, float maxPitch = float.PositiveInfinity) =>
            simGate.Allow(id, tick) && PlayNow(id, volume, pitch, maxPitch);

        /// <summary>Hit of <paramref name="weaponId"/> on Sim tick <paramref name="tick"/>; <paramref name="heat"/> (0..1) raises the
        /// pitch of the weapon's own clip by up to AudioIds.HeatPitchCapFor (jitter included, never above the cap).</summary>
        public bool RequestHit(string weaponId, int tick, float volume = 0.6f, float heat = 0f) =>
            RequestCue(HitCueFor(weaponId), tick, volume, AudioIds.HeatPitch(weaponId, heat), 1f + AudioIds.HeatPitchCapFor(weaponId));

        /// <summary>Heat (0..1) of the ball that owns a Hit / ProjectileHit (same ball as <see cref="HitCueFor(SimEvent, MatchSim, Func{string, bool})"/>),
        /// from its weapon stat as the blade tint uses it (Palette.Heat). 0 when unknown.</summary>
        public static float HeatOf(SimEvent e, MatchSim sim)
        {
            if (sim == null || e.A < 0 || e.A >= sim.Balls.Count) return 0f;
            var w = sim.Balls[e.A].Weapon;
            return Palette.Heat(w.Id, w.StatValue.ToFloat());
        }

        void RequestHitEvent(SimEvent e, MatchSim sim, float volume)
        {
            var weaponId = sim != null && e.A >= 0 && e.A < sim.Balls.Count ? sim.Balls[e.A].Weapon.Id : null;
            RequestCue(HitCueFor(e, sim, Has), e.Tick, volume, AudioIds.HeatPitch(weaponId, HeatOf(e, sim)), 1f + AudioIds.HeatPitchCapFor(weaponId));
        }

        /// <summary>A UI / flow cue (deduped per frame).</summary>
        public bool RequestUiCue(string id, float volume = 0.7f)
        {
            if (!uiGate.Allow(id, uiFrame)) return false;
            if (AudioIds.EndsFightMusic(id)) EndFightMusic();
            return PlayNow(id, volume, 1f);
        }

        void OnUiRequested(string id) => RequestUiCue(id);

        bool PlayNow(string id, float volume, float pitch, float maxPitch = float.PositiveInfinity)
        {
            if (AudioIds.Jittered(id)) pitch *= 1f + PitchJitter * (float)(rng.NextDouble() * 2.0 - 1.0);
            if (pitch > maxPitch) pitch = maxPitch;
            var now = Clock();
            var v = volume * VolumeCurve.Linear(SfxVolume);
            if (Played.Count >= PlayedCap) Played.RemoveRange(0, Played.Count - PlayedCap + 1);   // drop the oldest
            Played.Add(new PlayedCue { Id = id, Time = now, Volume = v, Pitch = pitch });
            if (cues != null && v > 0f) cues.Play(library != null ? library.Get(id) : null, v, pitch);
            if (AudioIds.Ducks(id)) Duck.Trigger(now);
            return true;
        }

        /// <summary>fight_win / fight_lose / run_win / run_over: the fight track fades out over 0.4 s (a stronger drop than the duck);
        /// the next screen's track then follows the normal mapping. A non-fight track (result music on the Summary) keeps playing.</summary>
        void EndFightMusic()
        {
            if (AudioIds.IsFightTrack(Music.Current)) Music.FadeOut(MusicMixer.FadeOutSeconds);
        }

        // ---------------- match events ----------------

        public void OnSimEvent(SimEvent e, MatchSim sim)
        {
            TrackHp(e, sim);
            if (AudioLog.Enabled)
            {
                var shell = ShellTrace.Line(e, sim);   // BB-6d QA: SHELL KING shield trace in the audio log file
                if (shell != null) AudioLog.Write(shell, this);
            }
            switch (e.Type)
            {
                case SimEventType.Hit:
                    RequestHitEvent(e, sim, 0.6f);
                    break;
                case SimEventType.ProjectileHit:
                    // A shot stopped by a boss shell (Variant 1, 0 damage) keeps its normal hit cue (BB-04).
                    if (e.Value.ToFloat() > 0f || e.Variant == 1) RequestHitEvent(e, sim, 0.35f);
                    break;
                case SimEventType.Boss:
                    RequestBossCue(e, sim);
                    break;
                case SimEventType.SkillFired:
                    var skillCue = "skill_" + e.SkillId;
                    if (Has(skillCue)) RequestCue(skillCue, e.Tick, 0.8f, 1f);
                    break;
                case SimEventType.Parry:
                    RequestCue(AudioIds.Parry, e.Tick, 0.6f);
                    break;
                case SimEventType.ProjectileDeflected:
                    RequestCue(AudioIds.Parry, e.Tick, 0.3f, 1.3f);
                    break;
                case SimEventType.ShieldBlocked:
                    RequestCue(AudioIds.Parry, e.Tick, 0.5f, 0.8f);
                    break;
                case SimEventType.ProjectileFired:
                    RequestCue(AudioIds.Wall, e.Tick, 0.12f, 1.7f);   // six turrets fire together: one sound
                    break;
                case SimEventType.WallBounce:
                    RequestHazard(e, sim, WallBounceVolume, 1f);
                    break;
                case SimEventType.BallBounce:
                    RequestCue(AudioIds.Wall, e.Tick, 0.35f, 0.8f);
                    break;
                case SimEventType.ObstacleHit:
                    // Damaging obstacle: spike_hit (or wall) at pitch 1.0 + the normal jitter (BB-SFX2: no 0.7 pitch-down).
                    // Harmless bumper / pillar: wall at pitch 1.2, as before.
                    RequestHazard(e, sim, ObstacleVolume, e.Value.ToFloat() > 0f ? 1f : ObstacleHarmlessPitch);
                    break;
                case SimEventType.Death:
                    RequestCue(AudioIds.Death, e.Tick, 0.9f);
                    break;
                case SimEventType.TraitTriggered:
                    if (e.TraitId == BallBattle.Sim.Traits.SecondWindTrait.TraitId) RequestCue(AudioIds.SecondWind, e.Tick, 0.7f);
                    break;
            }
        }

        bool Has(string id) => library != null && library.Has(id);

        // ---------------- boss cues (BB-04) ----------------

        public enum BossCueResult { None, Played, Missing, Gated }

        /// <summary>
        /// The cue of a boss event (AudioIds.BossCue): normal SFX bus at AudioIds.BossCueVolume, pitch 1, no jitter, through the same
        /// per-tick gate as every match cue. A missing clip plays nothing (no fallback) and is reported as Missing. Events without a
        /// cue (shell off, magnet end) return None. Logged when the audio log is on.
        /// </summary>
        public BossCueResult RequestBossCue(SimEvent e, MatchSim sim)
        {
            var id = AudioIds.BossCue((BallBattle.Sim.Boss.BossEvent)e.Variant);
            if (id == null) return BossCueResult.None;
            BossCueResult result;
            if (!Has(id)) result = BossCueResult.Missing;
            else result = RequestCue(id, e.Tick, AudioIds.BossCueVolume, AudioIds.BossCuePitch) ? BossCueResult.Played : BossCueResult.Gated;
            if (AudioLog.Enabled) AudioLog.Write(AudioLog.BossLine(e, sim, id, result), this);
            return result;
        }

        // ---------------- hazards: wall bounces and obstacle hits (BB-SFX2) ----------------

        /// <summary>Volume of every WallBounce cue (wall or spike_hit).</summary>
        public const float WallBounceVolume = 0.2f;
        /// <summary>Volume of every ObstacleHit cue (wall or spike_hit).</summary>
        public const float ObstacleVolume = 0.25f;
        /// <summary>Pitch of a harmless obstacle (bumper / pillar) bounce.</summary>
        public const float ObstacleHarmlessPitch = 1.2f;

        /// <summary>
        /// The one cue of a WallBounce / ObstacleHit: spike_hit when it dealt damage and the clip exists, else wall. Pure; exactly
        /// one id per event, never both. Any other event type gets wall.
        /// </summary>
        public static string HazardCue(SimEventType type, float damage, Func<string, bool> hasClip) =>
            (type == SimEventType.WallBounce || type == SimEventType.ObstacleHit) && damage > 0f && hasClip != null && hasClip(AudioIds.SpikeHit)
                ? AudioIds.SpikeHit : AudioIds.Wall;

        /// <summary>A bounce that took spike damage plays spike_hit when that clip exists, else the plain wall sound (never both).</summary>
        public static string WallBounceCue(float spikeDamage, Func<string, bool> hasClip) => HazardCue(SimEventType.WallBounce, spikeDamage, hasClip);

        // BB-6d: per ball, the tick of its last spike_hit, so the same contact never also plays a wall cue in that tick.
        readonly System.Collections.Generic.Dictionary<int, int> spikeTickByBall = new System.Collections.Generic.Dictionary<int, int>();

        void RequestHazard(SimEvent e, MatchSim sim, float volume, float pitch)
        {
            var damage = e.Value.ToFloat();
            var id = HazardCue(e.Type, damage, Has);
            bool allowed;
            if (id == AudioIds.Wall && spikeTickByBall.TryGetValue(e.A, out var spikeTick) && spikeTick == e.Tick)
                allowed = false;   // this ball already played spike_hit this tick: one contact = one cue
            else
            {
                allowed = RequestCue(id, e.Tick, volume, pitch);
                if (id == AudioIds.SpikeHit) spikeTickByBall[e.A] = e.Tick;
            }
            if (AudioLog.Enabled) AudioLog.Write(AudioLog.Line(e, sim, damage, LastHpChangeTick(e.A), id, allowed), this);
        }

        // HP as the View last saw it, per ball: the tick it last changed (for the audio log).
        float[] seenHp = new float[0];
        int[] hpChangedAt = new int[0];

        void TrackHp(SimEvent e, MatchSim sim)
        {
            if (sim == null) return;
            var n = sim.Balls.Count;
            if (seenHp.Length != n)
            {
                seenHp = new float[n];
                hpChangedAt = new int[n];
                for (var i = 0; i < n; i++) { seenHp[i] = sim.Balls[i].Hp.ToFloat(); hpChangedAt[i] = -1; }
            }
            for (var i = 0; i < n; i++)
            {
                var hp = sim.Balls[i].Hp.ToFloat();
                if (hp == seenHp[i]) continue;
                seenHp[i] = hp;
                hpChangedAt[i] = e.Tick;
            }
        }

        /// <summary>Tick at which ball <paramref name="ball"/>'s HP last changed as seen by the View (-1 = not since the match began).</summary>
        public int LastHpChangeTick(int ball) => ball >= 0 && ball < hpChangedAt.Length ? hpChangedAt[ball] : -1;

        /// <summary>Lines written by the audio log for this director (bounded like Played; tests).</summary>
        public readonly List<string> LogLines = new List<string>();

        /// <summary>A new match on the arena: tick numbers restart; a fight track faded by the last knockout comes back.</summary>
        public void OnMatchStarted()
        {
            simGate.Reset();
            spikeTickByBall.Clear();
            seenHp = new float[0];
            hpChangedAt = new int[0];
            pendingResult = null;
            var track = AudioIds.TrackFor(screen);
            if (AudioIds.IsFightTrack(track)) Music.Request(track);
        }

        /// <summary>
        /// Arena match over. <paramref name="playerBall"/> = the marked ball (Run: 0) or -1 (Versus): with a player the cue is
        /// fight_win / fight_lose from their side; in Versus a decided round is fight_win and a draw plays no cue at all (the
        /// result screen's music_result is the only sound). Plays after the KO slow-mo.
        /// </summary>
        public void OnMatchEnded(int winner, int playerBall)
        {
            if (playerBall < 0 && winner < 0)
            {
                pendingResult = null;   // Versus draw: neither win nor lose
                return;
            }
            pendingResult = (playerBall >= 0 ? winner == playerBall : winner >= 0) ? AudioIds.FightWin : AudioIds.FightLose;
            pendingResultAt = Clock() + ResultCueDelay;
        }

        /// <summary>Summary appears: run_win / run_over now, then one unlock per milestone row shown, 0.25 s apart.</summary>
        public void OnSummaryShown(bool won, int milestones)
        {
            RequestUiCue(won ? AudioIds.RunWin : AudioIds.RunOver, 0.8f);
            var now = Clock();
            for (var i = 0; i < milestones; i++) scheduled.Add((AudioIds.Unlock, now + i * UnlockSpacing, 0.7f));
            RunSchedule(now);
        }

        // ---------------- music ----------------

        /// <summary>Screen shown now; the track changes only when the screen does (same track = no restart).</summary>
        public void SetScreen(MusicScreen s)
        {
            if (s == MusicScreen.None || s == screen) return;
            screen = s;
            Music.Request(AudioIds.TrackFor(s));
        }

        public void SetMusicVolume(int step, bool save = false)
        {
            MusicVolume = VolumeSettings.Clamp(step);
            ApplyMusic(Clock());
            if (save) VolumeSettings.Save(store, MusicVolume, SfxVolume);
        }

        /// <summary>SFX slider: a ui_click at the new volume, at most one per 80 ms.</summary>
        public void SetSfxVolume(int step, bool save = false)
        {
            var v = VolumeSettings.Clamp(step);
            var changed = v != SfxVolume;
            SfxVolume = v;
            var now = Clock();
            if (changed && now - lastSliderClick >= SliderClickGap)
            {
                lastSliderClick = now;
                PlayNow(AudioIds.UiClick, 0.7f, 1f);
            }
            if (save) VolumeSettings.Save(store, MusicVolume, SfxVolume);
        }

        void OnSlotStarted(int slot, string track)
        {
            var src = music[slot];
            if (src == null) return;
            src.clip = library != null ? library.Get(track) : null;
            src.volume = 0f;
            src.pitch = 1f;
            if (src.clip != null && Application.isPlaying) src.Play();
        }

        void OnSlotStopped(int slot)
        {
            var src = music[slot];
            if (src == null) return;
            src.Stop();
            src.volume = 0f;
        }

        void ApplyMusic(float now)
        {
            var db = Duck.Db(now) + (Paused ? PauseDuckDb : 0f);
            var level = VolumeCurve.Linear(MusicVolume) * VolumeCurve.DbToLinear(db);
            for (var i = 0; i < music.Length; i++)
            {
                if (music[i] == null) continue;
                music[i].volume = Music.Gain[i] * level;
                music[i].pitch = 1f;
            }
        }

        // ---------------- clock ----------------

        void Update() => Tick(Time.unscaledDeltaTime);

        /// <summary>One frame of real time (Update's body; tests call it with their own clock).</summary>
        public void Tick(float unscaledDt)
        {
            uiFrame++;
            var now = Clock();
            if (pendingResult != null && now >= pendingResultAt)
            {
                var id = pendingResult;
                pendingResult = null;
                RequestUiCue(id, 0.7f);
            }
            RunSchedule(now);
            Music.Tick(unscaledDt);
            ApplyMusic(now);
        }

        void RunSchedule(float now)
        {
            for (var i = 0; i < scheduled.Count; i++)
            {
                if (scheduled[i].due > now) continue;
                var c = scheduled[i];
                scheduled.RemoveAt(i--);
                PlayNow(c.id, c.volume, 1f);
            }
        }
    }
}
