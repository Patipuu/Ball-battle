using System.Collections.Generic;
using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Floating damage numbers: rise ~14 px in whole-pixel steps, then fade. Two pooled sizes (big hits use 2x).
    /// Number strings are cached, so a repeated value never allocates again.
    /// A number that would start on top of one shown less than StackWindow ago is moved StackStep px above it
    /// (no more "1.4.3" pile-ups), and every number stays WallMargin px inside the arena for its whole rise.
    /// </summary>
    public sealed class DamagePopups : MonoBehaviour
    {
        /// <summary>Above every ball sprite (bodies 10+, blades 20+, marker arrow 32) and the walls (31), under menus.</summary>
        public const int Order = 45;
        /// <summary>Numbers stay at least this far inside the arena walls, for their whole rise.</summary>
        public const float WallMargin = 4f;
        public const float Duration = 0.7f;
        public const float Rise = 14f;
        const float BigHit = 10f;
        /// <summary>Numbers shown within this many seconds of each other, at nearly the same spot, are stacked.</summary>
        public const float StackWindow = 0.2f;
        /// <summary>Vertical step between stacked numbers (more than a 2x number's 10 px height).</summary>
        public const float StackStep = 12f;
        const int MaxStack = 16;

        sealed class Popup
        {
            public PixelText Text;
            public Vector2 Start;
            public float Age = Duration;
            public float ShownAt = float.NegativeInfinity;
            public int Scale;
            public Color32 Color;
            public string Value;
        }

        readonly List<Popup> small = new List<Popup>();
        readonly List<Popup> big = new List<Popup>();
        readonly Dictionary<int, string> numberCache = new Dictionary<int, string>();
        int nextSmall, nextBig;
        float clock;

        public static DamagePopups Create(Transform parent, Texture2D font, int perSize)
        {
            var go = new GameObject("DamagePopups");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DamagePopups>();
            for (var i = 0; i < perSize; i++)
            {
                d.small.Add(new Popup { Scale = 1, Text = PixelText.Create(go.transform, "s", font, Vector2.zero, PixelText.Align.Center, 1, Order) });
                d.big.Add(new Popup { Scale = 2, Text = PixelText.Create(go.transform, "b", font, Vector2.zero, PixelText.Align.Center, 2, Order) });
            }
            foreach (var p in d.small) p.Text.Set("", default);
            foreach (var p in d.big) p.Text.Set("", default);
            return d;
        }

        /// <summary>Formats like 7, 1.4, 12 (one decimal only when it matters). Cached by value×10.</summary>
        public string Format(float damage)
        {
            var key = Mathf.RoundToInt(damage * 10f);
            if (numberCache.TryGetValue(key, out var s)) return s;
            s = key % 10 == 0 ? (key / 10).ToString() : (key / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            numberCache[key] = s;
            return s;
        }

        /// <summary>Text size used for a value: 2x for big hits, unless <paramref name="scale"/> forces one (1 or 2).</summary>
        public static int ScaleFor(float damage, int scale = 0) => scale == 1 || scale == 2 ? scale : (damage >= BigHit ? 2 : 1);

        /// <summary>
        /// Show a number kept inside <paramref name="arena"/> (WallMargin px from every wall, rise included) and stacked
        /// above recent numbers at the same spot. <paramref name="scale"/> 0 = automatic (2x for big hits).
        /// </summary>
        public void Show(Vector2 at, float damage, Color32 color, ArenaRect arena, int scale = 0)
        {
            var s = ScaleFor(damage, scale);
            Spawn(Place(at, Format(damage), s, arena), damage, color, s);
        }

        /// <summary>Same without an arena (no wall clamp; stacking still applies).</summary>
        public void Show(Vector2 at, float damage, Color32 color, int scale = 0)
        {
            var s = ScaleFor(damage, scale);
            Spawn(Place(at, Format(damage), s, null), damage, color, s);
        }

        /// <summary>
        /// Where a new number of <paramref name="scale"/> starts: moved StackStep px above any number shown less than
        /// StackWindow ago that it would overlap, then clamped into the arena. If the clamp pushes it back onto a
        /// number (against the top wall), it goes below that number instead.
        /// </summary>
        public Vector2 Place(Vector2 at, string text, int scale, ArenaRect? arena)
        {
            var p = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
            for (var i = 0; i < MaxStack; i++)
            {
                var hit = RecentOverlap(p, text, scale);
                if (hit == null) break;
                p.y = hit.Start.y + StackStep;
            }
            if (!arena.HasValue) return p;
            var clamped = ClampToArena(p, text, scale, arena.Value);
            if (clamped == p) return p;
            p = clamped;
            for (var i = 0; i < MaxStack; i++)
            {
                var hit = RecentOverlap(p, text, scale);
                if (hit == null) break;
                p.y = hit.Start.y - StackStep;
            }
            return ClampToArena(p, text, scale, arena.Value);
        }

        Popup RecentOverlap(Vector2 start, string text, int scale)
        {
            var mine = StartRect(start, text, scale);
            Popup best = null;
            foreach (var list in new[] { small, big })
                foreach (var p in list)
                {
                    if (p.Age >= Duration || clock - p.ShownAt >= StackWindow) continue;
                    if (!Expand(StartRect(p.Start, p.Value, p.Scale), 1f).Overlaps(mine)) continue;
                    if (best == null || p.Start.y > best.Start.y) best = p;   // step past the highest one hit
                }
            return best;
        }

        /// <summary>Area a centred number covers at its start position.</summary>
        public static Rect StartRect(Vector2 start, string text, int scale)
        {
            var width = PixelFontData.MeasureWidth(text) * scale;
            return new Rect(start.x - width / 2, start.y, width, PixelFontData.GlyphHeight * scale);
        }

        static Rect Expand(Rect r, float by) => new Rect(r.xMin - by, r.yMin - by, r.width + 2f * by, r.height + 2f * by);

        /// <summary>
        /// Where a centred number of <paramref name="scale"/> must start so that its glyphs, during the whole rise,
        /// stay WallMargin px inside the arena. A number wider than the arena stays centred on it.
        /// </summary>
        public static Vector2 ClampToArena(Vector2 at, string text, int scale, ArenaRect arena)
        {
            var width = PixelFontData.MeasureWidth(text) * scale;
            var leftOffset = width / 2;                      // PixelText.Align.Center starts at -(width / 2)
            var height = PixelFontData.GlyphHeight * scale;
            var minX = arena.Left + WallMargin + leftOffset;
            var maxX = arena.Right - WallMargin - (width - leftOffset);
            var minY = arena.Bottom + WallMargin;
            var maxY = arena.Top - WallMargin - Rise - height;
            var x = Mathf.Round(at.x);
            var y = Mathf.Round(at.y);
            x = minX > maxX ? Mathf.Round((arena.Left + arena.Right) * 0.5f) : Mathf.Clamp(x, Mathf.Ceil(minX), Mathf.Floor(maxX));
            y = minY > maxY ? Mathf.Round(minY) : Mathf.Clamp(y, Mathf.Ceil(minY), Mathf.Floor(maxY));
            return new Vector2(x, y);
        }

        void Spawn(Vector2 start, float damage, Color32 color, int scale)
        {
            Popup p;
            if (scale >= 2) { p = big[nextBig]; nextBig = (nextBig + 1) % big.Count; }
            else { p = small[nextSmall]; nextSmall = (nextSmall + 1) % small.Count; }
            p.Start = new Vector2(Mathf.Round(start.x), Mathf.Round(start.y));
            p.Age = 0f;
            p.ShownAt = clock;
            p.Color = color;
            p.Value = Format(damage);
            p.Text.transform.localPosition = p.Start;
            p.Text.Set(p.Value, color);
        }

        public void Clear()
        {
            foreach (var p in small) Hide(p);
            foreach (var p in big) Hide(p);
        }

        static void Hide(Popup p)
        {
            p.Age = Duration;
            p.Text.Set("", default);
        }

        /// <summary>Numbers on screen now: value, scale, start position (tests).</summary>
        public List<(string value, int scale, Vector2 start)> Showing()
        {
            var list = new List<(string, int, Vector2)>();
            foreach (var l in new[] { small, big })
                foreach (var p in l)
                    if (p.Age < Duration) list.Add((p.Value, p.Scale, p.Start));
            return list;
        }

        public void Tick(float dt)
        {
            clock += dt;
            Advance(small, dt);
            Advance(big, dt);
        }

        static void Advance(List<Popup> list, float dt)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Age >= Duration) continue;
                p.Age += dt;
                if (p.Age >= Duration) { Hide(p); continue; }
                var t = p.Age / Duration;
                var y = p.Start.y + Mathf.Round(Rise * (1f - (1f - t) * (1f - t)));   // ease-out
                p.Text.transform.localPosition = new Vector3(p.Start.x, y, 0f);
                var c = p.Color;
                c.a = (byte)(t < 0.5f ? 255 : 255f * (1f - t) / 0.5f);
                p.Text.Set(p.Value, c);
            }
        }
    }
}
