using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
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
        static readonly Color32 StatusPoison = new Color32(96, 255, 64, 255);
        static readonly Color32 StatusHeal = new Color32(80, 255, 160, 255);
        /// <summary>Reflect damage (Aegis, Thorns, Spiky): numbers, flash and particles.</summary>
        public static readonly Color32 ReflectColor = new Color32(255, 90, 200, 255);
        const float ReflectFlightSeconds = 0.18f;

        ArenaView arena;
        PixelParticles particles;
        DamagePopups popups;
        SfxPlayer sfx;
        ScreenShake shake;
        float slowMoRemaining;
        bool winPending;
        int lastShotSoundTick = -1;

        // Reflect damage is shown once its whole tick is known (Parry comes after the Damage it caused).
        readonly List<SimEvent> pendingTick = new List<SimEvent>();
        readonly List<ReflectFx> reflects = new List<ReflectFx>();
        readonly ReflectSourceClassifier classifier = new ReflectSourceClassifier();
        readonly SimOwners owners = new SimOwners();

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
            pendingTick.Clear();
            shake?.Stop();
        }

        void OnMatchStarted()
        {
            particles?.Clear();
            popups?.Clear();
            shake?.Stop();
            slowMoRemaining = 0f;
            winPending = false;
            lastShotSoundTick = -1;
            pendingTick.Clear();
        }

        Color32 BallColor(int index) => Palette.Look(arena.Sim.Balls[index].Weapon.Id).Body;

        static Vector2 V(Vec2 p) => new Vector2(p.X, p.Y);

        void OnSimEvent(SimEvent e)
        {
            if (particles == null) return;   // events before Start (first frame) are ignored
            if (pendingTick.Count > 0 && pendingTick[0].Tick != e.Tick) FlushReflects();
            pendingTick.Add(e);
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
                case SimEventType.ProjectileFired:
                    particles.Burst(V(e.Point), 2, BallColor(e.A), 20f, 50f, 0.15f);
                    if (e.Tick != lastShotSoundTick)   // six turrets fire together: one sound
                    {
                        lastShotSoundTick = e.Tick;
                        sfx.Play(Sfx != null ? Sfx.Wall : null, 0.12f, 1.7f);
                    }
                    break;
                case SimEventType.ProjectileHit:
                    if (e.Value > 0f)
                    {
                        var hurt = arena.Sim.Balls[e.B];
                        arena.GetBallView(e.B)?.Flash(FlashSeconds);
                        particles.Burst(V(e.Point), 4, BallColor(e.A), 40f, 90f, 0.25f);
                        popups.Show(V(hurt.Pos) + new Vector2(0f, hurt.Radius + 4f), e.Value, Palette.Text);
                        sfx.Play(Sfx != null ? Sfx.Hit : null, 0.35f, 1.35f);
                    }
                    break;
                case SimEventType.ProjectileDeflected:
                    particles.Burst(V(e.Point), 4, Palette.Text, 60f, 120f, 0.2f);
                    sfx.Play(Sfx != null ? Sfx.Parry : null, 0.3f, 1.3f);
                    break;
                case SimEventType.StatusTick:
                {
                    var sick = arena.Sim.Balls[e.A];
                    particles.Burst(V(sick.Pos), 3, StatusPoison, 20f, 60f, 0.3f);
                    popups.Show(V(sick.Pos) + new Vector2(0f, sick.Radius + 4f), e.Value, StatusPoison);
                    break;
                }
                case SimEventType.ShieldBlocked:
                    particles.Burst(V(e.Point), 6, Palette.Text, 60f, 130f, 0.3f);
                    sfx.Play(Sfx != null ? Sfx.Parry : null, 0.5f, 0.8f);
                    break;
                case SimEventType.Heal:
                    if (e.Value >= 0.5f)
                    {
                        var healed = arena.Sim.Balls[e.A];
                        popups.Show(V(healed.Pos) + new Vector2(0f, healed.Radius + 4f), e.Value, StatusHeal);
                        particles.Burst(V(healed.Pos), 3, StatusHeal, 15f, 50f, 0.4f);
                    }
                    break;
                case SimEventType.ObstacleHit:
                    sfx.Play(Sfx != null ? Sfx.Wall : null, 0.25f, e.Value > 0f ? 0.7f : 1.2f);
                    if (e.Value > 0f)
                    {
                        particles.Burst(V(e.Point), 4, Palette.Hot, 40f, 100f, 0.25f);
                        popups.Show(V(e.Point), e.Value, Palette.Hot);
                    }
                    break;
                case SimEventType.WallBounce:
                    if (e.Value > 0f) popups.Show(V(arena.Sim.Balls[e.A].Pos), e.Value, Palette.Hot);
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
                case SimEventType.Damage:
                    break;   // Reflect damage: shown by FlushReflects once the tick is complete
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
            FlushReflects();
            if (winPending && slowMoRemaining <= 0f)
            {
                winPending = false;
                sfx.Play(Sfx != null ? Sfx.Win : null, 0.6f);
            }
            particles.Tick(fxDt);
            popups.Tick(fxDt);
            shake?.Tick(dt);
        }

        /// <summary>Classify and show every Reflect damage of the buffered tick(s), then forget them.</summary>
        void FlushReflects()
        {
            if (pendingTick.Count == 0) return;
            reflects.Clear();
            var sim = arena != null ? arena.Sim : null;
            if (sim != null && particles != null)
            {
                owners.Sim = sim;
                classifier.Classify(pendingTick, owners, reflects);
            }
            pendingTick.Clear();
            for (var i = 0; i < reflects.Count; i++) ShowReflect(sim, reflects[i]);
            reflects.Clear();
        }

        void ShowReflect(MatchSim sim, in ReflectFx r)
        {
            var d = r.Damage;
            if (d.A >= sim.Balls.Count || d.B >= sim.Balls.Count) return;
            var src = sim.Balls[d.A];
            var hurt = sim.Balls[d.B];

            arena.GetBallView(d.B)?.Flash(FlashSeconds, ReflectColor);
            popups.Show(V(hurt.Pos) + new Vector2(0f, hurt.Radius + 4f), d.Value, ReflectColor);

            // 3-4 magenta motes fly from the reflecting ball to the one it hurts (spikes for Thorns).
            var to = V(hurt.Pos);
            var count = d.Value >= 2f ? 4 : 3;
            for (var k = 0; k < count; k++)
            {
                var start = V(src.Pos) + Random.insideUnitCircle * (src.Radius * 0.6f);
                var flight = ReflectFlightSeconds * Random.Range(0.85f, 1.15f);
                var travel = to - start;
                var vel = travel / flight;
                if (r.Source == ReflectSource.Thorns)
                {
                    var dir = travel.sqrMagnitude > 0.01f ? travel.normalized : Vector2.up;
                    for (var j = 0; j < 3; j++) particles.Emit(start - dir * j, vel, ReflectColor, flight);   // 3 px spike, tip first
                }
                else particles.Emit(start, vel, ReflectColor, flight);
            }

            var at = V(r.Point);
            switch (r.Source)
            {
                case ReflectSource.Spiky:   // ring bursting at the bump
                    for (var k = 0; k < 8; k++)
                    {
                        var a = k * Mathf.PI / 4f;
                        particles.Emit(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 70f, ReflectColor, 0.25f);
                    }
                    break;
                case ReflectSource.Aegis:   // streak running along the shield
                    if (src.Weapon.HasBlade)
                    {
                        var s0 = V(src.BladeStart);
                        var s1 = V(src.BladeEnd);
                        var along = s1 - s0;
                        var speed = along.sqrMagnitude > 0.01f ? along.normalized * 90f : Vector2.zero;
                        for (var k = 0; k < 6; k++)
                            particles.Emit(Vector2.Lerp(s0, s1, k / 5f), speed, ReflectColor, 0.08f + 0.03f * k);
                    }
                    else particles.Burst(at, 4, ReflectColor, 40f, 90f, 0.2f);
                    break;
                default:   // Thorns
                    particles.Burst(at, 3, ReflectColor, 40f, 90f, 0.2f);
                    break;
            }
        }

        /// <summary>What each ball of the live match owns, for the reflect classifier.</summary>
        sealed class SimOwners : ReflectSourceClassifier.IOwners
        {
            public MatchSim Sim;

            BallState Ball(int i) => Sim != null && i >= 0 && i < Sim.Balls.Count ? Sim.Balls[i] : null;

            public bool UsesAegis(int ball) => Ball(ball)?.Weapon is AegisRule;
            public bool HasThorns(int ball) => Has(Ball(ball), ThornsTrait.TraitId);
            public bool HasSpiky(int ball) => Has(Ball(ball), SpikyTrait.TraitId);
            public Vec2 Center(int ball) => Ball(ball)?.Pos ?? Vec2.Zero;

            static bool Has(BallState b, string traitId)
            {
                if (b?.Traits == null) return false;
                foreach (var t in b.Traits)
                    if (t != null && t.Id == traitId) return true;
                return false;
            }
        }
    }
}
