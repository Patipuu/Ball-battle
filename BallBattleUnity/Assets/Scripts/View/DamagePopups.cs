using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Floating damage numbers: rise ~14 px in whole-pixel steps, then fade. Two pooled sizes (big hits use 2x).
    /// Number strings are cached, so a repeated value never allocates again.
    /// </summary>
    public sealed class DamagePopups : MonoBehaviour
    {
        public const int Order = 45;
        const float Duration = 0.7f;
        const float Rise = 14f;
        const float BigHit = 10f;

        sealed class Popup
        {
            public PixelText Text;
            public Vector2 Start;
            public float Age = Duration;
            public Color32 Color;
            public string Value;
        }

        readonly List<Popup> small = new List<Popup>();
        readonly List<Popup> big = new List<Popup>();
        readonly Dictionary<int, string> numberCache = new Dictionary<int, string>();
        int nextSmall, nextBig;

        public static DamagePopups Create(Transform parent, Texture2D font, int perSize)
        {
            var go = new GameObject("DamagePopups");
            go.transform.SetParent(parent, false);
            var d = go.AddComponent<DamagePopups>();
            for (var i = 0; i < perSize; i++)
            {
                d.small.Add(new Popup { Text = PixelText.Create(go.transform, "s", font, Vector2.zero, PixelText.Align.Center, 1, Order) });
                d.big.Add(new Popup { Text = PixelText.Create(go.transform, "b", font, Vector2.zero, PixelText.Align.Center, 2, Order) });
            }
            foreach (var p in d.small) p.Text.Set("", default);
            foreach (var p in d.big) p.Text.Set("", default);
            return d;
        }

        /// <summary>Formats like 7, 1.4, 12 (one decimal only when it matters). Cached by value×10.</summary>
        string Format(float damage)
        {
            var key = Mathf.RoundToInt(damage * 10f);
            if (numberCache.TryGetValue(key, out var s)) return s;
            s = key % 10 == 0 ? (key / 10).ToString() : (key / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
            numberCache[key] = s;
            return s;
        }

        public void Show(Vector2 at, float damage, Color32 color)
        {
            Popup p;
            if (damage >= BigHit) { p = big[nextBig]; nextBig = (nextBig + 1) % big.Count; }
            else { p = small[nextSmall]; nextSmall = (nextSmall + 1) % small.Count; }
            p.Start = new Vector2(Mathf.Round(at.x), Mathf.Round(at.y));
            p.Age = 0f;
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

        public void Tick(float dt)
        {
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
