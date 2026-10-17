using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-14 visuals of the tribute balls. BGLGXLQ: five colour orbs circling the ball (lit = bright, unlit = dim) and the UNITY
    /// ring (five colour bands expanding from the ball). The snake: its tail (discs in the ball colour, smaller toward the end,
    /// under the balls) and the prey (small pulsing gold dots). Fixed pools, reads the Sim only.
    /// </summary>
    public sealed class TributeView : MonoBehaviour
    {
        public const int TailOrder = 8, PreyOrder = 9, OrbOrder = 30, RingOrder = 31;
        public static readonly Color32[] RingColors =
        {
            new Color32(255, 120, 190, 255),   // PINK
            new Color32(110, 220, 110, 255),   // GREEN
            new Color32(80, 150, 255, 255),    // BLUE
            new Color32(255, 214, 70, 255),    // YELLOW
            new Color32(240, 70, 60, 255),     // RED
        };
        public static readonly Color32 PreyColor = new Color32(255, 230, 120, 255);
        const int MaxBalls = 4, RingDots = 40;
        const float RingSeconds = 0.4f;

        Sprite disc;
        SpriteRenderer[,] orbs, tail, beads, prey;
        const int BeadsPerGap = 3;   // small discs along the path between segments, so the tail reads as one body
        SpriteRenderer[] ring;
        float ringT = -1f, ringRadius;
        Vector2 ringCentre;

        /// <summary>Orbs lit for ball 0..3 (tests).</summary>
        public int OrbsLit(int ball)
        {
            var n = 0;
            for (var c = 0; c < ChorusRule.Colors; c++) if (orbs[ball, c].enabled && orbs[ball, c].color.a > 0.9f) n++;
            return n;
        }
        public int TailShown(int ball)
        {
            var n = 0;
            for (var k = 0; k < tail.GetLength(1); k++) if (tail[ball, k].enabled) n++;
            return n;
        }
        public int BeadsShown(int ball)
        {
            var n = 0;
            for (var k = 0; k < beads.GetLength(1); k++) if (beads[ball, k].enabled) n++;
            return n;
        }
        public int PreyShown(int ball)
        {
            var n = 0;
            for (var k = 0; k < prey.GetLength(1); k++) if (prey[ball, k].enabled) n++;
            return n;
        }
        public bool RingPlaying => ringT >= 0f;

        public static TributeView Create(Transform parent, Sprite disc)
        {
            var go = new GameObject("Tribute");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TributeView>();
            v.disc = disc;
            v.orbs = new SpriteRenderer[MaxBalls, ChorusRule.Colors];
            v.tail = new SpriteRenderer[MaxBalls, TributeTuning.SerpentMaxSegments];
            v.prey = new SpriteRenderer[MaxBalls, TributeTuning.SerpentPreyMax];
            v.beads = new SpriteRenderer[MaxBalls, TributeTuning.SerpentMaxSegments * BeadsPerGap];
            for (var b = 0; b < MaxBalls; b++)
            {
                for (var c = 0; c < ChorusRule.Colors; c++) v.orbs[b, c] = v.Disc(go.transform, "Orb", OrbOrder);
                for (var k = 0; k < TributeTuning.SerpentMaxSegments; k++) v.tail[b, k] = v.Disc(go.transform, "Tail", TailOrder);
                for (var k = 0; k < TributeTuning.SerpentPreyMax; k++) v.prey[b, k] = v.Disc(go.transform, "Prey", PreyOrder);
                for (var k = 0; k < TributeTuning.SerpentMaxSegments * BeadsPerGap; k++) v.beads[b, k] = v.Disc(go.transform, "Bead", TailOrder - 1);
            }
            v.ring = new SpriteRenderer[RingDots];
            for (var i = 0; i < RingDots; i++) v.ring[i] = v.Disc(go.transform, "Ring", RingOrder);
            return v;
        }

        SpriteRenderer Disc(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = disc;
            sr.sortingOrder = order;
            sr.enabled = false;
            return sr;
        }

        static void Place(SpriteRenderer sr, Vector2 centre, float diameter, Color32 color)
        {
            sr.enabled = true;
            sr.color = color;
            sr.transform.localPosition = new Vector3(Mathf.Round(centre.x), Mathf.Round(centre.y), 0f);
            var s = diameter / 32f;   // the disc sprite is 32 px across
            sr.transform.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>A UNITY burst from the event: starts the ring at the ball.</summary>
        public void OnEvent(SimEvent e, MatchSim sim)
        {
            if (e.Type != SimEventType.ChorusUnity || e.A < 0 || e.A >= sim.Balls.Count) return;
            ringT = 0f;
            ringCentre = new Vector2(e.Point.X.ToFloat(), e.Point.Y.ToFloat());
            ringRadius = TributeTuning.ChorusUnityRadius.ToFloat();
        }

        /// <param name="pos">Interpolated ball positions (as drawn this frame).</param>
        public void Render(MatchSim sim, Vector2[] pos, float dt)
        {
            for (var b = 0; b < MaxBalls; b++)
            {
                var ball = b < sim.Balls.Count ? sim.Balls[b] : null;
                var alive = ball != null && ball.Alive;
                var chorus = alive ? ball.Weapon as ChorusRule : null;
                var snake = alive ? ball.Weapon as SerpentRule : null;
                for (var c = 0; c < ChorusRule.Colors; c++)
                {
                    if (chorus == null) { orbs[b, c].enabled = false; continue; }
                    // BB-18: the orbs are Sim hitboxes now; draw them where the Sim has them, around the drawn ball
                    var o = chorus.Orb(c + 1) - ball.Pos;
                    var p = pos[b] + new Vector2(o.X.ToFloat(), o.Y.ToFloat());
                    Color32 col = RingColors[c];
                    var lit = chorus.IsLit(c + 1);
                    if (!lit) col = new Color32((byte)(col.r * 0.6f), (byte)(col.g * 0.6f), (byte)(col.b * 0.6f), 210);
                    var d = TributeTuning.ChorusOrbRadius.ToFloat() * 2f;
                    Place(orbs[b, c], p, lit ? d + 2f : d - 1f, col);
                }
                for (var k = 0; k < TributeTuning.SerpentMaxSegments; k++)
                {
                    if (snake == null || k >= snake.Segments) { tail[b, k].enabled = false; continue; }
                    var s = snake.Segment(k + 1);
                    var d = Mathf.Lerp(TributeTuning.SerpentSegmentRadius.ToFloat() * 2.4f, TributeTuning.SerpentSegmentRadius.ToFloat() * 1.4f, k / 7f);
                    Color32 body = Palette.Look(ball.Weapon.Id).Body;
                    var shade = k % 2 == 0 ? 1f : 0.8f;
                    Place(tail[b, k], new Vector2(s.X.ToFloat(), s.Y.ToFloat()), d, new Color32((byte)(body.r * shade), (byte)(body.g * shade), (byte)(body.b * shade), 255));
                }
                var spacing = TributeTuning.SerpentSegmentSpacing;
                for (var k = 0; k < TributeTuning.SerpentMaxSegments * BeadsPerGap; k++)
                {
                    var gap = k / BeadsPerGap;   // between segment gap and gap + 1 (0 = the head)
                    if (snake == null || gap >= snake.Segments) { beads[b, k].enabled = false; continue; }
                    var back = gap * spacing + (k % BeadsPerGap + 1) * spacing / (BeadsPerGap + 1);
                    var s = snake.Trail(back);
                    var d = Mathf.Lerp(TributeTuning.SerpentSegmentRadius.ToFloat() * 1.8f, TributeTuning.SerpentSegmentRadius.ToFloat() * 1.0f, back / (8f * spacing));
                    Color32 body = Palette.Look(ball.Weapon.Id).Body;
                    Place(beads[b, k], new Vector2(s.X.ToFloat(), s.Y.ToFloat()), d, new Color32((byte)(body.r * 0.7f), (byte)(body.g * 0.7f), (byte)(body.b * 0.7f), 255));
                }
                for (var k = 0; k < TributeTuning.SerpentPreyMax; k++)
                {
                    if (snake == null || k >= snake.PreyCount) { prey[b, k].enabled = false; continue; }
                    var p = snake.Prey(k);
                    var pulse = 0.75f + 0.25f * Mathf.Sin((sim.ActiveTick + k * 13) * 0.2f);
                    Place(prey[b, k], new Vector2(p.X.ToFloat(), p.Y.ToFloat()), TributeTuning.SerpentPreyRadius.ToFloat() * 2f * (0.8f + 0.4f * pulse),
                        new Color32(PreyColor.r, PreyColor.g, PreyColor.b, (byte)(255 * pulse)));
                }
            }
            RenderRing(dt);
        }

        void RenderRing(float dt)
        {
            if (ringT < 0f)
            {
                foreach (var d in ring) d.enabled = false;
                return;
            }
            ringT += dt;
            var t = ringT / RingSeconds;
            if (t >= 1f)
            {
                ringT = -1f;
                foreach (var d in ring) d.enabled = false;
                return;
            }
            var r = Mathf.Lerp(18f, ringRadius, 1f - (1f - t) * (1f - t));
            var alpha = (byte)(255 * (1f - t));
            for (var i = 0; i < RingDots; i++)
            {
                var a = i * Mathf.PI * 2f / RingDots;
                Color32 c = RingColors[i * ChorusRule.Colors / RingDots];
                Place(ring[i], ringCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, 5f, new Color32(c.r, c.g, c.b, alpha));
            }
        }
    }
}
