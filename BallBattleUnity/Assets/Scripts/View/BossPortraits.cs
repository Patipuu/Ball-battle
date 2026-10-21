using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Native pixel portraits for the four new bosses; generated once, with no asset import dependency.</summary>
    public static class BossPortraits
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Get(string id)
        {
            if (id != "hunter" && id != "bell" && id != "ember_boss" && id != "prism") return null;
            if (Cache.TryGetValue(id, out var sprite)) return sprite;
            var texture = new Texture2D(64, 64, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                name = "boss_" + id
            };
            var pixels = new Color32[64 * 64];
            var edge = id == "hunter" ? C(70, 20, 30) : id == "bell" ? C(84, 54, 13) :
                id == "ember_boss" ? C(95, 24, 12) : C(22, 40, 90);
            var body = id == "hunter" ? C(212, 65, 65) : id == "bell" ? C(238, 180, 48) :
                id == "ember_boss" ? C(235, 76, 36) : C(67, 171, 234);
            var shine = id == "hunter" ? C(255, 151, 103) : id == "bell" ? C(255, 229, 127) :
                id == "ember_boss" ? C(255, 190, 59) : C(170, 245, 255);
            for (var y = 0; y < 64; y++)
            for (var x = 0; x < 64; x++)
            {
                var dx = x - 32;
                var dy = y - 30;
                var d = dx * dx + dy * dy;
                if (d <= 24 * 24) pixels[y * 64 + x] = d >= 21 * 21 ? edge : body;
                if (id == "hunter" && (Mathf.Abs(dx) + Mathf.Abs(dy) < 29) &&
                    (Mathf.Abs(dx - dy) < 3 || Mathf.Abs(dx + dy) < 3)) pixels[y * 64 + x] = shine;
                if (id == "bell" && d > 95 && d < 150 && dy > -12) pixels[y * 64 + x] = edge;
                if (id == "ember_boss" && dy > 2 && Mathf.Abs(dx) < (23 - dy / 2) &&
                    (Mathf.Abs(dx * 3 + dy) < 8 || Mathf.Abs(dx * 3 - dy) < 8)) pixels[y * 64 + x] = shine;
                if (id == "prism" && Mathf.Abs(dx) + Mathf.Abs(dy) < 28)
                    pixels[y * 64 + x] = Mathf.Abs(dx) < 3 || Mathf.Abs(dy) < 3 ? shine : body;
            }
            // Eyes and central emblems provide a distinct read at 1x.
            if (id == "hunter")
            {
                Rect(pixels, 21, 31, 29, 34, edge); Rect(pixels, 35, 31, 43, 34, edge);
                Rect(pixels, 24, 31, 27, 32, shine); Rect(pixels, 37, 31, 40, 32, shine);
            }
            else if (id == "bell")
            {
                Rect(pixels, 28, 19, 36, 22, edge); Rect(pixels, 30, 35, 34, 42, shine);
            }
            else if (id == "ember_boss")
            {
                Rect(pixels, 25, 26, 29, 29, edge); Rect(pixels, 35, 26, 39, 29, edge);
            }
            else
            {
                Rect(pixels, 29, 26, 35, 34, shine);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            sprite = Sprite.Create(texture, new Rect(0, 0, 64, 64), new Vector2(0.5f, 30f / 64f), 1f, 0,
                SpriteMeshType.FullRect);
            sprite.name = texture.name;
            Cache.Add(id, sprite);
            return sprite;
        }

        static Color32 C(byte r, byte g, byte b) => new Color32(r, g, b, 255);

        static void Rect(Color32[] pixels, int l, int b, int r, int t, Color32 color)
        {
            for (var y = b; y <= t; y++)
            for (var x = l; x <= r; x++) pixels[y * 64 + x] = color;
        }
    }
}
