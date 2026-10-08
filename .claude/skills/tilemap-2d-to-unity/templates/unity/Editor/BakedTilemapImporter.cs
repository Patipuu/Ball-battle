// Generic importer for the neutral baked format (see references/baked-format.md).
// Copy into Assets/<YourGame>/Editor/, set the constants, then use the menu
// "Tools/Tilemap Import/Import Baked Sprites" and "Tools/Tilemap Import/Build Map Scene...".
// Needs com.unity.2d.tilemap and Newtonsoft Json (com.unity.nuget.newtonsoft-json).
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace TilemapImport
{
    /// <summary>One sprite layer of a frame: PNG path (relative to the baked root) and its anchor in px, y down.</summary>
    public sealed class BakedLayer
    {
        [JsonProperty("png")] public string Png;
        [JsonProperty("ox")] public int Ox;
        [JsonProperty("oy")] public int Oy;
    }

    public sealed class BakedFrame
    {
        [JsonProperty("layers")] public List<BakedLayer> Layers;
        [JsonProperty("dur")] public int Dur;
    }

    /// <summary>
    /// Copies the baked folder into the project and imports every PNG as a pixel-exact sprite:
    /// Point filter, no compression, no mipmaps, pixels-per-unit = one cell, pivot = the layer anchor.
    /// A sprite placed at its cell's bottom-left corner then lands exactly where the original drew it.
    /// </summary>
    public static class BakedTilemapImporter
    {
        /// <summary>Folder with objects.json, sprites/, maps/ (outside Assets, e.g. next to it).</summary>
        public const string SourceRoot = "../_source/baked";
        /// <summary>Where the copy lives inside the project.</summary>
        public const string ArtRoot = "Assets/Art/Baked";
        /// <summary>Cell size of the original game in pixels.</summary>
        public const int TilePx = 40;

        static Dictionary<string, Vector2Int> anchors;

        public static string ProjectRoot => Path.GetFullPath(Path.Combine(Application.dataPath, ".."));

        public static Dictionary<string, Dictionary<string, List<BakedFrame>>> LoadObjects()
        {
            string path = Path.Combine(ProjectRoot, ArtRoot, "objects.json");
            var parsed = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, List<BakedFrame>>>>(File.ReadAllText(path));
            // Map data often spells object names in another case than the sprite tables.
            return new Dictionary<string, Dictionary<string, List<BakedFrame>>>(parsed, System.StringComparer.OrdinalIgnoreCase);
        }

        [MenuItem("Tools/Tilemap Import/Import Baked Sprites")]
        public static void Import()
        {
            string src = Path.GetFullPath(Path.Combine(ProjectRoot, SourceRoot));
            string dst = Path.Combine(ProjectRoot, ArtRoot);
            bool anchorsChanged = CopyIfChanged(Path.Combine(src, "objects.json"), Path.Combine(dst, "objects.json"));
            foreach (var file in Directory.GetFiles(Path.Combine(src, "sprites"), "*.png", SearchOption.AllDirectories))
                CopyIfChanged(file, Path.Combine(dst, "sprites", file.Substring(Path.Combine(src, "sprites").Length + 1)));
            anchors = null;
            AssetDatabase.Refresh();
            // Pivots come from objects.json: when it changed, re-import every sprite so they follow.
            if (anchorsChanged)
                AssetDatabase.ImportAsset(ArtRoot + "/sprites", ImportAssetOptions.ForceUpdate | ImportAssetOptions.ImportRecursive);
        }

        /// <summary>Only changed files are copied (thousands of small PNGs).</summary>
        static bool CopyIfChanged(string from, string to)
        {
            if (File.Exists(to) && new FileInfo(from).Length == new FileInfo(to).Length
                && System.Linq.Enumerable.SequenceEqual(File.ReadAllBytes(from), File.ReadAllBytes(to)))
                return false;
            Directory.CreateDirectory(Path.GetDirectoryName(to));
            File.Copy(from, to, true);
            return true;
        }

        public static Sprite LoadSprite(string png)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + "/" + png);
        }

        internal static bool TryAnchor(string assetPath, out Vector2Int anchor)
        {
            if (anchors == null)
            {
                anchors = new Dictionary<string, Vector2Int>();
                foreach (var states in LoadObjects().Values)
                    foreach (var frames in states.Values)
                        foreach (var f in frames)
                            foreach (var l in f.Layers)
                                anchors[l.Png] = new Vector2Int(l.Ox, l.Oy);
            }
            return anchors.TryGetValue(assetPath.Substring(ArtRoot.Length + 1), out anchor);
        }
    }

    /// <summary>Import settings for every PNG under ArtRoot/sprites.</summary>
    public sealed class BakedTilemapPostprocessor : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(BakedTilemapImporter.ArtRoot + "/sprites/"))
                return;
            Vector2Int anchor;
            if (!BakedTilemapImporter.TryAnchor(assetPath, out anchor))
                return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = BakedTilemapImporter.TilePx;
            importer.filterMode = FilterMode.Point;               // crisp pixels (Bilinear = softer, like a scaled old client)
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            var size = PngSize(Path.Combine(BakedTilemapImporter.ProjectRoot, assetPath));
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            // Anchor (ox, oy) is y-down inside the PNG; Unity pivots are normalized and y-up.
            settings.spritePivot = new Vector2((float)anchor.x / size.x, (float)(size.y - 1 - anchor.y) / size.y);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // keep transparent borders, pivot stays exact
            importer.SetTextureSettings(settings);
        }

        /// <summary>PNG size from the IHDR chunk (the texture is not imported yet).</summary>
        static Vector2Int PngSize(string path)
        {
            using (var f = File.OpenRead(path))
            {
                var b = new byte[24];
                f.Read(b, 0, 24);
                return new Vector2Int((b[16] << 24) | (b[17] << 16) | (b[18] << 8) | b[19],
                    (b[20] << 24) | (b[21] << 16) | (b[22] << 8) | b[23]);
            }
        }
    }
}
