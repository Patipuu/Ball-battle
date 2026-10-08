// Generic scene builder for the neutral baked format (see references/baked-format.md).
// Builds Assets/Scenes/<Map>.unity: one Tilemap per map layer, one Tile asset per cell id
// (sprite = frame 0 of the object's "Default" state), a camera framing the map, and can capture
// the map rectangle to PNG for pixel comparison with scripts/reference_render.py.
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace TilemapImport
{
    public sealed class BakedMap
    {
        [JsonProperty("width")] public int Width;
        [JsonProperty("height")] public int Height;
        [JsonProperty("layers")] public int Layers;
        /// <summary>One array of layer ids per cell, row-major (y * width + x), y = 0 at the top.</summary>
        [JsonProperty("grid")] public List<long[]> Grid;
        /// <summary>Cell id -> object name ("cells" is accepted as an alias).</summary>
        [JsonProperty("palette")] public Dictionary<string, string> Palette;
        [JsonProperty("cells")] Dictionary<string, string> Cells { set { Palette = Palette ?? value; } }
        [JsonProperty("empty")] public long Empty = 0xFFFFFFFF;
    }

    public static class TilemapSceneBuilder
    {
        /// <summary>Baked map with palette (and grid). If the grid lives elsewhere, set GridRoot.</summary>
        public const string MapsDir = BakedTilemapImporter.SourceRoot + "/maps";
        /// <summary>Optional folder with map JSONs holding width/height/grid when the baked ones only have the palette.</summary>
        public static string GridRoot = null;

        public static string ScenePath(string map) => "Assets/Scenes/" + map + ".unity";

        public static BakedMap LoadMap(string map)
        {
            string root = BakedTilemapImporter.ProjectRoot;
            var doc = JsonConvert.DeserializeObject<BakedMap>(File.ReadAllText(Path.Combine(root, MapsDir, map + ".json")));
            if (doc.Grid == null && GridRoot != null)
            {
                var grid = JsonConvert.DeserializeObject<BakedMap>(File.ReadAllText(Path.Combine(root, GridRoot, map + ".json")));
                doc.Width = grid.Width;
                doc.Height = grid.Height;
                doc.Grid = grid.Grid;
                doc.Layers = grid.Layers;
            }
            if (doc.Layers <= 0 && doc.Grid != null && doc.Grid.Count > 0)
                doc.Layers = doc.Grid[0].Length;
            return doc;
        }

        public static void BuildMap(string map)
        {
            var data = LoadMap(map);
            // Create the scene first: NewScene(Single) unloads unreferenced assets, which would null fresh tiles.
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var objects = BakedTilemapImporter.LoadObjects();
            var tiles = MakeTiles(map, data, objects);

            var root = new GameObject("Map");
            root.AddComponent<Grid>().cellSize = Vector3.one;
            for (int layer = 0; layer < data.Layers; layer++)
            {
                var go = new GameObject("Layer" + layer);
                go.transform.SetParent(root.transform, false);
                var tilemap = go.AddComponent<Tilemap>();
                tilemap.tileAnchor = Vector3.zero;  // sprite pivot = anchor on the cell's bottom-left corner
                var renderer = go.AddComponent<TilemapRenderer>();
                // Per-tile sorting: tall statics interleave with characters by row instead of by chunk.
                renderer.mode = TilemapRenderer.Mode.Individual;
                renderer.sortingOrder = layer;
                for (int y = 0; y < data.Height; y++)
                    for (int x = 0; x < data.Width; x++)
                    {
                        long id = data.Grid[y * data.Width + x][layer];
                        Tile tile;
                        if (id != data.Empty && tiles.TryGetValue(id, out tile))
                            tilemap.SetTile(new Vector3Int(x, data.Height - 1 - y, 0), tile);  // file y down -> Unity y up
                    }
            }
            var cam = new GameObject("Main Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
            cam.orthographic = true;
            cam.orthographicSize = data.Height / 2f;
            cam.transform.position = new Vector3(data.Width / 2f, data.Height / 2f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
            EditorSceneManager.SaveScene(scene, ScenePath(map));
        }

        static Dictionary<long, Tile> MakeTiles(string map, BakedMap data, Dictionary<string, Dictionary<string, List<BakedFrame>>> objects)
        {
            string dir = "Assets/Art/Maps/" + map;
            if (AssetDatabase.IsValidFolder(dir))
                AssetDatabase.DeleteAsset(dir);
            Directory.CreateDirectory(Path.Combine(BakedTilemapImporter.ProjectRoot, dir));
            AssetDatabase.Refresh();
            var tiles = new Dictionary<long, Tile>();
            foreach (var kv in data.Palette)
            {
                Dictionary<string, List<BakedFrame>> states;
                if (!objects.TryGetValue(kv.Value, out states) || states.Count == 0)
                {
                    Debug.LogWarning(map + ": no baked object " + kv.Value);
                    continue;
                }
                List<BakedFrame> frames;
                if (!states.TryGetValue("Default", out frames))
                    foreach (var s in states.Values) { frames = s; break; }
                if (frames == null || frames.Count == 0 || frames[0].Layers == null || frames[0].Layers.Count == 0)
                    continue;
                // Multi-layer frames (e.g. shadow + body) need one Tilemap per layer; the first layer is the body here.
                var sprite = BakedTilemapImporter.LoadSprite(frames[0].Layers[0].Png);
                if (sprite == null)
                    continue;
                var tile = ScriptableObject.CreateInstance<Tile>();
                tile.sprite = sprite;
                tile.colliderType = Tile.ColliderType.None;
                AssetDatabase.CreateAsset(tile, dir + "/tile_" + kv.Key + ".asset");
                tiles[long.Parse(kv.Key)] = tile;
            }
            AssetDatabase.SaveAssets();
            return tiles;
        }

        /// <summary>Renders the open scene's camera to the map size in pixels and saves a PNG.</summary>
        public static void Capture(string map, string pngPath)
        {
            var data = LoadMap(map);
            int w = data.Width * BakedTilemapImporter.TilePx, h = data.Height * BakedTilemapImporter.TilePx;
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var img = new Texture2D(w, h, TextureFormat.RGB24, false);
            img.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            img.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            Object.DestroyImmediate(rt);
            File.WriteAllBytes(pngPath, img.EncodeToPNG());
        }

        [MenuItem("Tools/Tilemap Import/Build Map Scene...")]
        static void BuildFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Baked map", Path.Combine(BakedTilemapImporter.ProjectRoot, MapsDir), "json");
            if (string.IsNullOrEmpty(path))
                return;
            string map = Path.GetFileNameWithoutExtension(path);
            BuildMap(map);
            Capture(map, Path.Combine(BakedTilemapImporter.ProjectRoot, map + "-capture.png"));
        }
    }
}
