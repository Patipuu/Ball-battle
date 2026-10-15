using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BallBattle.Sim;
using BallBattle.Sim.Traits;
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
        /// <summary>Real art (v1). Used for a weapon when its file exists; the placeholders above stay as the fallback.</summary>
        public const string SpritesFolder = "Assets/Art/Sprites";
        /// <summary>Blade 9-slice borders per weapon id: [left, bottom, right, top] in px (Unity's Vector4 border order).</summary>
        public const string BladeBordersPath = SpritesFolder + "/blade_borders.json";

        [MenuItem("BallBattle/Generate Placeholder Art")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);

            Write("pixel.png", PlaceholderArtPainter.Pixel());
            Write("font_3x5.png", PlaceholderArtPainter.Font());
            Write("ball_flash.png", PlaceholderArtPainter.BallFlash());
            foreach (var look in Palette.Weapons)
            {
                Write($"ball_{look.Id}.png", PlaceholderArtPainter.Ball(look));
                var blade = PlaceholderArtPainter.Blade(look.Id, out _);
                if (blade != null) Write($"blade_{look.Id}.png", blade);
            }
            AssetDatabase.Refresh();

            ConfigureSprite($"{Folder}/pixel.png", new Vector2(0.5f, 0.5f), Vector4.zero);
            ConfigureSprite($"{Folder}/font_3x5.png", Vector2.zero, Vector4.zero);
            ConfigureSprite($"{Folder}/ball_flash.png", new Vector2(0.5f, 0.5f), Vector4.zero);
            foreach (var look in Palette.Weapons)
            {
                ConfigureSprite($"{Folder}/ball_{look.Id}.png", new Vector2(0.5f, 0.5f), Vector4.zero);
                if (PlaceholderArtPainter.Blade(look.Id, out var border) != null)
                    // Pivot y = 2/5: blade is 5 px tall, so its edges sit on whole pixels (y-2 .. y+3).
                    ConfigureSprite($"{Folder}/blade_{look.Id}.png", new Vector2(0f, 0.4f), border);
            }

            ConfigureRealArt();
            BuildLibrary();
            Debug.Log("[BallBattle] Placeholder art generated, real sprites configured");
        }

        static void Write(string file, Texture2D tex)
        {
            File.WriteAllBytes($"{Folder}/{file}", tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Pixel-art import: 1 PPU, point filter, no compression/mips, full-rect mesh (needed for 9-slice).</summary>
        /// <summary>
        /// Import settings for the real sprites (art v1): 1 PPU, point, uncompressed. Balls and bosses are readable so the
        /// outline flash can use their own dark rim (SpriteOutlineEdgeSource); blades get their 9-slice borders from
        /// blade_borders.json (only the 5 px core stretches). HUD sprites are imported for later (retry tokens: v4).
        /// </summary>
        public static void ConfigureRealArt()
        {
            if (!AssetDatabase.IsValidFolder(SpritesFolder)) { Debug.LogWarning("[BallBattle] No " + SpritesFolder + ": real art skipped"); return; }
            var borders = ReadBladeBorders();
            foreach (var look in Palette.Weapons)
            {
                var ball = $"{SpritesFolder}/ball_{look.Id}.png";
                if (File.Exists(ball)) ConfigureSprite(ball, new Vector2(0.5f, 0.5f), Vector4.zero, readable: true);
                var blade = $"{SpritesFolder}/blade_{look.Id}.png";
                if (!File.Exists(blade)) continue;
                if (!borders.TryGetValue(look.Id, out var border)) Debug.LogWarning($"[BallBattle] {blade}: no entry in blade_borders.json, imported without borders");
                // Pivot y = 3/7: the blade is 7 px tall (1 + 5 + 1), so its edges sit on whole pixels (y-3 .. y+4).
                ConfigureSprite(blade, new Vector2(0f, 3f / 7f), border);
            }
            foreach (var hud in new[] { "hud/retry_token_on.png", "hud/retry_token_off.png", "hud/icon_antidote.png" })
                if (File.Exists($"{SpritesFolder}/{hud}")) ConfigureSprite($"{SpritesFolder}/{hud}", new Vector2(0.5f, 0.5f), Vector4.zero);
            // Balls pre-drawn at x1.2 / x1.35 / x1.5 (ball_sizes/ball_<id>_38|43|48.png): same settings as ball_<id>.png.
            if (AssetDatabase.IsValidFolder(SpritesFolder + "/ball_sizes"))
                foreach (var file in Directory.GetFiles(SpritesFolder + "/ball_sizes", "*.png"))
                    ConfigureSprite(file.Replace('\\', '/'), new Vector2(0.5f, 0.5f), Vector4.zero, readable: true);
            if (AssetDatabase.IsValidFolder(SpritesFolder + "/boss"))
                foreach (var file in Directory.GetFiles(SpritesFolder + "/boss", "*.png"))
                    ConfigureSprite(file.Replace('\\', '/'), BossPivot, Vector4.zero, readable: true);

            // Art v1 (5b): trait icons, card frames (9-slice 4 px), arena floor tiles and the spike tooth.
            foreach (var folder in new[] { "icons", "arena" })
                if (AssetDatabase.IsValidFolder($"{SpritesFolder}/{folder}"))
                    foreach (var file in Directory.GetFiles($"{SpritesFolder}/{folder}", "*.png"))
                    {
                        var path = file.Replace('\\', '/');
                        var tooth = path.EndsWith("/spike_tooth.png");
                        ConfigureSprite(path, tooth ? new Vector2(0.5f, 1f) : new Vector2(0.5f, 0.5f), Vector4.zero);
                    }
            if (AssetDatabase.IsValidFolder(SpritesFolder + "/card"))
                foreach (var file in Directory.GetFiles(SpritesFolder + "/card", "*.png"))
                    ConfigureSprite(file.Replace('\\', '/'), new Vector2(0.5f, 0.5f), new Vector4(4, 4, 4, 4));
        }

        public const string ArenaColorsPath = SpritesFolder + "/arena/arena_colors.json";

        /// <summary>arena_colors.json: arena id → (floor, wall) colors, plus "closing_wall" (null when absent).</summary>
        public static Dictionary<string, (Color32 floor, Color32 wall)> ReadArenaColors(out Color32? closingWall)
        {
            var result = new Dictionary<string, (Color32, Color32)>();
            closingWall = null;
            if (!File.Exists(ArenaColorsPath)) return result;
            var json = File.ReadAllText(ArenaColorsPath);
            const string rgb = "\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]";
            foreach (Match m in Regex.Matches(json, "\"([\\w-]+)\"\\s*:\\s*\\{\\s*\"floor\"\\s*:\\s*" + rgb + "\\s*,\\s*\"wall\"\\s*:\\s*" + rgb + "\\s*\\}"))
                result[m.Groups[1].Value] = (Rgb(m, 2), Rgb(m, 5));
            var c = Regex.Match(json, "\"closing_wall\"\\s*:\\s*" + rgb);
            if (c.Success) closingWall = Rgb(c, 1);
            return result;
        }

        static Color32 Rgb(Match m, int first) =>
            new Color32(byte.Parse(m.Groups[first].Value), byte.Parse(m.Groups[first + 1].Value), byte.Parse(m.Groups[first + 2].Value), 255);

        static Sprite Real(string file) => AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{file}");

        /// <summary>Art v1 entries of the library (HUD, traits, cards, bosses, arenas). Missing files stay null and are logged.</summary>
        static void FillArtV1(ArtLibrary lib)
        {
            lib.RetryTokenOn = Real("hud/retry_token_on.png");
            lib.RetryTokenOff = Real("hud/retry_token_off.png");
            lib.IconAntidote = Real("hud/icon_antidote.png");

            var traits = new List<ArtLibrary.TraitArt>();
            foreach (var t in TraitRegistry.All)
            {
                var icon = Real($"icons/trait_{t.Id}.png");
                if (icon == null) Debug.LogWarning($"[BallBattle] missing icons/trait_{t.Id}.png");
                traits.Add(new ArtLibrary.TraitArt { Id = t.Id, Icon = icon });
            }
            lib.Traits = traits.ToArray();
            var skills = new List<ArtLibrary.TraitArt>();   // UI-UX1 signature-skill icons; a missing file stays null (SKILL UP falls back to card_swap)
            foreach (var s in BallBattle.Sim.Skills.SkillDefinition.All)
            {
                var icon = Real($"icons/skill_{s.WeaponId}.png");
                if (icon != null) skills.Add(new ArtLibrary.TraitArt { Id = s.WeaponId, Icon = icon });
            }
            lib.Skills = skills.ToArray();
            lib.CardFrames = new[] { Real("card/card_frame_common.png"), Real("card/card_frame_uncommon.png"), Real("card/card_frame_rare.png"), Real("card/card_frame_counter.png") };
            lib.CardAntidote = Real("icons/card_antidote.png");
            lib.CardSpeedCap = Real("icons/card_speed-cap.png");
            lib.CardMaxHp = Real("icons/card_max-hp.png");
            lib.CardDamage = Real("icons/card_damage.png");
            lib.CardHeal = Real("icons/card_heal.png");
            lib.CardSwap = Real("icons/card_swap.png");
            foreach (var (file, sprite) in new[] { ("card_speed-cap", lib.CardSpeedCap), ("card_max-hp", lib.CardMaxHp), ("card_damage", lib.CardDamage), ("card_heal", lib.CardHeal), ("card_swap", lib.CardSwap) })
                if (sprite == null) Debug.LogWarning($"[BallBattle] missing icons/{file}.png");

            lib.ShellKing = Real("boss/shellking.png");
            lib.ShellKingGlow = Real("boss/shellking_glow.png");
            lib.Mimic = new[] { Real("boss/mimic_idle.png"), Real("boss/mimic_wp.png"), Real("boss/mimic_sh.png"), Real("boss/mimic_ps.png"), Real("boss/mimic_rf.png") };
            Art2Linker.Link(lib); // v2 overlays/bodies; original 64px base sprites remain.

            var colors = ReadArenaColors(out var closing);
            var arenas = new List<ArtLibrary.ArenaArt>();
            foreach (var a in ArenaRegistry.All)
            {
                var floor = Real($"arena/floor_{a.Id}.png");
                if (floor == null) Debug.LogWarning($"[BallBattle] missing arena/floor_{a.Id}.png");
                if (!colors.TryGetValue(a.Id, out var c)) { Debug.LogWarning($"[BallBattle] arena_colors.json has no {a.Id}"); c = (Palette.Floor, Palette.Wall); }
                arenas.Add(new ArtLibrary.ArenaArt { Id = a.Id, Floor = floor, FloorColor = c.Item1, WallColor = c.Item2 });
            }
            lib.Arenas = arenas.ToArray();
            if (closing.HasValue) lib.ClosingWallColor = closing.Value;
            lib.SpikeTooth = Real("arena/spike_tooth.png");
        }

        /// <summary>Boss sprites are 64x64 with the body centre at (32, 34) counted from the top-left, i.e. (32, 30) from the bottom.</summary>
        public static readonly Vector2 BossPivot = new Vector2(32f / 64f, 30f / 64f);

        /// <summary>blade_borders.json as id → Unity border (left, bottom, right, top).</summary>
        public static Dictionary<string, Vector4> ReadBladeBorders()
        {
            var result = new Dictionary<string, Vector4>();
            if (!File.Exists(BladeBordersPath)) return result;
            var json = File.ReadAllText(BladeBordersPath);
            foreach (Match m in Regex.Matches(json, "\"([\\w-]+)\"\\s*:\\s*\\[\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*,\\s*(\\d+)\\s*\\]"))
                result[m.Groups[1].Value] = new Vector4(int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value), int.Parse(m.Groups[4].Value), int.Parse(m.Groups[5].Value));
            return result;
        }

        /// <summary>The sprite to use for a file name: real art when present, else the placeholder.</summary>
        static Sprite Pick(string file)
        {
            var real = AssetDatabase.LoadAssetAtPath<Sprite>($"{SpritesFolder}/{file}");
            return real != null ? real : AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/{file}");
        }

        static void ConfigureSprite(string path, Vector2 pivot, Vector4 border, bool readable = false)
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
            imp.isReadable = readable;

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
            lib.BallFlash = AssetDatabase.LoadAssetAtPath<Sprite>($"{Folder}/ball_flash.png");
            lib.Weapons = new ArtLibrary.WeaponArt[Palette.Weapons.Length];
            for (var i = 0; i < Palette.Weapons.Length; i++)
            {
                var id = Palette.Weapons[i].Id;
                lib.Weapons[i] = new ArtLibrary.WeaponArt
                {
                    Id = id,
                    Ball = Pick($"ball_{id}.png"),
                    Ball38 = Real($"ball_sizes/ball_{id}_38.png"),
                    Ball43 = Real($"ball_sizes/ball_{id}_43.png"),
                    Ball48 = Real($"ball_sizes/ball_{id}_48.png"),
                    Blade = Pick($"blade_{id}.png")
                };
            }
            FillArtV1(lib);
            EditorUtility.SetDirty(lib);
            AssetDatabase.SaveAssets();
        }
    }
}
