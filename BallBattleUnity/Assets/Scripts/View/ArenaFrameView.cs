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

        public const int ObstacleOrder = 1;
        static readonly Color32 PostColor = new Color32(120, 130, 150, 255);
        static readonly Color32 BumperColor = new Color32(240, 170, 60, 255);
        static readonly Color32 SpikeColor = new Color32(210, 60, 60, 255);

        Sprite pixelSprite;
        ArenaLayout drawnLayout;
        Transform obstacleRoot;
        SpriteRenderer floor;
        readonly SpriteRenderer[] masks = new SpriteRenderer[4];
        readonly SpriteRenderer[] walls = new SpriteRenderer[4];

        public static ArenaFrameView Create(Transform parent, Sprite pixel)
        {
            var go = new GameObject("ArenaFrame");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ArenaFrameView>();
            v.pixelSprite = pixel;
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

            DrawObstacles(config.Layout ?? ArenaLayout.Empty);

            var baseColor = (config.Layout != null && config.Layout.WallDamage > 0f) ? SpikeColor : (Color32)Palette.Wall;
            var color = baseColor;
            var untilShrink = config.ShrinkStartTick - activeTick;
            if (untilShrink > 0 && untilShrink <= WarnTicks)
                color = (untilShrink / 8) % 2 == 0 ? Palette.WallWarning : baseColor;   // blink ~4 Hz
            else if (untilShrink <= 0 && activeTick < config.ShrinkStartTick + config.ShrinkDurationTicks)
                color = Palette.WallWarning;
            foreach (var wall in walls) wall.color = color;
        }

        /// <summary>Static obstacles as pixel rows (circles) under the balls; rebuilt only when the layout changes.</summary>
        void DrawObstacles(ArenaLayout layout)
        {
            if (ReferenceEquals(layout, drawnLayout)) return;
            drawnLayout = layout;
            if (obstacleRoot != null) Destroy(obstacleRoot.gameObject);
            var root = new GameObject("Obstacles");
            root.transform.SetParent(transform, false);
            obstacleRoot = root.transform;
            for (var i = 0; i < layout.ObstacleCount; i++)
            {
                var o = layout.Get(i);
                var color = o.Damage > 0f ? SpikeColor : (o.Boost > 0f ? BumperColor : PostColor);
                var steps = o.Shape == ObstacleShape.Circle ? 1 : Mathf.Max(1, Mathf.CeilToInt((o.B - o.A).Length));
                for (var s = 0; s < steps; s++)
                {
                    var c = o.Shape == ObstacleShape.Circle ? o.A : o.A + (o.B - o.A) * (s / (float)steps);
                    var r = Mathf.RoundToInt(o.Radius);
                    for (var dy = -r; dy < r; dy++)
                    {
                        var half = Mathf.Round(Mathf.Sqrt(r * r - (dy + 0.5f) * (dy + 0.5f)));
                        var row = Quad(obstacleRoot, "Row", pixelSprite, color, ObstacleOrder);
                        var y = Mathf.Round(c.Y) + dy;
                        Box(row, Mathf.Round(c.X) - half, y, Mathf.Round(c.X) + half, y + 1);
                    }
                }
            }
        }
    }
}
