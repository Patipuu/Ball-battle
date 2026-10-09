using System;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Small builders shared by the Run screens (full-screen pixel panels drawn over the arena).</summary>
    static class RunUi
    {
        public const int Order = 60;
        public static readonly Color32 Panel = new Color32(24, 24, 38, 255);

        public static Transform Root(Transform parent, string name, ArtLibrary art, bool background = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            if (background) PixelRect.Set(PixelRect.Create(go.transform, "Back", art.Pixel, Palette.Background, Order), -200, -300, 200, 300);
            return go.transform;
        }

        public static PixelText Text(Transform parent, ArtLibrary art, float x, float y, int scale, PixelText.Align align = PixelText.Align.Center, string initial = null, Color32? color = null)
        {
            var t = PixelText.Create(parent, "Text", art.Font, new Vector2(x, y), align, scale, Order + 4);
            if (initial != null) t.Set(initial, color ?? Palette.Text);
            return t;
        }

        public static PixelButton Button(Transform parent, ArtLibrary art, Rect r, string label, int scale, Color32 accent, Action onClick)
        {
            var b = PixelButton.Create(parent, label ?? "Button", art, r, label, scale, Palette.Floor, accent, accent, Order + 2);
            if (onClick != null) b.Clicked += onClick;
            return b;
        }

        /// <summary>Framed panel: border at Order+1, fill at Order+2 (icons go at Order+3, text at Order+4).</summary>
        public static void Box(Transform parent, ArtLibrary art, float left, float bottom, float right, float top)
        {
            PixelRect.Set(PixelRect.Create(parent, "BoxBorder", art.Pixel, Palette.Wall, Order + 1), left, bottom, right, top);
            PixelRect.Set(PixelRect.Create(parent, "BoxFill", art.Pixel, Panel, Order + 2), left + 1, bottom + 1, right - 1, top - 1);
        }

        public static SpriteRenderer Icon(Transform parent, Vector2 pos, int order)
        {
            var go = new GameObject("Icon");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        public static string WeaponName(string id) => WeaponRegistry.Get(id).DisplayName;
    }
}
