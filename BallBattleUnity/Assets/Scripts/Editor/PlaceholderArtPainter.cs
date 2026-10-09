using BallBattle.View;
using UnityEngine;

namespace BallBattle.EditorTools
{
    /// <summary>Draws the placeholder textures pixel by pixel. Pure functions returning new Texture2D (caller destroys).</summary>
    public static class PlaceholderArtPainter
    {
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);

        static Texture2D New(int w, int h)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (var i = 0; i < px.Length; i++) px[i] = Clear;
            t.SetPixels32(px);
            return t;
        }

        static Color32 Scale(Color32 c, float k) =>
            new Color32((byte)Mathf.Clamp(c.r * k, 0, 255), (byte)Mathf.Clamp(c.g * k, 0, 255), (byte)Mathf.Clamp(c.b * k, 0, 255), c.a);

        public static Texture2D Pixel()
        {
            var t = New(1, 1);
            t.SetPixel(0, 0, Color.white);
            t.Apply();
            return t;
        }

        public static Texture2D Font()
        {
            var t = New(PixelFontData.AtlasWidth, PixelFontData.GlyphHeight);
            for (var g = 0; g < PixelFontData.Glyphs.Length; g++)
                for (var row = 0; row < PixelFontData.GlyphHeight; row++)
                    for (var x = 0; x < PixelFontData.GlyphWidth; x++)
                        if (PixelFontData.Ink(g, x, row))
                            t.SetPixel(g * PixelFontData.CellWidth + x, PixelFontData.GlyphHeight - 1 - row, Color.white);
            t.Apply();
            return t;
        }

        /// <summary>32x32 solid white disc (same shape as balls) for the hit flash.</summary>
        public static Texture2D BallFlash()
        {
            var t = New(32, 32);
            for (var y = 0; y < 32; y++)
            for (var x = 0; x < 32; x++)
            {
                var dx = x + 0.5f - 16f;
                var dy = y + 0.5f - 16f;
                if (dx * dx + dy * dy <= 15.5f * 15.5f) t.SetPixel(x, y, Color.white);
            }
            t.Apply();
            return t;
        }

        /// <summary>32x32 ball: body color, 1px dark outline, light upper-left, shade lower-right, small highlight.</summary>
        public static Texture2D Ball(Palette.WeaponLook look)
        {
            const int size = 32;
            const float r = 15.5f;
            var t = New(size, size);
            var light = Scale(look.Body, 1.25f);
            var shade = Scale(look.Body, 0.7f);
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5f - 16f;
                var dy = y + 0.5f - 16f;
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > r) continue;
                Color32 c;
                if (d > r - 1f) c = Palette.Outline;
                else
                {
                    var lit = (-dx + dy) / r; // + toward upper-left
                    c = lit > 0.55f ? light : (lit < -0.45f ? shade : look.Body);
                }
                t.SetPixel(x, y, c);
            }
            // Highlight 3x2 near upper-left.
            for (var x = 9; x < 12; x++) for (var y = 21; y < 23; y++) t.SetPixel(x, y, new Color32(255, 255, 255, 220));
            if (look.Id == "brawler")
            {
                // Knuckle marks so the bladeless fighter reads as "fists".
                for (var i = 0; i < 4; i++) { t.SetPixel(11 + i * 3, 12, Palette.Outline); t.SetPixel(11 + i * 3, 13, Palette.Outline); }
            }
            t.Apply();
            return t;
        }

        /// <summary>
        /// Horizontal blade, pivot at the left (inner) end, height 5 (3px core + outline). 9-slice border keeps the
        /// handle and tip crisp while the middle stretches to the sim's BladeLength. Null for bladeless weapons.
        /// </summary>
        public static Texture2D Blade(string id, out Vector4 border)
        {
            switch (id)
            {
                case "blade": return Sword(24, out border);
                case "fang": return Sword(16, out border);
                case "pike": return Pike(26, out border);
                // Placeholder blades for the Step 2 weapons (real art in Phase 6).
                case "volley": return Sword(14, out border);
                case "venom": return Sword(22, out border);
                case "aegis": return Sword(22, out border);
                case "rig": return Sword(18, out border);
                default: border = Vector4.zero; return null;
            }
        }

        static Texture2D Sword(int w, out Vector4 border)
        {
            var t = New(w, 5);
            for (var x = 0; x < 4; x++) { t.SetPixel(x, 1, Palette.WoodDark); t.SetPixel(x, 2, Palette.Wood); t.SetPixel(x, 3, Palette.WoodDark); }
            for (var y = 0; y < 5; y++) t.SetPixel(4, y, Palette.Outline);           // guard
            for (var x = 5; x < w - 2; x++)
            {
                t.SetPixel(x, 1, Palette.MetalDark);
                t.SetPixel(x, 2, Palette.Metal);
                t.SetPixel(x, 3, Color.white);
            }
            t.SetPixel(w - 2, 2, Palette.Metal); t.SetPixel(w - 2, 3, Color.white);  // taper
            t.SetPixel(w - 1, 2, Color.white);                                         // point
            t.Apply();
            border = new Vector4(5, 0, 2, 0);
            return t;
        }

        static Texture2D Pike(int w, out Vector4 border)
        {
            var t = New(w, 5);
            for (var x = 0; x < w - 6; x++) { t.SetPixel(x, 1, Palette.WoodDark); t.SetPixel(x, 2, Palette.Wood); t.SetPixel(x, 3, Palette.WoodDark); }
            var tip = w - 6;
            for (var x = 0; x < 6; x++)
            {
                var half = x < 3 ? 2 : (x < 5 ? 1 : 0);   // leaf-shaped head
                for (var y = 2 - half; y <= 2 + half; y++) t.SetPixel(tip + x, y, y > 2 ? Color.white : (Color)Palette.Metal);
            }
            t.Apply();
            border = new Vector4(2, 0, 6, 0);
            return t;
        }
    }
}
