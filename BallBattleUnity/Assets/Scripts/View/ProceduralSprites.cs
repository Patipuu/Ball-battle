using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Small white sprites drawn in code (tinted by the renderer): the big-ball outline flash and the player marker
    /// (dotted ring + arrow). Built once, cached; pixels-per-unit 1, point filtered, like the rest of the art.
    /// </summary>
    public static class ProceduralSprites
    {
        /// <summary>Ball sprite size in native pixels (the placeholder balls are 32x32).</summary>
        public const int BallSize = 32;
        /// <summary>Outline flash thickness, in ball-sprite pixels (scaled with the ball).</summary>
        public const int OutlineThickness = 2;
        /// <summary>Dotted ring: gap between ball edge and dots, and number of dots.</summary>
        public const float RingGap = 3.5f;
        public const int RingDots = 16;
        public const int RingSize = 42;

        static Sprite outline, thinOutline, ring, arrow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCache() { outline = null; thinOutline = null; ring = null; arrow = null; }

        /// <summary>Same disc as the ball flash, but only its outer <see cref="OutlineThickness"/> pixels.</summary>
        public static Sprite BallOutline
        {
            get
            {
                if (outline != null) return outline;
                var t = New(BallSize, BallSize);
                const float r = 15.5f;
                for (var y = 0; y < BallSize; y++)
                for (var x = 0; x < BallSize; x++)
                {
                    var dx = x + 0.5f - BallSize / 2f;
                    var dy = y + 0.5f - BallSize / 2f;
                    var d2 = dx * dx + dy * dy;
                    if (d2 <= r * r && d2 > (r - OutlineThickness) * (r - OutlineThickness)) t.SetPixel(x, y, Color.white);
                }
                return outline = Finish(t, "ball_outline_flash", new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>Outermost 1 px of the ball disc (reflect-damage flash: the body keeps its color).</summary>
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

        /// <summary>Ring of single-pixel dots just outside a default-size ball (player marker).</summary>
        public static Sprite DottedRing
        {
            get
            {
                if (ring != null) return ring;
                var t = New(RingSize, RingSize);
                var radius = BallSize / 2f + RingGap;
                for (var k = 0; k < RingDots; k++)
                {
                    var a = k * Mathf.PI * 2f / RingDots;
                    var x = Mathf.FloorToInt(RingSize / 2f + Mathf.Cos(a) * radius);
                    var y = Mathf.FloorToInt(RingSize / 2f + Mathf.Sin(a) * radius);
                    t.SetPixel(Mathf.Clamp(x, 0, RingSize - 1), Mathf.Clamp(y, 0, RingSize - 1), Color.white);
                }
                return ring = Finish(t, "player_ring", new Vector2(0.5f, 0.5f));
            }
        }

        /// <summary>7x4 arrow pointing down, pivot at the left edge of its tip column (whole-pixel placement).</summary>
        public static Sprite DownArrow
        {
            get
            {
                if (arrow != null) return arrow;
                string[] rows = { "#######", ".#####.", "..###..", "...#..." };   // top to bottom
                var t = New(7, rows.Length);
                for (var r = 0; r < rows.Length; r++)
                for (var x = 0; x < 7; x++)
                    if (rows[r][x] == '#') t.SetPixel(x, rows.Length - 1 - r, Color.white);
                return arrow = Finish(t, "player_arrow", new Vector2(3f / 7f, 0f));
            }
        }

        static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var clear = new Color32[w * h];
            t.SetPixels32(clear);
            return t;
        }

        static Sprite Finish(Texture2D t, string name, Vector2 pivot)
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
