using System.Collections.Generic;
using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// TAP-4: "tap to strike" in Run fights. A press anywhere on the arena (or Space) asks the Sim for the player's strike
    /// (MatchSim.Tap(0), applied on the next tick); the Sim decides whether the cooldown allows it. Around the player's ball a
    /// 2 px arc fills as the cooldown runs (grey; red after a missed strike) and turns into a pulsing yellow ring when ready.
    /// PERFECT! = yellow word, sparks, a small shake and a 0.06 s hit-stop; a missed strike shows MISS. During the first
    /// seconds of a fight with no tap yet, "TAP TO STRIKE!" blinks at the bottom. View only: it reads the Sim and calls Tap.
    /// </summary>
    public sealed class TapView : MonoBehaviour
    {
        public const string PerfectWord = "PERFECT!";
        public const string MissWord = "MISS";
        public const string PromptText = "TAP TO STRIKE!";
        /// <summary>Tap target: the arena (x2 button below y -214 and the HUD above y 200 keep their own taps).</summary>
        public static readonly Rect TapArea = new Rect(-135, -212, 270, 412);
        public const int RingSegments = 24;
        /// <summary>Arc distance from the ball's visible edge (the player marker's dashed ring is at 3 px).</summary>
        public const int RingGap = 5;
        public const int RingThickness = 2;
        public const float HitStopSeconds = 0.06f;
        /// <summary>The prompt shows during this many active ticks of a fight while the player has not tapped (5 s).</summary>
        public const int PromptTicks = 300;
        /// <summary>Prompt row: in the gap between the arena's bottom wall and the FOE name.</summary>
        public const float PromptY = -134f;
        public const int RingOrder = 9;   // under the ball bodies (10+)
        static readonly Color32 Charging = new Color32(170, 170, 196, 170);
        static readonly Color32 ChargingAfterMiss = new Color32(255, 70, 50, 200);
        public static readonly Color32 ReadyColor = Palette.PrimaryFill;

        ArenaView arena;
        FxView fx;
        PixelButton button;
        Transform fxRoot;
        SpriteRenderer ring;
        PixelText prompt;
        PixelParticles particles;
        DamagePopups popups;
        float clock;
        bool active;
        static readonly Dictionary<int, Sprite[]> arcs = new Dictionary<int, Sprite[]>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() => arcs.Clear();

        /// <summary>The tap target is live (a Run fight is playing with the player's TapState).</summary>
        public bool Active => active;
        public PixelButton Button => button;
        public SpriteRenderer Ring => ring;
        public PixelText Prompt => prompt;
        public DamagePopups Popups => popups;
        public int PerfectsShown { get; private set; }
        public int MissesShown { get; private set; }
        public int TapsSent { get; private set; }

        public static TapView Create(Transform parent, ArenaView arena)
        {
            var go = new GameObject("Tap");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TapView>();
            v.arena = arena;
            v.fx = arena.GetComponent<FxView>();
            var art = arena.Art;
            v.button = PixelButton.Create(go.transform, "TapArea", art, TapArea, null, 1, new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), Palette.Text, 0);
            v.button.ClickCue = null;
            v.button.Pressed += v.OnPressed;
            v.prompt = PixelText.Create(go.transform, "TapPrompt", art.Font, new Vector2(0, PromptY), PixelText.Align.Center, 1, RunUi.Order - 5);
            v.prompt.Set(PromptText, ReadyColor);

            // Ring, sparks and words live in the arena's space (sim coordinates), like FxView's.
            v.fxRoot = new GameObject("TapFx").transform;
            v.fxRoot.SetParent(arena.transform, false);
            var r = new GameObject("TapRing");
            r.transform.SetParent(v.fxRoot, false);
            v.ring = r.AddComponent<SpriteRenderer>();
            v.ring.sortingOrder = RingOrder;
            v.particles = PixelParticles.Create(v.fxRoot, art.Pixel, 48);
            v.popups = DamagePopups.Create(v.fxRoot, art.Font, 2);
            arena.SimEventRaised += v.OnSimEvent;
            arena.MatchStarted += v.OnMatchStarted;
            v.SetActive(false);
            v.ring.enabled = false;
            v.prompt.gameObject.SetActive(false);
            return v;
        }

        void OnDestroy()
        {
            if (arena == null) return;
            arena.SimEventRaised -= OnSimEvent;
            arena.MatchStarted -= OnMatchStarted;
        }

        void OnMatchStarted()
        {
            particles.Clear();
            popups.Clear();
        }

        static BallBattle.Sim.Tap.TapState PlayerTap(MatchSim sim) => sim != null && sim.Balls.Count > 0 ? sim.Balls[0].Tap : null;

        void SetActive(bool on)
        {
            active = on;
            if (button.gameObject.activeSelf != on) button.gameObject.SetActive(on);
        }

        /// <summary>One frame: RunController calls it after the flow ticked (null flow = Run closed: hide everything).</summary>
        public void Tick(RunFlow flow, float dt)
        {
            clock += dt;
            var sim = arena != null ? arena.Sim : null;
            var tap = PlayerTap(sim);
            var state = flow != null ? flow.Current : RunFlow.State.Closed;
            var inFight = flow != null && tap != null && sim == flow.CurrentMatch &&
                          (state == RunFlow.State.Countdown || state == RunFlow.State.Playing);
            SetActive(inFight && state == RunFlow.State.Playing && sim.Outcome == MatchOutcome.Ongoing);
            if (active && Input.GetKeyDown(KeyCode.Space)) Request();

            var me = inFight ? sim.Balls[0] : null;
            var view = me != null ? arena.GetBallView(0) : null;
            ring.enabled = me != null && me.Alive && view != null && sim.Outcome == MatchOutcome.Ongoing;
            if (ring.enabled) RenderRing(tap, view);

            var showPrompt = active && tap.Taps == 0 && sim.ActiveTick < PromptTicks;
            if (prompt.gameObject.activeSelf != showPrompt) prompt.gameObject.SetActive(showPrompt);
            if (showPrompt) prompt.Set(PromptText, Mathf.Repeat(clock, 0.8f) < 0.55f ? ReadyColor : Palette.TextDim);

            popups.SetBounds(sim != null ? sim.Arena : (ArenaRect?)null);
            particles.Tick(dt);
            popups.Tick(dt);
        }

        void RenderRing(BallBattle.Sim.Tap.TapState tap, BallView view)
        {
            var radius = view.VisibleRadius + RingGap;
            var ready = tap.Ready;
            var fraction = ready ? 1f : Mathf.Clamp01(tap.ReadyFraction.ToFloat());
            var k = ready ? RingSegments : Mathf.Min(RingSegments - 1, Mathf.FloorToInt(fraction * RingSegments));
            ring.sprite = Arc(radius, k);
            ring.transform.localPosition = view.transform.localPosition;
            if (ready)
            {
                var c = ReadyColor;
                c.a = (byte)(Mathf.Lerp(150f, 255f, 0.5f + 0.5f * Mathf.Sin(clock * 10f)));
                ring.color = c;
            }
            else ring.color = tap.LastMissed ? ChargingAfterMiss : Charging;
        }

        /// <summary>Press on the arena: ask for the strike (the Sim ignores it while the cooldown runs).</summary>
        void OnPressed()
        {
            if (!active) return;
            Request();
        }

        public bool Request()
        {
            var sim = arena.Sim;
            if (!active || PlayerTap(sim) == null) return false;
            TapsSent++;
            return sim.Tap(0);
        }

        Vector2 Above(BallState b, float extra) => new Vector2(b.Pos.X.ToFloat(), b.Pos.Y.ToFloat() + b.Radius.ToFloat() + extra);

        void OnSimEvent(SimEvent e)
        {
            var sim = arena.Sim;
            if (sim == null || e.A != 0 || PlayerTap(sim) == null) return;
            switch (e.Type)
            {
                case SimEventType.TapStrike:
                    particles.Burst(new Vector2(e.Point.X.ToFloat(), e.Point.Y.ToFloat()), 6, ReadyColor, 40f, 90f, 0.2f);
                    break;
                case SimEventType.TapPerfect:
                {
                    var at = new Vector2(e.Point.X.ToFloat(), e.Point.Y.ToFloat());
                    var target = e.B >= 0 && e.B < sim.Balls.Count ? sim.Balls[e.B] : sim.Balls[0];
                    popups.ShowText(Above(target, 14f), PerfectWord, ReadyColor, sim.Arena, 2);
                    particles.Burst(at, 12, ReadyColor, 60f, 150f, 0.4f, 2);
                    particles.Burst(at, 6, Palette.Text, 40f, 110f, 0.3f);
                    fx?.Kick(2f, 0.12f);
                    arena.HitStop(HitStopSeconds);
                    PerfectsShown++;
                    break;
                }
                case SimEventType.TapMiss:
                    popups.ShowText(Above(sim.Balls[0], 14f), MissWord, Palette.TextDim, sim.Arena);
                    MissesShown++;
                    break;
            }
        }

        /// <summary>Arc of <paramref name="segments"/>/24 of a 2 px ring, clockwise from 12 o'clock. Cached per half-pixel radius.</summary>
        public static Sprite Arc(float radius, int segments)
        {
            var key = Mathf.RoundToInt(radius * 2f);
            if (!arcs.TryGetValue(key, out var set) || set == null) arcs[key] = set = new Sprite[RingSegments + 1];
            segments = Mathf.Clamp(segments, 0, RingSegments);
            if (set[segments] != null) return set[segments];
            var r = key * 0.5f;
            var size = 2 * Mathf.CeilToInt(r + RingThickness + 1f);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            t.SetPixels32(new Color32[size * size]);
            var c = size / 2f;
            var limit = 360f * segments / RingSegments;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - c;
                var dy = y + 0.5f - c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < r || d >= r + RingThickness) continue;
                var deg = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;   // 0 at 12 o'clock, clockwise
                if (deg < 0f) deg += 360f;
                if (deg < limit) t.SetPixel(x, y, Color.white);
            }
            return set[segments] = ProceduralSprites.Finish(t, "tap_arc_" + key + "_" + segments, new Vector2(0.5f, 0.5f));
        }
    }
}
