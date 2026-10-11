using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Small sprites drawn in code: the outline flashes (white, tinted by the renderer) and the player marker
    /// (arrow + dashed ring, docs/design/art/fix_marker_flash_v1.png). Built once, cached; pixels-per-unit 1,
    /// point filtered, like the rest of the art.
    /// </summary>
    public static class ProceduralSprites
    {
        /// <summary>Ball sprite size in native pixels (the placeholder balls are 32x32).</summary>
        public const int BallSize = 32;
        /// <summary>Outline flash thickness, in ball-sprite pixels (scaled with the ball).</summary>
        public const int OutlineThickness = 2;

        /// <summary>Player marker ring: 1 px, its pixels on the 3rd pixel out from the ball edge (2 px gap), 6° on / 6° off.</summary>
        public const int RingGapPixels = 2;
        public const float RingDashDegrees = 6f;

        /// <summary>Marker arrow: white part 9 px wide, 6 px tall, pointing down, 1 px dark outline (20,18,28).</summary>
        public const int ArrowWhiteWidth = 9;
        public const int ArrowWhiteHeight = 6;
        public static readonly Color32 ArrowWhite = new Color32(255, 255, 255, 255);
        public static readonly Color32 ArrowOutline = new Color32(20, 18, 28, 255);

        /// <summary>
        /// The arrow exactly as in the mockup (top row first): o = outline, W = white. 11x8. The bottom row is the
        /// outline under the tip; the tip white pixel is row 6 (column 5).
        /// </summary>
        public static readonly string[] ArrowRows =
        {
            "ooooooooooo",
            ".WWWWWWWWW.",
            ".oWWWWWWWo.",
            "..oWWWWWo..",
            "...WWWWW...",
            "....WWW....",
            "....oWo....",
            ".....o.....",
        };

        /// <summary>Column of the tip in ArrowRows (the arrow is centred on it).</summary>
        public const int ArrowTipColumn = 5;
        /// <summary>Rows under the tip white pixel inside the sprite (its outline).</summary>
        public const int ArrowRowsBelowTip = 1;

        static Sprite outline, thinOutline, arrow;
        static readonly Dictionary<int, Sprite> rings = new Dictionary<int, Sprite>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache()
        {
            outline = null;
            thinOutline = null;
            arrow = null;
            rings.Clear();
        }

        /// <summary>Same disc as the ball flash, but only its outer <see cref="OutlineThickness"/> pixels.</summary>
        public static Sprite BallOutline => outline != null ? outline : (outline = Ring("ball_outline_flash", OutlineThickness));

        /// <summary>Outermost 1 px of the ball disc.</summary>
        public static Sprite BallOutlineThin => thinOutline != null ? thinOutline : (thinOutline = Ring("ball_outline_thin", 1));

        static Sprite Ring(string name, int thickness)
        {
            var t = New(BallSize, BallSize);
            const float r = 15.5f;
            for (var y = 0; y < BallSize; y++)
            for (var x = 0; x < BallSize; x++)
            {
                var dx = x + 0.5f - BallSize / 2f;
                var dy = y + 0.5f - BallSize / 2f;
                var d2 = dx * dx + dy * dy;
                if (d2 <= r * r && d2 > (r - thickness) * (r - thickness)) t.SetPixel(x, y, Color.white);
            }
            return Finish(t, name, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Dashed ring around a ball whose visible disc has radius <paramref name="visibleRadius"/> (native px, as drawn):
        /// 1 px thick, its pixels 3 px out from the ball's edge pixels (2 px gap, as in the mockup), 6° drawn / 6° empty.
        /// Fixed orientation (the marker never rotates). Cached per half-pixel radius.
        /// </summary>
        public static Sprite DashedRing(float visibleRadius)
        {
            visibleRadius = Mathf.Max(1f, visibleRadius);
            var key = Mathf.RoundToInt(visibleRadius * 2f);
            if (rings.TryGetValue(key, out var cached) && cached != null) return cached;
            var vr = key * 0.5f;
            var size = 2 * Mathf.CeilToInt(vr + RingGapPixels + 3f);
            var t = New(size, size);
            var c = size / 2f;
            var inner = vr + RingGapPixels;   // edge pixel centres sit at <= vr - 0.5; ring pixel centres 3 px further out
            var outer = inner + 1f;
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - c;
                var dy = y + 0.5f - c;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d < inner || d >= outer) continue;
                if (IsDash(dx, dy)) t.SetPixel(x, y, Color.white);
            }
            var s = Finish(t, "player_ring_" + key, new Vector2(0.5f, 0.5f));
            rings[key] = s;
            return s;
        }

        /// <summary>Radius of the disc actually drawn in a 32 px ball sprite (its top and side rows are transparent).</summary>
        public const float BallDiscRadius = 15.5f;
        /// <summary>The 32 px ball's topmost drawn row ends this many sprite px below the sprite's top edge.</summary>
        public const float BallTopInset = 1f;
        /// <summary>True when the angle of (dx, dy) falls in a drawn 6° dash (even 6° slices from 0°).</summary>
        public static bool IsDash(float dx, float dy)
        {
            var deg = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            if (deg < 0f) deg += 360f;
            return ((int)(deg / RingDashDegrees)) % 2 == 0;
        }

        /// <summary>The marker arrow (11x8 with its outline). Pivot: bottom edge, left edge of the tip column.</summary>
        public static Sprite DownArrow
        {
            get
            {
                if (arrow != null) return arrow;
                var w = ArrowRows[0].Length;
                var h = ArrowRows.Length;
                var t = New(w, h);
                for (var r = 0; r < h; r++)
                for (var x = 0; x < w; x++)
                {
                    var ch = ArrowRows[r][x];
                    if (ch == 'W') t.SetPixel(x, h - 1 - r, ArrowWhite);
                    else if (ch == 'o') t.SetPixel(x, h - 1 - r, ArrowOutline);
                }
                return arrow = Finish(t, "player_arrow", new Vector2(ArrowTipColumn / (float)w, 0f));
            }
        }

        static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            t.SetPixels32(new Color32[w * h]);
            return t;
        }

        internal static Sprite Finish(Texture2D t, string name, Vector2 pivot)
        {
            t.Apply();
            t.name = name;
            var s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, 1f, 0, SpriteMeshType.FullRect);
            s.name = name;
            s.hideFlags = HideFlags.DontSave;
            return s;
        }
    }
}
