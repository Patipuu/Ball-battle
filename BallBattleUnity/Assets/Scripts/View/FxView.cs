using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Turns sim events into feel: hit flash, damage numbers, pixel sparks, integer-pixel screen shake,
    /// sounds, and a short slow-motion of the effects on a knockout. View-only: never touches the sim.
    /// Lives next to ArenaView and listens to its events.
    /// </summary>
    [RequireComponent(typeof(ArenaView))]
    public sealed class FxView : MonoBehaviour
    {
        public SfxLibrary Sfx;

        const float FlashSeconds = 0.06f;
        const float KoSlowMoSeconds = 0.5f;
        const float KoSlowMoScale = 0.3f;

        ArenaView arena;
        PixelParticles particles;
        DamagePopups popups;
        SfxPlayer sfx;
        ScreenShake shake;
        float slowMoRemaining;
        bool winPending;

        // Built in Awake (not Start) so effects work from the very first tick, whatever order the Starts run in.
        void Awake()
        {
            arena = GetComponent<ArenaView>();
            particles = PixelParticles.Create(transform, arena.Art.Pixel, 96);
            popups = DamagePopups.Create(transform, arena.Art.Font, 8);
            sfx = SfxPlayer.Create(transform, 8);
            var cam = Camera.main;
            if (cam != null) shake = new ScreenShake(cam.transform);
        }

        void OnEnable()
        {
            if (arena == null) arena = GetComponent<ArenaView>();
            arena.SimEventRaised += OnSimEvent;
            arena.MatchStarted += OnMatchStarted;
        }

        void OnDisable()
        {
            arena.SimEventRaised -= OnSimEvent;
            arena.MatchStarted -= OnMatchStarted;
            shake?.Stop();
        }

        void OnMatchStarted()
        {
            particles?.Clear();
            popups?.Clear();
            shake?.Stop();
            slowMoRemaining = 0f;
            winPending = false;
        }

        Color32 BallColor(int index) => Palette.Look(arena.Sim.Balls[index].Weapon.Id).Body;

        static Vector2 V(Vec2 p) => new Vector2(p.X, p.Y);

        void OnSimEvent(SimEvent e)
        {
            if (particles == null) return;   // events before Start (first frame) are ignored
            switch (e.Type)
            {
                case SimEventType.Hit:
                {
                    var attacker = arena.Sim.Balls[e.A];
                    var target = arena.Sim.Balls[e.B];
                    arena.GetBallView(e.B)?.Flash(FlashSeconds);
                    particles.Burst(V(e.Point), e.Value >= 5f ? 7 : 5, BallColor(e.A), 50f, 110f, 0.35f, e.Value >= 5f ? 2 : 1);
                    popups.Show(V(target.Pos) + new Vector2(0f, target.Radius + 4f), e.Value, Palette.Text);
                    shake?.Add(Mathf.Clamp(1f + e.Value / 6f, 1f, ScreenShake.MaxAmplitude), 0.15f);
                    var heat = Palette.Heat(attacker.Weapon.Id, attacker.Weapon.StatValue);
                    sfx.Play(Sfx != null ? Sfx.Hit : null, 0.6f, 1f + heat * 0.5f);
                    break;
                }
                case SimEventType.Parry:
                    particles.Burst(V(e.Point), 6, Palette.Text, 80f, 150f, 0.25f);
                    particles.Burst(V(e.Point), 3, Palette.WallWarning, 60f, 120f, 0.3f);
                    shake?.Add(1f, 0.08f);
                    sfx.Play(Sfx != null ? Sfx.Parry : null, 0.6f, Random.Range(0.95f, 1.05f));
                    break;
                case SimEventType.WallBounce:
                    sfx.Play(Sfx != null ? Sfx.Wall : null, 0.2f, Random.Range(0.9f, 1.1f));
                    break;
                case SimEventType.BallBounce:
                    sfx.Play(Sfx != null ? Sfx.Wall : null, 0.35f, 0.8f);
                    break;
                case SimEventType.Death:
                    particles.Burst(V(e.Point), 18, BallColor(e.A), 40f, 160f, 0.8f, 2);
                    particles.Burst(V(e.Point), 10, Palette.Text, 60f, 140f, 0.5f);
                    shake?.Add(ScreenShake.MaxAmplitude, 0.4f);
                    slowMoRemaining = KoSlowMoSeconds;
                    sfx.Play(Sfx != null ? Sfx.Death : null, 0.9f);
                    break;
                case SimEventType.MatchEnd:
                    winPending = e.A >= 0;   // played when the knockout slow-mo ends, not on top of the death sound
                    break;
            }
        }

        void Update()
        {
            if (particles == null) return;
            var dt = Time.deltaTime;
            var fxDt = dt;
            if (slowMoRemaining > 0f)
            {
                slowMoRemaining -= dt;
                fxDt *= KoSlowMoScale;
            }
            if (winPending && slowMoRemaining <= 0f)
            {
                winPending = false;
                sfx.Play(Sfx != null ? Sfx.Win : null, 0.6f);
            }
            particles.Tick(fxDt);
            popups.Tick(fxDt);
            shake?.Tick(dt);
        }
    }
}
