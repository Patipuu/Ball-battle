using UnityEngine;

namespace BallBattle.View
{
    /// <summary>Solid rectangles built from the 1x1 pixel sprite, positioned by whole-pixel edges.</summary>
    public static class PixelRect
    {
        public static SpriteRenderer Create(Transform parent, string name, Sprite pixel, Color32 color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixel;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Place by edges (pixel sprite pivot is centred, 1 unit = 1 px). Edges are rounded to whole pixels.</summary>
        public static void Set(SpriteRenderer sr, float left, float bottom, float right, float top)
        {
            left = Mathf.Round(left); right = Mathf.Round(right);
            bottom = Mathf.Round(bottom); top = Mathf.Round(top);
            sr.transform.localPosition = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
            sr.transform.localScale = new Vector3(Mathf.Max(0f, right - left), Mathf.Max(0f, top - bottom), 1f);
        }

        public static void Set(SpriteRenderer sr, Rect r) => Set(sr, r.xMin, r.yMin, r.xMax, r.yMax);
    }
}
