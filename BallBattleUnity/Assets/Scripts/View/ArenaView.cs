using System;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Runs a MatchSim at a fixed 60 Hz from Update and draws it, interpolating between the last two ticks.
    /// Never writes into the sim. StartMatch may be called at any time, even from a SimEventRaised/MatchEnded
    /// handler: during the tick loop the request is deferred until the loop finishes.
    /// </summary>
    public sealed class ArenaView : MonoBehaviour
    {
        const float TickSeconds = 1f / MatchConfig.TicksPerSecond;
        const float MaxFrameSeconds = 0.25f;
        const int BodyOrderBase = 10;
        const int BladeOrderBase = 20;

        public ArtLibrary Art;
        public string WeaponA = BladeRule.WeaponId;
        public string WeaponB = FangRule.WeaponId;
        public uint Seed = 1;
        /// <summary>Arena used by plain Versus matches (ArenaRegistry id).</summary>
        public string ArenaId = ArenaRegistry.Classic;
        [Tooltip("Restart with the next seed a moment after a match ends (preview without the match flow).")]
        public bool AutoRestart = true;
        public float RestartDelaySeconds = 2f;

        /// <summary>Frozen: the sim does not step, the current pose is still drawn (menu, countdown).</summary>
        public bool Paused { get; set; }

        /// <summary>Playback speed (Run mode x2). Ticks stay fixed; more of them run per frame.</summary>
        public float TimeScale { get; set; } = 1f;

        /// <summary>Raised for every sim event, in tick order (effects/audio subscribe here).</summary>
        public event Action<SimEvent> SimEventRaised;
        /// <summary>Raised once when the current match ends: winner ball index, -1 on draw.</summary>
        public event Action<int> MatchEnded;
        /// <summary>Raised after a new match (and its ball views) has been created.</summary>
        public event Action MatchStarted;

        public BallView GetBallView(int index) => index >= 0 && index < ballViews.Length ? ballViews[index] : null;

        public MatchSim Sim { get; private set; }

        /// <summary>HUD (tests).</summary>
        public HudView Hud => hud;

        /// <summary>Trait badges on the HUD (FxView marks a used Second Wind).</summary>
        public TraitBadgeView Badges => badges;

        /// <summary>Arena frame: floor tiles, walls, teeth (tests).</summary>
        public ArenaFrameView Frame => frame;

        /// <summary>How a boss ball is drawn (art v1). Sizes and hitboxes stay the sim's; only the body sprite changes.</summary>
        public enum BossLook { None, ShellKing, Mimic }

        /// <summary>Boss look applied to <see cref="BossBall"/> in the next StartMatch (Run sets it per fight; Versus: None).</summary>
        public BossLook Boss { get; set; } = BossLook.None;
        public int BossBall { get; set; } = 1;
        public ArtLibrary.MimicLook MimicShown { get; private set; } = ArtLibrary.MimicLook.Idle;

        /// <summary>Mimic's sprite for the damage kind it adapted to (wired when the sim reports adaptation, v4).</summary>
        public static ArtLibrary.MimicLook MimicLookFor(DamageKind kind)
        {
            switch (kind)
            {
                case DamageKind.Weapon: return ArtLibrary.MimicLook.Weapon;
                case DamageKind.Projectile: return ArtLibrary.MimicLook.Shot;
                case DamageKind.Status: return ArtLibrary.MimicLook.Poison;
                case DamageKind.Reflect: return ArtLibrary.MimicLook.Reflect;
                default: return ArtLibrary.MimicLook.Idle;
            }
        }

        /// <summary>Swap Mimic's body to the look for what it adapted to. No-op for other bosses.</summary>
        public void SetMimicLook(ArtLibrary.MimicLook look)
        {
            if (Boss != BossLook.Mimic) return;
            var sprite = Art.MimicSprite(look);
            var v = GetBallView(BossBall);
            if (sprite == null || v == null) return;
            v.SetBodySprite(sprite, ArtLibrary.BossBodyRadius);
            MimicShown = look;
        }

        /// <summary>Shell King's 1 s glow (shellking_glow.png), e.g. when its shield takes a hit.</summary>
        public void PlayBossGlow()
        {
            if (Boss != BossLook.ShellKing) return;
            GetBallView(BossBall)?.PlayGlow(Art.ShellKingGlow, 1f);
        }

        void ApplyBossLook()
        {
            MimicShown = ArtLibrary.MimicLook.Idle;
            if (Boss == BossLook.None) return;
            var v = GetBallView(BossBall);
            var sprite = Boss == BossLook.ShellKing ? Art.ShellKing : Art.MimicSprite(ArtLibrary.MimicLook.Idle);
            if (v != null && sprite != null) v.SetBodySprite(sprite, ArtLibrary.BossBodyRadius);   // drawn at its own size: no x1.5
        }

        /// <summary>Ball that gets the player marker (arrow + dotted ring) in the next StartMatch; -1 = none (Versus).</summary>
        public int MarkedBall { get; set; } = -1;

        BallView[] ballViews = new BallView[0];
        ArenaFrameView frame;
        HudView hud;
        ProjectileView projectileView;
        StatusView statusView;
        TraitBadgeView badges;
        Vector2[] prevPos = new Vector2[0];
        float[] prevAngle = new float[0];
        float accumulator;
        float endTimer;
        bool endReported;
        bool stepping;
        MatchSim pending;
        string pendingTagA, pendingTagB;

        void Start()
        {
            EnsureInit();
            if (Sim == null) StartMatch(WeaponA, WeaponB, Seed);
        }

        void EnsureInit()
        {
            if (frame != null) return;
            if (Art == null) throw new InvalidOperationException("ArenaView needs an ArtLibrary (run BallBattle/Build Scenes)");
            frame = ArenaFrameView.Create(transform, Art);
            hud = HudView.Create(transform, Art);
            projectileView = ProjectileView.Create(transform, Art.Pixel);
            statusView = StatusView.Create(transform, Art.Pixel);
            badges = TraitBadgeView.Create(transform, Art);
        }

        /// <summary>Plain Versus match from two weapon ids.</summary>
        public void StartMatch(string weaponA, string weaponB, uint seed)
            => StartMatch(new MatchSim(ArenaRegistry.Create(ArenaId), seed, new[] { WeaponRegistry.Create(weaponA), WeaponRegistry.Create(weaponB) }));

        /// <summary>Show and run a match built elsewhere (Run mode: loadouts with traits, HP, size). Tags label the HUD names.</summary>
        public void StartMatch(MatchSim sim, string tagA = null, string tagB = null)
        {
            if (sim == null) throw new ArgumentNullException(nameof(sim));
            if (stepping)
            {
                pending = sim;
                pendingTagA = tagA;
                pendingTagB = tagB;
                return;
            }

            EnsureInit();
            WeaponA = sim.Balls[0].Weapon.Id;
            WeaponB = sim.Balls[1].Weapon.Id;
            Seed = sim.Seed;
            foreach (var v in ballViews)
            {
                if (v == null) continue;
                v.gameObject.SetActive(false);   // Destroy is deferred to end of frame; hide now
                if (Application.isPlaying) Destroy(v.gameObject); else DestroyImmediate(v.gameObject);
            }

            Sim = sim;
            var n = Sim.Balls.Count;
            ballViews = new BallView[n];
            prevPos = new Vector2[n];
            prevAngle = new float[n];
            for (var i = 0; i < n; i++)
            {
                var id = Sim.Balls[i].Weapon.Id;
                ballViews[i] = BallView.Create(transform, i, id, Art.Get(id), Art.BallFlash, BodyOrderBase + i * 2, BladeOrderBase + i);
                ballViews[i].ArtLibraryRef = Art;
                hud.Bind(i, id, i == 0 ? tagA : (i == 1 ? tagB : null));
                ballViews[i].SetMarked(i == MarkedBall);
            }
            ApplyBossLook();
            badges.Bind(Sim);
            SnapshotPrevious();
            accumulator = 0f;
            endTimer = 0f;
            endReported = false;
            Draw(0f, 0f);   // new views show their real pose this frame, not the origin
            MatchStarted?.Invoke();
        }

        void Update() => Advance(Time.deltaTime);

        /// <summary>One frame of <paramref name="frameSeconds"/> (Update's body; tests drive it with a fixed step).</summary>
        public void Advance(float frameSeconds)
        {
            if (Sim == null) return;
            var dt = Mathf.Min(frameSeconds, MaxFrameSeconds);
            if (Paused)
            {
                accumulator = 0f;
                Draw(0f, dt);
                return;
            }
            accumulator += dt * TimeScale;

            stepping = true;
            try
            {
                while (accumulator >= TickSeconds && pending == null)
                {
                    accumulator -= TickSeconds;
                    SnapshotPrevious();
                    Sim.Step();
                    var events = Sim.Events;
                    for (var i = 0; i < events.Count; i++) SimEventRaised?.Invoke(events[i]);
                    if (Sim.Outcome != MatchOutcome.Ongoing && !endReported)
                    {
                        endReported = true;
                        MatchEnded?.Invoke(Sim.WinnerIndex);
                    }
                }
            }
            finally
            {
                stepping = false;
            }

            if (pending != null)
            {
                var next = pending;
                pending = null;
                StartMatch(next, pendingTagA, pendingTagB);
                return;
            }

            Draw(accumulator / TickSeconds, dt);

            if (Sim.Outcome != MatchOutcome.Ongoing && AutoRestart)
            {
                endTimer += dt;
                if (endTimer >= RestartDelaySeconds) StartMatch(WeaponA, WeaponB, Seed + 1);
            }
        }

        void SnapshotPrevious()
        {
            for (var i = 0; i < Sim.Balls.Count; i++)
            {
                var b = Sim.Balls[i];
                prevPos[i] = new Vector2(b.Pos.X, b.Pos.Y);
                prevAngle[i] = b.WeaponAngleDeg;
            }
        }

        void Draw(float alpha, float dt)
        {
            frame.Render(Sim.Arena, Sim.ActiveTick, Sim.Config, ArenaId);
            for (var i = 0; i < Sim.Balls.Count; i++)
            {
                var b = Sim.Balls[i];
                var pos = Vector2.Lerp(prevPos[i], new Vector2(b.Pos.X, b.Pos.Y), alpha);
                var angle = Mathf.LerpAngle(prevAngle[i], b.WeaponAngleDeg, alpha);
                ballViews[i].FitRadius(b.Radius);   // x1.2/x1.35/x1.5: pre-drawn sprite at scale 1; other sizes scale the base sprite
                ballViews[i].Render(pos, angle, b.Weapon.BladeInner + b.BladeShift, b.Weapon.BladeLength, b.Weapon.StatValue, b.Alive, dt, b.HasTwinBlade ? b.Weapon.BladeLength * b.TwinBladeScale : 0f, b.Weapon.BladeThickness);
                statusView.Render(i, b, pos, Sim.ActiveTick);
            }
            projectileView.Render(Sim);
            hud.Render(Sim, dt);
        }

        /// <summary>Sim coordinates (native pixels, arena-centred) to world space, for effects.</summary>
        public Vector3 SimToWorld(Vec2 p) => transform.TransformPoint(new Vector3(p.X, p.Y, 0f));
    }
}
