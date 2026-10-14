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

        /// <summary>Spike teeth (art v1) every this many px along each wall of a spike arena.</summary>
        public const int ToothSpacing = 8;
        public const int ToothOrder = ObstacleOrder + 1;

        Sprite pixelSprite;
        ArtLibrary art;
        string tiledArena;
        Color32 shownWallColor;
        readonly System.Collections.Generic.List<SpriteRenderer> teeth = new System.Collections.Generic.List<SpriteRenderer>();
        int activeTeeth;
        ArenaLayout drawnLayout;
        Transform obstacleRoot;
        /// <summary>One node per obstacle (its pixel rows), so a Warden step can hide a disabled one (BB-04).</summary>
        readonly System.Collections.Generic.List<GameObject> obstacleNodes = new System.Collections.Generic.List<GameObject>();
        SpriteRenderer floor;
        readonly SpriteRenderer[] masks = new SpriteRenderer[4];
        readonly SpriteRenderer[] walls = new SpriteRenderer[4];
        /// <summary>Red wall strips over the spike zones, used only when there is no spike_tooth art.</summary>
        readonly System.Collections.Generic.List<SpriteRenderer> zoneStrips = new System.Collections.Generic.List<SpriteRenderer>();
        int activeStrips;

        /// <summary>One spike zone along one wall: [From, To] along it, wall line At, tooth angle, horizontal wall?</summary>
        public struct ZoneSegment
        {
            public ArenaWall Wall;
            public float From, To, At, Angle;
            public bool Horizontal;
            public float Length => To - From;
        }

        static void DestroyNow(GameObject go)

        {

            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);

        }


        public static ArenaFrameView Create(Transform parent, ArtLibrary art)
        {
            var pixel = art.Pixel;
            var go = new GameObject("ArenaFrame");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ArenaFrameView>();
            v.pixelSprite = pixel;
            v.art = art;
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

        public void Render(ArenaRect arena, int activeTick, MatchConfig config) => Render(arena, activeTick, config, null);

        /// <summary>Floor tiled with floor_&lt;arenaId&gt;, walls in that arena's color (arena_colors.json), teeth on spike walls.</summary>
        public void Render(ArenaRect arena, int activeTick, MatchConfig config, string arenaId)
        {
            // Snap edges to whole pixels so the frame never smears.
            var l = Mathf.Round(arena.Left.ToFloat());
            var r = Mathf.Round(arena.Right.ToFloat());
            var b = Mathf.Round(arena.Bottom.ToFloat());
            var t = Mathf.Round(arena.Top.ToFloat());

            ArtLibrary.ArenaArt look = default;
            var hasArt = art != null && arenaId != null && art.TryArena(arenaId, out look);
            if (hasArt && look.Floor != null)
            {
                if (tiledArena != arenaId)
                {
                    tiledArena = arenaId;
                    floor.sprite = look.Floor;
                    floor.drawMode = SpriteDrawMode.Tiled;
                    floor.color = Color.white;
                }
                floor.transform.localPosition = new Vector3((l + r) * 0.5f, (b + t) * 0.5f, 0f);
                floor.transform.localScale = Vector3.one;
                floor.size = new Vector2(r - l, t - b);
            }
            else
            {
                if (tiledArena != null)
                {
                    tiledArena = null;
                    floor.drawMode = SpriteDrawMode.Simple;
                    floor.sprite = pixelSprite;
                }
                floor.color = hasArt ? look.FloorColor : (Color32)Palette.Floor;
                Box(floor, l, b, r, t);
            }
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

            var spikes = config.Layout != null && config.Layout.WallDamage > Fix.Zero;
            var baseColor = hasArt ? look.WallColor : (Color32)Palette.Wall;   // spike arena: normal walls, spikes only in the corner zones
            var closing = art != null ? art.ClosingWallColor : (Color32)Palette.WallWarning;
            var zones = spikes ? ZoneSegments(config.Layout.SpikeCorners, arena) : NoZones;
            var teethArt = hasArt && art.SpikeTooth != null;
            DrawTeeth(teethArt ? zones : NoZones);
            DrawZoneStrips(teethArt ? NoZones : zones);
            var color = baseColor;
            var untilShrink = config.ShrinkStartTick - activeTick;
            if (untilShrink > 0 && untilShrink <= WarnTicks)
                color = (untilShrink / 8) % 2 == 0 ? closing : baseColor;   // blink ~4 Hz
            else if (untilShrink <= 0 && activeTick < config.ShrinkStartTick + config.ShrinkDurationTicks)
                color = closing;
            shownWallColor = color;
            foreach (var wall in walls) wall.color = color;
        }

        /// <summary>BB-04: hide obstacles the sim switched off (Warden step). Call after Render.</summary>
        public void ShowObstacles(MatchSim sim)
        {
            for (var k = 0; k < obstacleNodes.Count; k++)
            {
                var on = sim == null || k >= sim.Layout.ObstacleCount || sim.ObstacleActive(k);
                if (obstacleNodes[k] != null && obstacleNodes[k].activeSelf != on) obstacleNodes[k].SetActive(on);
            }
        }

        /// <summary>Obstacles drawn now (tests).</summary>
        public int ObstaclesShown
        {
            get
            {
                var n = 0;
                foreach (var o in obstacleNodes) if (o != null && o.activeSelf) n++;
                return n;
            }
        }

        /// <summary>Wall color drawn now (tests).</summary>
        public Color32 WallColor => shownWallColor;

        /// <summary>Floor renderer (tests).</summary>
        public SpriteRenderer Floor => floor;

        /// <summary>Spike teeth shown now (tests).</summary>
        public int TeethShown => activeTeeth;

        /// <summary>Spike zone strips shown now when there is no tooth art (tests).</summary>
        public int ZoneStripsShown => activeStrips;

        static readonly ZoneSegment[] NoZones = new ZoneSegment[0];

        /// <summary>
        /// Spike zones of the sim rect <paramref name="arena"/> for the given corners, exactly as the sim defines them
        /// (SpikeZones.Segments: SpikeZoneNum/SpikeZoneDen of each wall from the corner, on both walls meeting there, from the
        /// current, shrinking rect), converted to floats for drawing. The wall line (At) is the rounded edge the frame draws.
        /// </summary>
        public static ZoneSegment[] ZoneSegments(SpikeCorner corners, ArenaRect arena)
        {
            var segs = SpikeZones.Segments(arena, corners);
            var l = Mathf.Round(arena.Left.ToFloat());
            var r = Mathf.Round(arena.Right.ToFloat());
            var b = Mathf.Round(arena.Bottom.ToFloat());
            var t = Mathf.Round(arena.Top.ToFloat());
            var list = new ZoneSegment[segs.Length];
            for (var i = 0; i < segs.Length; i++)
            {
                var s = segs[i];
                float at, angle;
                switch (s.Wall)
                {
                    case ArenaWall.Top: at = t; angle = 0f; break;
                    case ArenaWall.Bottom: at = b; angle = 180f; break;
                    case ArenaWall.Left: at = l; angle = 90f; break;
                    default: at = r; angle = -90f; break;
                }
                list[i] = new ZoneSegment { Wall = s.Wall, From = s.From.ToFloat(), To = s.To.ToFloat(), At = at, Angle = angle, Horizontal = s.Horizontal };
            }
            return list;
        }

        /// <summary>
        /// spike_tooth (6x4, pointing down, pivot top-centre) every ToothSpacing px along the inside of each spike zone, pointing
        /// into the arena; follows the walls when they close in. The rest of the walls keep the arena's normal look.
        /// </summary>
        void DrawTeeth(ZoneSegment[] zones)
        {
            var n = 0;
            foreach (var z in zones) n = Edge(n, z.From, z.To, z.At, z.Angle, z.Horizontal);
            for (var i = n; i < activeTeeth; i++) teeth[i].enabled = false;
            activeTeeth = n;
        }

        /// <summary>No tooth art: the zones are drawn as red strips over the wall.</summary>
        void DrawZoneStrips(ZoneSegment[] zones)
        {
            var w = WallThickness;
            for (var i = 0; i < zones.Length; i++)
            {
                if (i >= zoneStrips.Count) zoneStrips.Add(Quad(transform, "SpikeZone", pixelSprite, SpikeColor, WallOrder + 1));
                var s = zoneStrips[i];
                s.enabled = true;
                var z = zones[i];
                switch (z.Wall)
                {
                    case ArenaWall.Top: Box(s, z.From, z.At, z.To, z.At + w); break;
                    case ArenaWall.Bottom: Box(s, z.From, z.At - w, z.To, z.At); break;
                    case ArenaWall.Left: Box(s, z.At - w, z.From, z.At, z.To); break;
                    default: Box(s, z.At, z.From, z.At + w, z.To); break;
                }
            }
            for (var i = zones.Length; i < activeStrips; i++) zoneStrips[i].enabled = false;
            activeStrips = zones.Length;
        }

        int Edge(int n, float from, float to, float at, float angle, bool horizontal)
        {
            var count = Mathf.FloorToInt((to - from) / ToothSpacing);
            var start = Mathf.Round((from + to) * 0.5f - (count - 1) * ToothSpacing * 0.5f);
            for (var i = 0; i < count; i++, n++)
            {
                if (n >= teeth.Count)
                {
                    var go = new GameObject("Tooth");
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sprite = art.SpikeTooth;
                    sr.sortingOrder = ToothOrder;
                    teeth.Add(sr);
                }
                var tooth = teeth[n];
                tooth.enabled = true;
                var along = start + i * ToothSpacing;
                tooth.transform.localPosition = horizontal ? new Vector3(along, at, 0f) : new Vector3(at, along, 0f);
                tooth.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
            return n;
        }

        /// <summary>Static obstacles as pixel rows (circles) under the balls; rebuilt only when the layout changes.</summary>
        void DrawObstacles(ArenaLayout layout)
        {
            if (ReferenceEquals(layout, drawnLayout)) return;
            drawnLayout = layout;
            if (obstacleRoot != null) DestroyNow(obstacleRoot.gameObject);
            var root = new GameObject("Obstacles");
            root.transform.SetParent(transform, false);
            obstacleRoot = root.transform;
            obstacleNodes.Clear();
            for (var i = 0; i < layout.ObstacleCount; i++)
            {
                var o = layout.Get(i);
                var node = new GameObject("Obstacle" + i);
                node.transform.SetParent(obstacleRoot, false);
                obstacleNodes.Add(node);
                var color = o.Damage > Fix.Zero ? SpikeColor : (o.Boost > Fix.Zero ? BumperColor : PostColor);
                var steps = o.Shape == ObstacleShape.Circle ? 1 : Mathf.Max(1, Mathf.CeilToInt((o.B - o.A).Length.ToFloat()));
                for (var s = 0; s < steps; s++)
                {
                    var a = new Vector2(o.A.X.ToFloat(), o.A.Y.ToFloat());
                    var c = o.Shape == ObstacleShape.Circle ? a : a + (new Vector2(o.B.X.ToFloat(), o.B.Y.ToFloat()) - a) * (s / (float)steps);
                    var r = Mathf.RoundToInt(o.Radius.ToFloat());
                    for (var dy = -r; dy < r; dy++)
                    {
                        var half = Mathf.Round(Mathf.Sqrt(r * r - (dy + 0.5f) * (dy + 0.5f)));
                        var row = Quad(node.transform, "Row", pixelSprite, color, ObstacleOrder);
                        var y = Mathf.Round(c.y) + dy;
                        Box(row, Mathf.Round(c.x) - half, y, Mathf.Round(c.x) + half, y + 1);
                    }
                }
            }
        }
    }
}
