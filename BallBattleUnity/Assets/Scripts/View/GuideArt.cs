using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BI, the guide ball (an original character, drawn in code): a mint ball with a leaf sprout, big eyes and pink cheeks.
    /// 48x48 px, point-filtered, 1 px = 1 unit. Faces: idle, blink, talking (mouth open).
    /// </summary>
    public static class GuideArt
    {
        public const int Size = 48;
        public enum Face { Idle, Blink, Talk }

        static readonly Sprite[] cache = new Sprite[3];

        static readonly Color32 Body = new Color32(86, 196, 170, 255), Shade = new Color32(58, 158, 138, 255), Light = new Color32(150, 232, 206, 255),
            Rim = new Color32(22, 52, 56, 255), Shine = new Color32(235, 255, 245, 255), Stem = new Color32(60, 120, 50, 255),
            Leaf = new Color32(120, 200, 80, 255), LeafDark = new Color32(70, 150, 60, 255), White = new Color32(250, 250, 250, 255),
            Pupil = new Color32(30, 30, 50, 255), Cheek = new Color32(240, 140, 150, 255), MouthIn = new Color32(120, 40, 50, 255),
            Tongue = new Color32(240, 120, 130, 255);

        public static Sprite Get(Face face)
        {
            var i = (int)face;
            if (cache[i] != null) return cache[i];
            var px = Draw(face);
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, name = "GuideBi" + face };
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return cache[i] = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 1f);
        }

        /// <summary>Pixels (row 0 = bottom, as Unity textures). Drawn top-down, flipped when written.</summary>
        public static Color32[] Draw(Face face)
        {
            var px = new Color32[Size * Size];
            void S(int x, int y, Color32 c) { if (x >= 0 && x < Size && y >= 0 && y < Size) px[(Size - 1 - y) * Size + x] = c; }

            const float cx = 23.5f, cy = 26.5f, r = 18.5f;
            for (var y = 0; y < Size; y++)
            for (var x = 0; x < Size; x++)
            {
                var d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                if (d > r) continue;
                var k = ((x - cx) * 0.6f + (y - cy) * 0.8f) / r;   // light from the top left
                var c = Body;
                if (k > 0.5f) c = Shade;
                if (k < -0.55f && d < r - 2) c = Light;
                if (d > r - 1.3f) c = Rim;
                S(x, y, c);
            }
            foreach (var p in new[] { (13, 15), (14, 15), (13, 16), (15, 14), (16, 14) }) S(p.Item1, p.Item2, Shine);
            for (var y = 4; y < 9; y++) S(24, y, Stem);
            foreach (var p in new[] { (25, 4), (26, 3), (27, 3), (28, 3), (29, 4), (26, 4), (27, 4), (28, 4), (26, 5), (27, 5) }) S(p.Item1, p.Item2, Leaf);
            foreach (var p in new[] { (23, 5), (22, 5), (21, 5), (20, 6), (21, 6), (22, 6), (22, 7) }) S(p.Item1, p.Item2, LeafDark);

            foreach (var ex in new[] { 17, 30 })
            {
                if (face == Face.Blink)
                {
                    for (var x = ex - 3; x <= ex + 3; x++) S(x, 25, Rim);
                    continue;
                }
                for (var y = 20; y < 30; y++)
                for (var x = ex - 3; x <= ex + 3; x++)
                {
                    var a = (x - ex) / 3.6f; var b = (y - 24.5f) / 4.8f;
                    if (a * a + b * b <= 1f) S(x, y, White);
                }
                for (var y = 22; y < 29; y++)
                for (var x = ex - 2; x <= ex + 2; x++)
                {
                    var a = (x - ex - 0.3f) / 2.3f; var b = (y - 25.5f) / 3.2f;
                    if (a * a + b * b <= 1f) S(x, y, Pupil);
                }
                S(ex, 23, White); S(ex + 1, 23, White); S(ex, 24, White);
            }
            foreach (var p in new[] { (12, 31), (13, 31), (12, 32), (13, 32), (34, 31), (35, 31), (34, 32), (35, 32) }) S(p.Item1, p.Item2, Cheek);
            if (face == Face.Talk)
            {
                for (var y = 33; y < 37; y++)
                for (var x = 21; x < 27; x++)
                {
                    var a = (x - 23.5f) / 3f; var b = (y - 34.5f) / 2f;
                    if (a * a + b * b <= 1f) S(x, y, MouthIn);
                }
                S(23, 36, Tongue); S(24, 36, Tongue);
            }
            else foreach (var p in new[] { (21, 33), (22, 34), (23, 34), (24, 34), (25, 34), (26, 33) }) S(p.Item1, p.Item2, Rim);
            return px;
        }
    }
}
