using System.IO;
using BallBattle.View;
using UnityEditor;
using UnityEngine;

namespace BallBattle.EditorTools
{
    /// <summary>
    /// Writes placeholder pixel art PNGs (balls, blades, font, 1px white) and the ArtLibrary asset.
    /// Real art later: overwrite the PNGs (same names, borders) or repoint ArtLibrary. Idempotent.
    /// Menu: BallBattle/Generate Placeholder Art. Pixel drawing lives in PlaceholderArtPainter.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        public const string Folder = "Assets/Art/Generated";
        public const string LibraryPath = "Assets/Art/ArtLibrary.asset";

        [MenuItem("BallBattle/Generate Placeholder Art")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);

            Write("pixel.png", PlaceholderArtPainter.Pixel());
            Write("font_3x5.png", PlaceholderArtPainter.Font());
            foreach (var look in Palette.Weapons)
            {
                Write($"ball_{look.Id}.png", PlaceholderArtPainter.Ball(look));
                var blade = PlaceholderArtPainter.Blade(look.Id, out _);
                if (blade != null) Write($"blade_{look.Id}.png", blade);
            }
            AssetDatabase.Refresh();

            ConfigureSprite($"{Folder}/pixel.png", new Vector2(0.5f, 0.5f), Vector4.zero);
            ConfigureSprite($"{Folder}/font_3x5.png", Vector2.zero, Vector4.zero);
            foreach (var look in Palette.Weapons)
            {
                ConfigureSprite($"{Folder}/ball_{look.Id}.png", new Vector2(0.5f, 0.5f), Vector4.zero);
                if (PlaceholderArtPainter.Blade(look.Id, out var border) != null)
                    // Pivot y = 2/5: blade is 5 px tall, so its edges sit on whole pixels (y-2 .. y+3).
                    ConfigureSprite($"{Folder}/blade_{look.Id}.png", new Vector2(0f, 0.4f), border);
            }

            BuildLibrary();
            Debug.Log("[BallBattle] Placeholder art generated");
        }

        static void Write(string file, Texture2D tex)
        {
            File.WriteAllBytes($"{Folder}/{file}", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Pixel-art import: 1 PPU, point filter, no compression/mips, full-rect mesh (needed for 9-slice).</summary>
        static void ConfigureSprite(string path, Vector2 pivot, Vector4 border)
        {
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spritePixelsPerUnit = 1f;
            imp.filterMode = FilterMode.Point;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.npotScale = TextureImporterNPOTScale.None;

            var s = new TextureImporterSettings();
            imp.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Custom;
            s.spritePivot = pivot;
            s.spriteBorder = border;
            s.spriteExtrude = 0;
            imp.SetTextureSettings(s);
            imp.SaveAndReimport();
        }

        static void BuildLibrary()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>(LibraryPath);
            if (lib == null)
            {
                lib = ScriptableObject.CreateInstance<ArtLibrary>();
                AssetDatabase.CreateAsset(lib, LibraryPath);
            }

            lib.Pixel = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/pixel.png");
            lib.Font = AssetDatabase.LoadAssetAtPath<Texture2D>($"{Folder}/font_3x5.png");
            lib.Weapons = new ArtLibrary.WeaponArt[Palette.Weapons.Length];
            for (var i = 0; i < Palette.Weapons.Length; i++)
            {
                var id = Palette.Weapons[i].Id;
                lib.Weapons[i] = new ArtLibrary.WeaponArt
                {
                    Id = id,
                    Ball = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/ball_{id}.png"),
                    Blade = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/blade_{id}.png")
                };
            }
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }
    }
}
