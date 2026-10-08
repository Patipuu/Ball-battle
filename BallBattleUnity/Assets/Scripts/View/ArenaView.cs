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
        [Tooltip("Restart with the next seed a moment after a match ends (preview without the match flow).")]
        public bool AutoRestart = true;
        public float RestartDelaySeconds = 2f;

        /// <summary>Raised for every sim event, in tick order (effects/audio subscribe here).</summary>
        public event Action<SimEvent> SimEventRaised;
        /// <summary>Raised once when the current match ends: winner ball index, -1 on draw.</summary>
        public event Action<int> MatchEnded;
        /// <summary>Raised after a new match (and its ball views) has been created.</summary>
        public event Action MatchStarted;

        public BallView GetBallView(int index) => index >= 0 && index < ballViews.Length ? ballViews[index] : null;

        public MatchSim Sim { get; private set; }

        BallView[] ballViews = new BallView[0];
        ArenaFrameView frame;
        HudView hud;
        Vector2[] prevPos = new Vector2[0];
        float[] prevAngle = new float[0];
        float accumulator;
        float endTimer;
        bool endReported;
        bool stepping;
        bool hasPending;
        string pendingA, pendingB;
        uint pendingSeed;

        void Start()
        {
            EnsureInit();
            if (Sim == null) StartMatch(WeaponA, WeaponB, Seed);
        }

        void EnsureInit()
        {
            if (frame != null) return;
            if (Art == null) throw new InvalidOperationException("ArenaView needs an ArtLibrary (run BallBattle/Build Scenes)");
            frame = ArenaFrameView.Create(transform, Art.Pixel);
            hud = HudView.Create(transform, Art);
        }

        public void StartMatch(string weaponA, string weaponB, uint seed)
        {
            if (stepping)
            {
                hasPending = true;
                pendingA = weaponA;
                pendingB = weaponB;
                pendingSeed = seed;
                return;
            }

            EnsureInit();
            WeaponA = weaponA;
            WeaponB = weaponB;
            Seed = seed;
            foreach (var v in ballViews)
            {
                if (v == null) continue;
                v.gameObject.SetActive(false);   // Destroy is deferred to end of frame; hide now
                Destroy(v.gameObject);
            }

            Sim = new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(weaponA), WeaponRegistry.Create(weaponB) });
            var n = Sim.Balls.Count;
            ballViews = new BallView[n];
            prevPos = new Vector2[n];
            prevAngle = new float[n];
            for (var i = 0; i < n; i++)
            {
                var id = Sim.Balls[i].Weapon.Id;
                ballViews[i] = BallView.Create(transform, i, id, Art.Get(id), Art.BallFlash, BodyOrderBase + i * 2, BladeOrderBase + i);
                hud.Bind(i, id);
            }
            SnapshotPrevious();
            accumulator = 0f;
            endTimer = 0f;
            endReported = false;
            Draw(0f, 0f);   // new views show their real pose this frame, not the origin
            MatchStarted?.Invoke();
        }

        void Update()
        {
            if (Sim == null) return;
            var dt = Mathf.Min(Time.deltaTime, MaxFrameSeconds);
            accumulator += dt;

            stepping = true;
            try
            {
                while (accumulator >= TickSeconds && !hasPending)
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

            if (hasPending)
            {
                hasPending = false;
                StartMatch(pendingA, pendingB, pendingSeed);
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
            frame.Render(Sim.Arena, Sim.ActiveTick, Sim.Config);
            for (var i = 0; i < Sim.Balls.Count; i++)
            {
                var b = Sim.Balls[i];
                var pos = Vector2.Lerp(prevPos[i], new Vector2(b.Pos.X, b.Pos.Y), alpha);
                var angle = Mathf.LerpAngle(prevAngle[i], b.WeaponAngleDeg, alpha);
                ballViews[i].Render(pos, angle, b.Weapon.BladeInner, b.Weapon.BladeLength, b.Weapon.StatValue, b.Alive, dt);
            }
            hud.Render(Sim, dt);
        }

        /// <summary>Sim coordinates (native pixels, arena-centred) to world space, for effects.</summary>
        public Vector3 SimToWorld(Vec2 p) => transform.TransformPoint(new Vector3(p.X, p.Y, 0f));
    }
}
