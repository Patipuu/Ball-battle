using System.Collections.Generic;
using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Floating damage numbers: rise ~14 px in whole-pixel steps, then fade. Two pooled sizes (big hits use 2x).
    /// Number strings are cached, so a repeated value never allocates again.
    /// Every number stays at least StackStep px from every other number on screen in the same columns, for their whole
    /// lives (even numbers of the same tick), and WallMargin px inside the arena for its whole rise.
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
        /// <summary>Kept for reference: stacking now applies to every number on screen, not only those this recent.</summary>
        public const float StackWindow = 0.2f;
        /// <summary>Minimum vertical gap between numbers in the same columns (more than a 2x number's 14 px ink height).</summary>
        public const float StackStep = 15f;

        /// <summary>
        /// Numbers are digits and signs: all their pixels are in the font's body rows (4..10 of the 13-row cell). Their box is
        /// that ink box, from InkBottom to InkTop above the text position (BB-FONT1; the marks/dot rows are always empty here).
        /// </summary>
        public static float InkBottom(int scale) => (PixelFont.CellHeight - 1 - PixelFont.BodyBottomRow) * scale;
        public static float InkTop(int scale) => (PixelFont.CellHeight - PixelFont.BodyTopRow) * scale;
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
            /// <summary>Optional small caption under the number ("2ND WIND"), moving and fading with it.</summary>
            public PixelText Label;
            public Color32 LabelColor;
        }

        /// <summary>Caption under a labelled number: scale 1, this many px below the number's bottom.</summary>
        public const int LabelGap = 2;
        const int LabelHeight = 5;
        readonly List<PixelText> labelPool = new List<PixelText>();
        int nextLabel;

        readonly List<Popup> small = new List<Popup>();
        readonly List<Popup> big = new List<Popup>();
        readonly Dictionary<int, string> numberCache = new Dictionary<int, string>();
        int nextSmall, nextBig;
        float clock;
        ArenaRect? bounds;

        /// <summary>Current arena (FxView sets it every frame): numbers never drift over a wall that closed in.</summary>
        public void SetBounds(ArenaRect? arena) => bounds = arena;

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
            for (var i = 0; i < 4; i++)
            {
                var l = PixelText.Create(go.transform, "label", font, Vector2.zero, PixelText.Align.Center, 1, Order);
                l.Set("", default);
                d.labelPool.Add(l);
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
        /// Where a new number of <paramref name="scale"/> starts so that it stays at least StackStep px from every number on
        /// screen it shares columns with, for both their lifetimes, then inside the arena.
        /// All numbers rise on the same ease-out curve, so: placed StackStep above another's CURRENT height the gap only
        /// grows; placed StackStep below another's START height it never shrinks under StackStep. A number is stacked
        /// above; if the top wall stops it, it is stacked below instead.
        /// </summary>
        public Vector2 Place(Vector2 at, string text, int scale, ArenaRect? arena)
        {
            var p = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
            if (arena.HasValue) p = ClampToArena(p, text, scale, arena.Value);
            for (var i = 0; i < MaxStack; i++)
            {
                var q = Conflict(p, text, scale);
                if (q == null) break;
                p.y = CurrentY(q) + StackStep + (q.Age > 0f ? 1f : 0f);   // +1: whole-pixel rounding of two different ages
            }
            if (!arena.HasValue) return p;
            var limited = ClampToArena(p, text, scale, arena.Value);
            if (limited.y >= p.y) return limited;
            p = limited;   // the top wall stopped it: go below the numbers it meets
            for (var i = 0; i < MaxStack; i++)
            {
                var q = Conflict(p, text, scale);
                if (q == null) break;
                p.y = q.Start.y - StackStep;
            }
            return ClampToArena(p, text, scale, arena.Value);
        }

        /// <summary>A number on screen that a new one starting at <paramref name="start"/> would come closer than StackStep to.</summary>
        Popup Conflict(Vector2 start, string text, int scale)
        {
            var mine = StartRect(start, text, scale);
            Popup best = null;
            foreach (var list in new[] { small, big })
                foreach (var p in list)
                {
                    if (p.Age >= Duration) continue;
                    var other = StartRect(p.Start, p.Value, p.Scale);
                    if (mine.xMax + 1f <= other.xMin || other.xMax + 1f <= mine.xMin) continue;   // different columns
                    var below = p.Start.y - StackStep;
                    var above = CurrentY(p) + StackStep;
                    if (start.y <= below || start.y >= above) continue;
                    if (best == null || CurrentY(p) > CurrentY(best)) best = p;   // step past the highest one hit
                }
            return best;
        }

        static float RiseAt(float age)
        {
            var t = Mathf.Clamp01(age / Duration);
            return Mathf.Round(Rise * (1f - (1f - t) * (1f - t)));   // ease-out
        }

        static float CurrentY(Popup p) => p.Start.y + RiseAt(p.Age);
        /// <summary>Area a centred number covers at its start position.</summary>
        public static Rect StartRect(Vector2 start, string text, int scale)
        {
            var width = PixelFont.MeasureWidth(text) * scale;
            return new Rect(start.x - width / 2, start.y + InkBottom(scale), width, InkTop(scale) - InkBottom(scale));
        }

        static Rect Expand(Rect r, float by) => new Rect(r.xMin - by, r.yMin - by, r.width + 2f * by, r.height + 2f * by);

        /// <summary>
        /// Where a centred number of <paramref name="scale"/> must start so that its glyphs, during the whole rise,
        /// stay WallMargin px inside the arena. A number wider than the arena stays centred on it.
        /// </summary>
        public static Vector2 ClampToArena(Vector2 at, string text, int scale, ArenaRect arena)
        {
            var width = PixelFont.MeasureWidth(text) * scale;
            var leftOffset = width / 2;                      // PixelText.Align.Center starts at -(width / 2)
            var height = InkTop(scale);   // ink top above the text position
            var minX = arena.Left.ToFloat() + WallMargin + leftOffset;
            var maxX = arena.Right.ToFloat() - WallMargin - (width - leftOffset);
            var minY = arena.Bottom.ToFloat() + WallMargin;
            var maxY = arena.Top.ToFloat() - WallMargin - Rise - height;
            var x = Mathf.Round(at.x);
            var y = Mathf.Round(at.y);
            x = minX > maxX ? Mathf.Round((arena.Left.ToFloat() + arena.Right.ToFloat()) * 0.5f) : Mathf.Clamp(x, Mathf.Ceil(minX), Mathf.Floor(maxX));
            y = minY > maxY ? Mathf.Round(minY) : Mathf.Clamp(y, Mathf.Ceil(minY), Mathf.Floor(maxY));
            return new Vector2(x, y);
        }

        /// <summary>
        /// A number given as text (e.g. "+20"), at <paramref name="scale"/>, with a small caption under it. Stacked and
        /// kept inside the arena like any number (the caption included).
        /// </summary>
        public void ShowLabelled(Vector2 at, string text, Color32 color, int scale, string label, Color32 labelColor, ArenaRect? arena)
        {
            var p = Place(at, text, scale, arena);
            if (arena.HasValue) p.y = Mathf.Max(p.y, Mathf.Ceil(arena.Value.Bottom.ToFloat() + WallMargin + LabelGap + LabelHeight));
            var popup = SpawnText(p, text, color, scale);
            var l = labelPool[nextLabel];
            nextLabel = (nextLabel + 1) % labelPool.Count;
            foreach (var other in new[] { small, big }) foreach (var o in other) if (o.Label == l) o.Label = null;   // a reused caption leaves its old number
            popup.Label = l;
            popup.LabelColor = labelColor;
            l.transform.localPosition = new Vector3(popup.Start.x, popup.Start.y - LabelGap - LabelHeight, 0f);
            l.Set(label, labelColor);
        }

        void Spawn(Vector2 start, float damage, Color32 color, int scale) => SpawnText(start, Format(damage), color, scale);

        Popup SpawnText(Vector2 start, string value, Color32 color, int scale)
        {
            Popup p;
            if (scale >= 2) { p = big[nextBig]; nextBig = (nextBig + 1) % big.Count; }
            else { p = small[nextSmall]; nextSmall = (nextSmall + 1) % small.Count; }
            p.Start = new Vector2(Mathf.Round(start.x), Mathf.Round(start.y));
            p.Age = 0f;
            p.ShownAt = clock;
            p.Color = color;
            p.Value = value;
            if (p.Label != null) { p.Label.Set("", default); p.Label = null; }
            p.Text.transform.localPosition = p.Start;
            p.Text.Set(p.Value, color);
            return p;
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
            if (p.Label != null) { p.Label.Set("", default); p.Label = null; }
        }

        /// <summary>Height a number shows at now (tests): start + rise so far, kept under the top wall.</summary>
        public List<(string value, int scale, Vector2 start, float y)> ShowingNow()
        {
            var list = new List<(string, int, Vector2, float)>();
            foreach (var l in new[] { small, big })
                foreach (var p in l)
                    if (p.Age < Duration) list.Add((p.Value, p.Scale, p.Start, p.Text.transform.localPosition.y));
            return list;
        }

        /// <summary>Caption shown under a number on screen with this text, or null (tests).</summary>
        public PixelText LabelOf(string value)
        {
            foreach (var l in new[] { small, big }) foreach (var p in l) if (p.Age < Duration && p.Value == value) return p.Label;
            return null;
        }

        /// <summary>Color of a number on screen (tests).</summary>
        public Color32? ColorOf(string value)
        {
            foreach (var l in new[] { small, big }) foreach (var p in l) if (p.Age < Duration && p.Value == value) return p.Color;
            return null;
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

        void Advance(List<Popup> list, float dt)
        {
            for (var i = 0; i < list.Count; i++)
            {
                var p = list[i];
                if (p.Age >= Duration) continue;
                p.Age += dt;
                if (p.Age >= Duration) { Hide(p); continue; }
                var t = p.Age / Duration;
                var y = CurrentY(p);
                if (bounds.HasValue) y = Mathf.Min(y, Mathf.Floor(bounds.Value.Top.ToFloat() - WallMargin - InkTop(p.Scale)));   // the walls may close in
                p.Text.transform.localPosition = new Vector3(p.Start.x, y, 0f);
                var c = p.Color;
                c.a = (byte)(t < 0.5f ? 255 : 255f * (1f - t) / 0.5f);
                p.Text.Set(p.Value, c);
                if (p.Label != null)
                {
                    p.Label.transform.localPosition = new Vector3(p.Start.x, y - LabelGap - LabelHeight, 0f);
                    var lc = p.LabelColor;
                    lc.a = c.a;
                    p.Label.Set(p.Label.Text, lc);
                }
            }
        }
    }
}
