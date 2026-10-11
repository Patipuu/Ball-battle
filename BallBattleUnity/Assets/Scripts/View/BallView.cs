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
        /// <summary>
        /// Blade sprites are 7 px tall: 1 px top outline + 5 px core + 1 px bottom outline. The sim's BladeThickness is drawn
        /// rounded to whole pixels and never thinner than this, so the 9-slice only ever stretches the core, never the outline.
        /// </summary>
        public const int MinBladePixels = 7;
        /// <summary>Blade sprite pivot height as a fraction of the sprite (3/7: the 7 px art sits on whole pixels, y-3 .. y+4).</summary>
        public const float BladePivotY = 3f / 7f;

        SpriteRenderer body;
        SpriteRenderer blade;
        SpriteRenderer flash;
        SpriteRenderer outlineFlash;
        float flashRemaining;
        // Reflect-damage flash: only the outer 1 px ring, the body keeps its weapon color.
        SpriteRenderer edgeFlash;
        float edgeRemaining;
        // Player marker (Run mode): dotted ring that scales with the ball, arrow kept at native pixel size.
        SpriteRenderer markerRing, markerArrow;
        Transform markerUnscaled;
        Transform pivot;
        Transform twinPivot;
        SpriteRenderer twin;
        int bladeOrder;
        string weaponId;
        float shownHeat = -1f;
        float shownScale = 1f;
        bool scaleDirty;

        public static BallView Create(Transform parent, int index, string weaponId, ArtLibrary.WeaponArt art, Sprite flashSprite, int bodyOrder, int bladeOrder)
        {
            var go = new GameObject($"Ball{index}_{weaponId}");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallView>();
            v.weaponId = weaponId;
            v.bladeOrder = bladeOrder;

            v.body = go.AddComponent<SpriteRenderer>();
            v.body.sprite = art.Ball;
            v.body.sortingOrder = bodyOrder;

            var f = new GameObject("Flash");
            f.transform.SetParent(go.transform, false);
            v.flash = f.AddComponent<SpriteRenderer>();
            v.flash.sprite = flashSprite;
            v.flash.sortingOrder = bodyOrder + 1;   // always over its own body, under the other ball
            v.flash.enabled = false;

            var o = new GameObject("OutlineFlash");
            o.transform.SetParent(go.transform, false);
            v.outlineFlash = o.AddComponent<SpriteRenderer>();
            v.outlineFlash.sprite = BallEdges.Source.Edge(art.Ball, 2);
            v.outlineFlash.sortingOrder = bodyOrder + 1;
            v.outlineFlash.enabled = false;

            var e = new GameObject("EdgeFlash");
            e.transform.SetParent(go.transform, false);
            v.edgeFlash = e.AddComponent<SpriteRenderer>();
            v.edgeFlash.sprite = BallEdges.Source.Edge(art.Ball, 1);
            v.edgeFlash.sortingOrder = bodyOrder + 1;
            v.edgeFlash.enabled = false;

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

        /// <summary>
        /// Size relative to the default ball (giant boss 1.5, Heavy 1.2). Scales the body and flash; the blade pivot
        /// is scaled back so blade offsets stay in native pixels. Placeholder until real big-ball sprites (Phase 6).
        /// </summary>
        public void SetScale(float scale)
        {
            if (!scaleDirty && Mathf.Approximately(scale, shownScale)) return;
            scaleDirty = false;
            shownScale = scale;
            transform.localScale = new Vector3(scale, scale, 1f);
            if (pivot != null) pivot.localScale = new Vector3(1f / scale, 1f / scale, 1f);
            if (twinPivot != null) twinPivot.localScale = new Vector3(1f / scale, 1f / scale, 1f);
            PlaceMarker();
        }

        /// <summary>Current size relative to the default ball.</summary>
        public float Scale => shownScale;

        /// <summary>Radius in native pixels of the ball sprite at scale 1 (the size the sim radius is mapped onto).</summary>
        public float NativeRadius => bodyRadiusOverride > 0f ? bodyRadiusOverride
            : body != null && body.sprite != null ? body.sprite.rect.width / body.sprite.pixelsPerUnit * 0.5f : ProceduralSprites.BallSize * 0.5f;

        float bodyRadiusOverride;
        SpriteRenderer glow;
        float glowRemaining, glowDuration;

        /// <summary>
        /// Draw this ball with another body sprite (boss art: 64x64, body radius 24 px around the pivot). The sim radius maps
        /// onto <paramref name="bodyRadiusPixels"/>, so a 24 px boss drawn with a 24 px body is NOT scaled up. The outline
        /// flashes follow the new sprite's own rim.
        /// </summary>
        public void SetBodySprite(Sprite sprite, float bodyRadiusPixels)
        {
            if (sprite == null) return;
            body.sprite = sprite;
            bodyRadiusOverride = bodyRadiusPixels;
            if (outlineFlash != null) outlineFlash.sprite = BallEdges.Source.Edge(sprite, 2);
            if (edgeFlash != null) edgeFlash.sprite = BallEdges.Source.Edge(sprite, 1);
            scaleDirty = true;   // the next SetScale re-applies even if the value is the same (Scale stays valid meanwhile)
        }

        /// <summary>Body sprite now (tests).</summary>
        public Sprite BodySprite => body.sprite;

        /// <summary>Show <paramref name="glowSprite"/> over the body, fading out over <paramref name="seconds"/> (Shell King glow).</summary>
        public void PlayGlow(Sprite glowSprite, float seconds)
        {
            if (glowSprite == null || seconds <= 0f) return;
            if (glow == null)
            {
                var g = new GameObject("Glow");
                g.transform.SetParent(transform, false);
                glow = g.AddComponent<SpriteRenderer>();
                glow.sortingOrder = body.sortingOrder + 1;
            }
            glow.sprite = glowSprite;
            glowDuration = seconds;
            glowRemaining = seconds;
            glow.enabled = true;
            glow.color = Color.white;
        }

        public bool IsGlowing => glow != null && glow.enabled;

        void TickGlow(float dt)
        {
            if (glow == null || !glow.enabled) return;
            glowRemaining -= dt;
            if (glowRemaining <= 0f) { glow.enabled = false; return; }
            glow.color = new Color(1f, 1f, 1f, glowRemaining / glowDuration);
        }

        /// <summary>From this size up the hit flash only lights the outline: a flashing giant body is a wall of white.</summary>
        public const float OutlineFlashScale = 1.3f;

        /// <summary>True when hit flashes draw only the outline (giant / very big balls).</summary>
        /// <summary>Big on screen (radius at least 1.3x a default 16 px ball): Heavy LV2, bosses, whatever their sprite.</summary>
        public bool UsesOutlineFlash => NativeRadius * shownScale >= OutlineFlashScale * ProceduralSprites.BallSize * 0.5f - 0.001f;

        /// <summary>True while the hit flash is showing.</summary>
        public bool IsFlashing => flashRemaining > 0f;

        /// <summary>The renderer the current flash uses (outline for big balls, full disc otherwise).</summary>
        public SpriteRenderer ActiveFlashRenderer => UsesOutlineFlash ? outlineFlash : flash;

        /// <summary>Order of the white arrow over the player's ball: above walls (31), under the HUD (40).</summary>
        public const int MarkerArrowOrder = 32;

        public bool IsMarked => markerUnscaled != null && markerUnscaled.gameObject.activeSelf;

        /// <summary>
        /// Player marker (Run mode only), per docs/design/art/fix_marker_flash_v1.png: a white down arrow with a dark
        /// 1 px outline whose tip is 5 pixel rows above the ball's top row, centred, following the ball; and a white
        /// dashed ring (1 px, 6° on / 6° off, never rotating) on the 3rd pixel out from the ball edge. Both are drawn
        /// at native pixel size whatever the ball scale, so two same-weapon balls can be told apart.
        /// </summary>
        public void SetMarked(bool marked)
        {
            if (!marked)
            {
                if (markerUnscaled != null) markerUnscaled.gameObject.SetActive(false);
                return;
            }
            if (markerUnscaled == null)
            {
                markerUnscaled = new GameObject("Marker").transform;
                markerUnscaled.SetParent(transform, false);

                var r = new GameObject("MarkerRing");
                r.transform.SetParent(markerUnscaled, false);
                markerRing = r.AddComponent<SpriteRenderer>();
                markerRing.sortingOrder = body.sortingOrder - 1;   // never over its own body
                markerRing.color = Color.white;

                var a = new GameObject("MarkerArrow");
                a.transform.SetParent(markerUnscaled, false);
                markerArrow = a.AddComponent<SpriteRenderer>();
                markerArrow.sprite = ProceduralSprites.DownArrow;
                markerArrow.sortingOrder = MarkerArrowOrder;
                markerArrow.color = Color.white;   // the sprite carries its own white and outline colors
            }
            markerUnscaled.gameObject.SetActive(true);
            PlaceMarker();
        }

        /// <summary>Top edge of the ball's topmost drawn pixel row above its centre, whole native px (measured on the sprite).</summary>
        public int VisibleTopPixels => Mathf.Max(1, Mathf.RoundToInt((NativeRadius - DiscOf(body != null ? body.sprite : null).topInset) * shownScale));

        /// <summary>Radius of the drawn disc on screen (native px), measured on the sprite.</summary>
        public float VisibleRadius => DiscOf(body != null ? body.sprite : null).radius * shownScale;

        static readonly System.Collections.Generic.Dictionary<Sprite, (float topInset, float radius)> discs = new System.Collections.Generic.Dictionary<Sprite, (float, float)>();

        /// <summary>
        /// The ball disc as drawn in its sprite: transparent rows above it and its half-width, from the alpha channel when
        /// the texture is readable (real art: 30 px disc in a 32 px cell), else the placeholder's known disc.
        /// </summary>
        public static (float topInset, float radius) DiscOf(Sprite sprite)
        {
            var fallback = (ProceduralSprites.BallTopInset, ProceduralSprites.BallDiscRadius);
            if (sprite == null || !sprite.texture.isReadable) return fallback;
            if (discs.TryGetValue(sprite, out var known)) return known;
            var r = sprite.textureRect;
            int x0 = (int)r.x, y0 = (int)r.y, w = (int)r.width, h = (int)r.height;
            var px = sprite.texture.GetPixels32();
            var texW = sprite.texture.width;
            int top = -1, left = w, right = -1;
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                if (px[(y0 + y) * texW + x0 + x].a == 0) continue;
                if (y > top) top = y;
                if (x < left) left = x;
                if (x > right) right = x;
            }
            var disc = top < 0 ? fallback : ((float)(h - 1 - top), (right - left + 1) * 0.5f);
            discs[sprite] = disc;
            return disc;
        }

        void PlaceMarker()
        {
            if (markerUnscaled == null) return;
            markerUnscaled.localScale = new Vector3(1f / shownScale, 1f / shownScale, 1f);
            markerRing.sprite = ProceduralSprites.DashedRing(VisibleRadius);
            // The ball's top row is [top-1, top); the tip white row is 5 rows higher, and the sprite's pivot is the outline row under it.
            var top = VisibleTopPixels;
            markerArrow.transform.localPosition = new Vector3(0f, top - 1 + 5 - ProceduralSprites.ArrowRowsBelowTip, 0f);
        }

        /// <summary>World position of the arrow sprite's pivot: bottom of its outline row, left edge of the tip column (tests).</summary>
        public Vector3 MarkerArrowPosition => markerArrow != null ? markerArrow.transform.position : transform.position;

        /// <summary>Marker renderers (tests).</summary>
        public SpriteRenderer MarkerArrowRenderer => markerArrow;
        public SpriteRenderer MarkerRingRenderer => markerRing;

        /// <summary>Show the white hit flash for a short time (driven by Render's frame time).</summary>
        public void Flash(float seconds) => Flash(seconds, Color.white);

        /// <summary>Same flash in another color (magenta for reflect damage). The latest call sets the color.</summary>
        public void Flash(float seconds, Color color)
        {
            flashRemaining = Mathf.Max(flashRemaining, seconds);
            if (flash != null) flash.color = color;
            if (outlineFlash != null) outlineFlash.color = color;
            ApplyFlash();
        }

        /// <summary>
        /// Flash only the outer 1 px of the ball in <paramref name="color"/> (reflect damage). Independent of the
        /// hit flash; while it shows, the full-disc flash is suppressed so the body keeps its weapon color.
        /// </summary>
        public void FlashEdge(float seconds, Color color)
        {
            edgeRemaining = Mathf.Max(edgeRemaining, seconds);
            if (edgeFlash != null) edgeFlash.color = color;
            ApplyFlash();
        }

        /// <summary>True while the 1 px reflect edge flash shows.</summary>
        public bool IsEdgeFlashing => edgeRemaining > 0f;

        /// <summary>The 1 px edge renderer (tests).</summary>
        public SpriteRenderer EdgeFlashRenderer => edgeFlash;

        /// <summary>The full-disc flash renderer (tests).</summary>
        public SpriteRenderer FullFlashRenderer => flash;

        void ApplyFlash()
        {
            var edgeOn = edgeRemaining > 0f;
            if (edgeFlash != null && edgeFlash.enabled != edgeOn) edgeFlash.enabled = edgeOn;
            var flashing = flashRemaining > 0f;
            var outlineOnly = UsesOutlineFlash;
            var full = flashing && !outlineOnly && !edgeOn;
            var edge = flashing && outlineOnly;
            if (flash.enabled != full) flash.enabled = full;
            if (outlineFlash != null && outlineFlash.enabled != edge) outlineFlash.enabled = edge;
        }

        /// <summary>Called every frame with interpolated sim values (world units = native pixels).</summary>
        public void Render(Vector2 pos, float angleDeg, float bladeInner, float bladeLength, float statValue, bool alive, float deltaTime, float twinLength = 0f, float bladeThickness = 0f)
        {
            if (gameObject.activeSelf != alive) gameObject.SetActive(alive);
            if (!alive) return;

            flashRemaining = Mathf.Max(0f, flashRemaining - deltaTime);
            edgeRemaining = Mathf.Max(0f, edgeRemaining - deltaTime);
            ApplyFlash();
            TickGlow(deltaTime);

            transform.localPosition = new Vector3(Mathf.Round(pos.x), Mathf.Round(pos.y), 0f);
            if (blade == null) return;

            pivot.localRotation = Quaternion.Euler(0f, 0f, angleDeg);
            var height = BladePixels(bladeThickness);
            blade.transform.localPosition = new Vector3(Mathf.Round(bladeInner), PivotOffset(height), 0f);
            var minLength = blade.sprite.border.x + blade.sprite.border.z;
            blade.size = new Vector2(Mathf.Max(minLength, Mathf.Round(bladeLength)), height);

            RenderTwin(angleDeg, bladeInner, twinLength, height);

            var heat = Palette.Heat(weaponId, statValue);
            if (!Mathf.Approximately(heat, shownHeat))
            {
                shownHeat = heat;
                blade.color = Color.Lerp(Color.white, Palette.Hot, heat * 0.85f);
                if (twin != null) twin.color = blade.color;
            }
        }

        /// <summary>Second blade opposite the first (Twin Blade). Created the first time it is needed.</summary>
        void RenderTwin(float angleDeg, float bladeInner, float twinLength, int height)
        {
            if (twinLength <= 0f)
            {
                if (twinPivot != null && twinPivot.gameObject.activeSelf) twinPivot.gameObject.SetActive(false);
                return;
            }
            if (twinPivot == null)
            {
                twinPivot = new GameObject("TwinPivot").transform;
                twinPivot.SetParent(transform, false);
                twinPivot.localScale = pivot.localScale;
                var t = new GameObject("TwinBlade");
                t.transform.SetParent(twinPivot, false);
                twin = t.AddComponent<SpriteRenderer>();
                twin.sprite = blade.sprite;
                twin.drawMode = SpriteDrawMode.Sliced;
                twin.sortingOrder = bladeOrder;
                twin.color = blade.color;
            }
            if (!twinPivot.gameObject.activeSelf) twinPivot.gameObject.SetActive(true);
            twinPivot.localRotation = Quaternion.Euler(0f, 0f, angleDeg + 180f);
            twin.transform.localPosition = new Vector3(Mathf.Round(bladeInner), PivotOffset(height), 0f);
            var minLength = twin.sprite.border.x + twin.sprite.border.z;
            twin.size = new Vector2(Mathf.Max(minLength, Mathf.Round(twinLength)), height);
        }

        /// <summary>Sim blade thickness to whole pixels, never thinner than MinBladePixels.</summary>
        public static int BladePixels(float simThickness) => Mathf.Max(MinBladePixels, Mathf.RoundToInt(simThickness));

        /// <summary>
        /// Shift along the blade's normal so its bottom edge sits on a whole pixel (floor(h/2) below the axis) whatever
        /// the height: the sliced sprite keeps its pivot at BladePivotY of its size. 0 for the 7 px default.
        /// </summary>
        public static float PivotOffset(int height) => BladePivotY * height - height / 2;
    }
}
