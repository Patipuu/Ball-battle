using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// One ball: body sprite plus a 9-sliced blade on a rotating pivot. Bladeless weapons show the body only.
    /// Body position and blade length are snapped to whole pixels (blade sprite pivot y = 2/5 keeps its edges
    /// on pixel boundaries), so sprites never crawl by a texel between frames; rotation stays continuous.
    /// </summary>
    public sealed class BallView : MonoBehaviour
    {
        public const int BladeHeight = 5;

        SpriteRenderer body;
        SpriteRenderer blade;
        SpriteRenderer flash;
        float flashRemaining;
        Transform pivot;
        string weaponId;
        float shownHeat = -1f;

        public static BallView Create(Transform parent, int index, string weaponId, ArtLibrary.WeaponArt art, Sprite flashSprite, int bodyOrder, int bladeOrder)
        {
            var go = new GameObject($"Ball{index}_{weaponId}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallView>();
            v.weaponId = weaponId;

            v.body = go.AddComponent<SpriteRenderer>();
            v.body.sprite = art.Ball;
            v.body.sortingOrder = bodyOrder;

            var f = new GameObject("Flash");
            f.transform.SetParent(go.transform, false);
            v.flash = f.AddComponent<SpriteRenderer>();
            v.flash.sprite = flashSprite;
            v.flash.sortingOrder = bodyOrder + 1;   // always over its own body, under the other ball
            v.flash.enabled = false;

            if (art.Blade != null)
            {
                v.pivot = new GameObject("BladePivot").transform;
                v.pivot.SetParent(go.transform, false);
                var b = new GameObject("Blade");
                b.transform.SetParent(v.pivot, false);
                v.blade = b.AddComponent<SpriteRenderer>();
                v.blade.sprite = art.Blade;
                v.blade.drawMode = SpriteDrawMode.Sliced;
                v.blade.sortingOrder = bladeOrder;
            }
            return v;
        }

        /// <summary>Show the white hit flash for a short time (driven by Render's frame time).</summary>
        public void Flash(float seconds) => flashRemaining = Mathf.Max(flashRemaining, seconds);

        /// <summary>Called every frame with interpolated sim values (world units = native pixels).</summary>
        public void Render(Vector2 pos, float angleDeg, float bladeInner, float bladeLength, float statValue, bool alive, float deltaTime)
        {
            if (gameObject.activeSelf != alive) gameObject.SetActive(alive);
            if (!alive) return;

            flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
            var flashing = flashRemaining > 0f;
            if (flash.enabled != flashing) flash.enabled = flashing;

            transform.localPosition = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
            if (blade == null) return;

            pivot.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            blade.transform.localPosition = new Vector3(Mathf.Round(bladeInner), 0f, 0f);
            var minLength = blade.sprite.border.x + blade.sprite.border.z;
            blade.size = new Vector2(Mathf.Max(minLength, Mathf.Round(bladeLength)), BladeHeight);

            var heat = Palette.Heat(weaponId, statValue);
            if (!Mathf.Approximately(heat, shownHeat))
            {
                shownHeat = heat;
                blade.color = Color.Lerp(Color.white, Palette.Hot, heat * 0.85f);
            }
        }
    }
}
