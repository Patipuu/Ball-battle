using BallBattle.Sim;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Arena floor, walls and the outside mask. The mask (background color) sits above balls and blades,
    /// so blades overhanging the wall are clipped exactly at the arena edge. Walls blink before the shrink
    /// starts and turn warning-colored while it runs.
    /// </summary>
    public sealed class ArenaFrameView : MonoBehaviour
    {
        public const int FloorOrder = 0;
        public const int MaskOrder = 30;
        public const int WallOrder = 31;
        const int WallThickness = 2;
        const int WarnTicks = 3 * MatchConfig.TicksPerSecond;
        const float Far = 400f;

        SpriteRenderer floor;
        readonly SpriteRenderer[] masks = new SpriteRenderer[4];
        readonly SpriteRenderer[] walls = new SpriteRenderer[4];

        public static ArenaFrameView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("ArenaFrame");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ArenaFrameView>();
            v.floor = Quad(go.transform, "Floor", pixel, Palette.Floor, FloorOrder);
            for (var i = 0; i < 4; i++)
            {
                v.masks[i] = Quad(go.transform, $"Mask{i}", pixel, Palette.Background, MaskOrder);
                v.walls[i] = Quad(go.transform, $"Wall{i}", pixel, Palette.Wall, WallOrder);
            }
            return v;
        }

        static SpriteRenderer Quad(Transform parent, string name, Sprite pixel, Color32 color, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = pixel;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }

        /// <summary>Axis-aligned box by edges (pixel sprite has a centered pivot and is 1x1 unit).</summary>
        static void Box(SpriteRenderer sr, float left, float bottom, float right, float top)
        {
            sr.transform.localPosition = new Vector3((left + right) * 0.5f, (bottom + top) * 0.5f, 0f);
            sr.transform.localScale = new Vector3(right - left, top - bottom, 1f);
        }

        public void Render(ArenaRect arena, int activeTick, MatchConfig config)
        {
            // Snap edges to whole pixels so the frame never smears.
            var l = Mathf.Round(arena.Left);
            var r = Mathf.Round(arena.Right);
            var b = Mathf.Round(arena.Bottom);
            var t = Mathf.Round(arena.Top);

            Box(floor, l, b, r, t);
            Box(masks[0], -Far, t, Far, Far);    // above
            Box(masks[1], -Far, -Far, Far, b);   // below
            Box(masks[2], -Far, b, l, t);        // left
            Box(masks[3], r, b, Far, t);         // right

            var w = WallThickness;
            Box(walls[0], l - w, t, r + w, t + w);
            Box(walls[1], l - w, b - w, r + w, b);
            Box(walls[2], l - w, b, l, t);
            Box(walls[3], r, b, r + w, t);

            var color = Palette.Wall;
            var untilShrink = config.ShrinkStartTick - activeTick;
            if (untilShrink > 0 && untilShrink <= WarnTicks)
                color = (untilShrink / 8) % 2 == 0 ? Palette.WallWarning : Palette.Wall;   // blink ~4 Hz
            else if (untilShrink <= 0 && activeTick < config.ShrinkStartTick + config.ShrinkDurationTicks)
                color = Palette.WallWarning;
            foreach (var wall in walls) wall.color = color;
        }
    }
}
