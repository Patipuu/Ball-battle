using System.Collections.Generic;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Where an outline flash gets its pixels. BallView only asks for "the edge of this ball sprite" and tints it;
    /// swapping the source changes which pixels light up without touching the flash code.
    ///
    /// NOTE for the real-sprite pass (prompts 5/5b): set <see cref="BallEdges.Source"/> to
    /// <see cref="SpriteOutlineEdgeSource"/>. Then the flash recolors exactly the sprite's own 1 px dark outline pixels
    /// (Palette.Outline 20,18,28) — no extra ring is drawn outside the ball. The ball textures must be imported with
    /// Read/Write enabled (or the source falls back to the procedural ring and logs a warning once).
    /// </summary>
    public interface IBallEdgeSource
    {
        /// <summary>
        /// A white mask sprite, same size, pivot and pixels-per-unit as <paramref name="ball"/>, covering its outline.
        /// <paramref name="thickness"/> is a hint (1 = reflect edge, 2 = big-ball hit flash); a source drawn from the
        /// sprite itself may ignore it.
        /// </summary>
        Sprite Edge(Sprite ball, int thickness);
    }

    /// <summary>Current placeholder art: rings drawn in code on the 32x32 ball disc (1 or 2 px).</summary>
    public sealed class ProceduralEdgeSource : IBallEdgeSource
    {
        public Sprite Edge(Sprite ball, int thickness) => thickness >= 2 ? ProceduralSprites.BallOutline : ProceduralSprites.BallOutlineThin;
    }

    /// <summary>
    /// Real art: the mask is every pixel of the ball sprite that has the outline color (the 1 px dark rim), so the
    /// flash recolors those exact pixels. Cached per sprite. Falls back to <paramref name="fallback"/> when the texture
    /// cannot be read.
    /// </summary>
    public sealed class SpriteOutlineEdgeSource : IBallEdgeSource
    {
        readonly IBallEdgeSource fallback;
        readonly Color32 outline;
        readonly int tolerance;
        readonly Dictionary<Sprite, Sprite> cache = new Dictionary<Sprite, Sprite>();
        bool warned;

        public SpriteOutlineEdgeSource(IBallEdgeSource fallback = null, Color32? outlineColor = null, int tolerance = 6)
        {
            this.fallback = fallback ?? new ProceduralEdgeSource();
            outline = outlineColor ?? Palette.Outline;
            this.tolerance = tolerance;
        }

        public Sprite Edge(Sprite ball, int thickness)
        {
            if (ball == null) return fallback.Edge(null, thickness);
            if (cache.TryGetValue(ball, out var done) && done != null) return done;
            if (!ball.texture.isReadable)
            {
                if (!warned) Debug.LogWarning($"[BallBattle] {ball.texture.name} is not readable: outline flash uses the procedural ring");
                warned = true;
                return fallback.Edge(ball, thickness);
            }
            var r = ball.textureRect;
            var x0 = Mathf.RoundToInt(r.x);
            var y0 = Mathf.RoundToInt(r.y);
            var w = Mathf.RoundToInt(r.width);
            var h = Mathf.RoundToInt(r.height);
            var src = ball.texture.GetPixels32();
            var texW = ball.texture.width;
            var mask = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontSave };
            var px = new Color32[w * h];
            for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
            {
                var c = src[(y0 + y) * texW + x0 + x];
                if (IsOutline(c)) px[y * w + x] = new Color32(255, 255, 255, 255);
            }
            mask.SetPixels32(px);
            var pivot = new Vector2(ball.pivot.x / w, ball.pivot.y / h);
            var s = ProceduralSprites.Finish(mask, ball.name + "_edge", pivot);
            if (!Mathf.Approximately(ball.pixelsPerUnit, 1f))
                s = Sprite.Create(mask, new Rect(0, 0, w, h), pivot, ball.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            cache[ball] = s;
            return s;
        }

        bool IsOutline(Color32 c) =>
            c.a > 0 && Mathf.Abs(c.r - outline.r) <= tolerance && Mathf.Abs(c.g - outline.g) <= tolerance && Mathf.Abs(c.b - outline.b) <= tolerance;
    }

    /// <summary>The edge source every BallView uses.</summary>
    public static class BallEdges
    {
        public static IBallEdgeSource Source { get; set; } = Default();

        /// <summary>Real art (v1): flash the sprite's own 1 px dark rim; placeholder rings when a texture cannot be read.</summary>
        public static IBallEdgeSource Default() => new SpriteOutlineEdgeSource(new ProceduralEdgeSource());
    }
}
