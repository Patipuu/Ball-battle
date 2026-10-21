using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Per-chapter fight backdrop (Phu 21/10: every chapter has its own background concept). Drawn procedurally, 1 px = 1 unit,
    /// point-filtered, around the arena only (the arena itself is a transparent hole), so floors, balls and HUD are untouched.
    /// Colours stay dark and low-contrast so the HUD text over the top and bottom bands keeps reading clearly.
    ///   0 TRAINING GROUND: chalkboard green, faint court lines and X / O drill marks.
    ///   1 IRON FORT: steel bricks with rivets.
    ///   2 EMBER WOODS: smoky red-brown, pine silhouettes and drifting embers.
    ///   3 BELL TOWER: night purple, stars, a moon and hanging bells over the tower stones.
    /// </summary>
    public static class ChapterBackdrop
    {
        public const int Width = 270, Height = 480;
        public const int Count = 4;

        /// <summary>Plain colour of each chapter (the frame's outside masks use it, so the backdrop has no seam).</summary>
        public static readonly Color32[] BaseColors =
        {
            new Color32(20, 38, 30, 255),
            new Color32(28, 30, 38, 255),
            new Color32(34, 18, 16, 255),
            new Color32(22, 16, 38, 255),
        };

        static readonly Color32[][] cache = new Color32[Count][];

        public static Color32 BaseColor(int chapter) => BaseColors[Clamp(chapter)];

        static int Clamp(int chapter) => chapter < 0 ? 0 : chapter >= Count ? Count - 1 : chapter;

        /// <summary>The chapter's full-screen pattern (row 0 = bottom), cached. Callers copy before changing it.</summary>
        public static Color32[] Pattern(int chapter)
        {
            chapter = Clamp(chapter);
            if (cache[chapter] != null) return cache[chapter];
            var px = new Color32[Width * Height];
            var c = BaseColors[chapter];
            for (var i = 0; i < px.Length; i++) px[i] = c;
            var rng = new Hash(0x5EEDu + (uint)chapter * 7919u);
            switch (chapter)
            {
                case 0: Training(px, rng); break;
                case 1: IronFort(px, rng); break;
                case 2: EmberWoods(px, rng); break;
                default: BellTower(px, rng); break;
            }
            return cache[chapter] = px;
        }

        /// <summary>
        /// Texture of the chapter with a transparent hole over the arena (<paramref name="hole"/> in units around the screen
        /// centre). The caller owns and destroys it.
        /// </summary>
        public static Texture2D Build(int chapter, RectInt hole)
        {
            var src = Pattern(chapter);
            var px = (Color32[])src.Clone();
            for (var y = hole.yMin; y < hole.yMax; y++)
            for (var x = hole.xMin; x < hole.xMax; x++)
            {
                var tx = x + Width / 2;
                var ty = y + Height / 2;
                if (tx >= 0 && tx < Width && ty >= 0 && ty < Height) px[ty * Width + tx] = new Color32(0, 0, 0, 0);
            }
            var tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "ChapterBackdrop" + chapter
            };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return tex;
        }

        // ---- Patterns ----

        static void Training(Color32[] px, Hash rng)
        {
            var line = new Color32(28, 52, 40, 255);
            var chalk = new Color32(52, 84, 66, 255);
            // Faint court lines: a centre line and two arcs worth of rows every 40 px.
            for (var y = 20; y < Height; y += 40) HLine(px, 0, Width, y, line);
            for (var x = 15; x < Width; x += 60) VLine(px, x, 0, Height, line);
            // Drill marks: X and O in chalk.
            for (var i = 0; i < 26; i++)
            {
                var x = rng.Range(6, Width - 6);
                var y = rng.Range(6, Height - 6);
                if (rng.Range(0, 2) == 0)
                    for (var d = -2; d <= 2; d++) { Set(px, x + d, y + d, chalk); Set(px, x + d, y - d, chalk); }
                else Ring(px, x, y, 3, chalk);
            }
            // A few dashed arrows.
            for (var i = 0; i < 6; i++)
            {
                var x = rng.Range(10, Width - 40);
                var y = rng.Range(10, Height - 10);
                for (var d = 0; d < 26; d += 4) { Set(px, x + d, y, chalk); Set(px, x + d + 1, y, chalk); }
                Set(px, x + 27, y + 1, chalk); Set(px, x + 27, y - 1, chalk); Set(px, x + 28, y, chalk);
            }
        }

        static void IronFort(Color32[] px, Hash rng)
        {
            var mortar = new Color32(20, 21, 27, 255);
            var light = new Color32(38, 41, 51, 255);
            var rivet = new Color32(62, 66, 80, 255);
            const int bh = 12, bw = 30;
            for (var row = 0; row * bh < Height; row++)
            {
                var y0 = row * bh;
                HLine(px, 0, Width, y0, mortar);
                HLine(px, 0, Width, y0 + 1, light);
                var off = (row % 2) * (bw / 2);
                for (var x = -off; x < Width; x += bw)
                {
                    VLine(px, x, y0, y0 + bh, mortar);
                    if (rng.Range(0, 3) == 0)
                    {
                        Set(px, x + 4, y0 + 6, rivet);
                        Set(px, x + bw - 4, y0 + 6, rivet);
                    }
                }
            }
        }

        static void EmberWoods(Color32[] px, Hash rng)
        {
            // Smoke: darker towards the top.
            for (var y = 0; y < Height; y++)
            {
                var k = y / (float)Height;
                var c = new Color32((byte)(36 - 12 * k), (byte)(19 - 7 * k), (byte)(16 - 5 * k), 255);
                HLine(px, 0, Width, y, c);
            }
            var tree = new Color32(18, 9, 9, 255);
            // Pines along the bottom and hanging from the top.
            for (var x = -6; x < Width + 6; x += rng.Range(14, 24))
            {
                var h = rng.Range(40, 90);
                Pine(px, x, 0, h, tree, 1);
                Pine(px, x + 8, Height - 1, rng.Range(30, 60), tree, -1);
            }
            var ember1 = new Color32(180, 70, 24, 255);
            var ember2 = new Color32(236, 150, 56, 255);
            for (var i = 0; i < 70; i++)
            {
                var x = rng.Range(0, Width);
                var y = rng.Range(0, Height);
                Set(px, x, y, i % 4 == 0 ? ember2 : ember1);
                if (i % 9 == 0) Set(px, x, y + 1, ember1);
            }
        }

        static void BellTower(Color32[] px, Hash rng)
        {
            for (var y = 0; y < Height; y++)
            {
                var k = y / (float)Height;
                var c = new Color32((byte)(26 - 8 * k), (byte)(18 - 6 * k), (byte)(42 - 8 * k), 255);
                HLine(px, 0, Width, y, c);
            }
            // Tower stones along the bottom band.
            var stone = new Color32(32, 24, 50, 255);
            var joint = new Color32(18, 13, 30, 255);
            for (var y = 0; y < 90; y++) HLine(px, 0, Width, y, stone);
            for (var row = 0; row * 10 < 90; row++)
            {
                HLine(px, 0, Width, row * 10, joint);
                for (var x = (row % 2) * 10; x < Width; x += 20) VLine(px, x, row * 10, row * 10 + 10, joint);
            }
            // Stars.
            var star = new Color32(150, 140, 190, 255);
            var bright = new Color32(214, 206, 240, 255);
            for (var i = 0; i < 90; i++)
            {
                var x = rng.Range(0, Width);
                var y = rng.Range(95, Height);
                Set(px, x, y, i % 7 == 0 ? bright : star);
            }
            // Moon, top left.
            Disc(px, 34, Height - 34, 11, new Color32(206, 200, 176, 255));
            Disc(px, 39, Height - 30, 9, BaseAt(px, 60, Height - 30));
            // Bells hanging from the top, right side.
            var bell = new Color32(120, 96, 52, 255);
            var rope = new Color32(60, 50, 40, 255);
            Bell(px, Width - 30, Height - 22, bell, rope);
            Bell(px, Width - 62, Height - 12, bell, rope);
        }

        // ---- Drawing helpers (row 0 = bottom) ----

        static void Set(Color32[] px, int x, int y, Color32 c)
        {
            if (x >= 0 && x < Width && y >= 0 && y < Height) px[y * Width + x] = c;
        }

        static Color32 BaseAt(Color32[] px, int x, int y) => px[Mathf.Clamp(y, 0, Height - 1) * Width + Mathf.Clamp(x, 0, Width - 1)];

        static void HLine(Color32[] px, int x0, int x1, int y, Color32 c) { for (var x = x0; x < x1; x++) Set(px, x, y, c); }
        static void VLine(Color32[] px, int x, int y0, int y1, Color32 c) { for (var y = y0; y < y1; y++) Set(px, x, y, c); }

        static void Ring(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            for (var y = -r; y <= r; y++)
            for (var x = -r; x <= r; x++)
            {
                var d = x * x + y * y;
                if (d <= r * r && d > (r - 1) * (r - 1)) Set(px, cx + x, cy + y, c);
            }
        }

        static void Disc(Color32[] px, int cx, int cy, int r, Color32 c)
        {
            for (var y = -r; y <= r; y++)
            for (var x = -r; x <= r; x++)
                if (x * x + y * y <= r * r) Set(px, cx + x, cy + y, c);
        }

        /// <summary>A pine of height <paramref name="h"/> growing from (x, y) up (dir 1) or down (dir -1).</summary>
        static void Pine(Color32[] px, int x, int y, int h, Color32 c, int dir)
        {
            for (var i = 0; i < h; i++)
            {
                var yy = y + dir * i;
                var half = i < 6 ? 1 : (h - i) * 10 / h + ((i / 4) % 2 == 0 ? 1 : 0);
                HLine(px, x - half, x + half + 1, yy, c);
            }
        }

        static void Bell(Color32[] px, int cx, int top, Color32 bell, Color32 rope)
        {
            VLine(px, cx, top, Height, rope);
            for (var i = 0; i < 12; i++)
            {
                var half = 2 + i / 2;
                HLine(px, cx - half, cx + half + 1, top - i, bell);
            }
            HLine(px, cx - 9, cx + 10, top - 12, bell);
            Set(px, cx, top - 13, bell);
            Set(px, cx, top - 14, rope);
        }

        /// <summary>Tiny deterministic hash rng (view only, never the Sim's).</summary>
        sealed class Hash
        {
            uint s;
            public Hash(uint seed) { s = seed == 0 ? 1u : seed; }
            uint Next() { s ^= s << 13; s ^= s >> 17; s ^= s << 5; return s; }
            public int Range(int lo, int hi) => hi <= lo ? lo : lo + (int)(Next() % (uint)(hi - lo));
        }
    }
}
