using System;
using System.IO;
using System.Linq;
using BallBattle.Sim.Skills;
using BallBattle.View;
using UnityEditor;
using UnityEngine;

namespace BallBattle.EditorTools
{
    public static class Art2Linker
    {
        public static readonly string[] BossFiles = {"magnet_body","warden_body","shellking_shell_on","shellking_shell_warn",
            "mimic_flash_1","mimic_flash_2","mimic_flash_3","magnet_ring_warn","magnet_ring_pull"};
        public static readonly string[] WallFiles = {"warden_spike_left_on","warden_spike_left_fade","warden_spike_right_on",
            "warden_spike_right_fade","warden_spike_top_on","warden_spike_top_fade","warden_wallflash","bumper_rings"};
        public static string[] SpritePaths => BossFiles.Select(f=>ArtLibrary.BossSpriteFolder+"/"+f+".png")
            .Concat(WallFiles.Select(f=>PlaceholderArtGenerator.SpritesFolder+"/boss/"+f+".png"))
            .Concat(SkillDefinition.All.Select(s=>PlaceholderArtGenerator.SpritesFolder+"/role_icons/role_"+s.Role.ToLowerInvariant()+".png"))
            .Concat(new[]{PlaceholderArtGenerator.SpritesFolder+"/arena_thumbs/thumb_spike-walls_v3.png"}).ToArray();
        [InitializeOnLoadMethod] static void OvernightImport()
        {
            if(Environment.GetEnvironmentVariable("BB_NIGHT1_PHASE")=="D") EditorApplication.delayCall+=ConfigureAndLink;
        }
        [MenuItem("BallBattle/Link ART-2")]
        public static void ConfigureAndLink()
        {
            foreach(var path in SpritePaths)
            {
                if(!File.Exists(path)) {Debug.LogWarning("[ART-2] missing "+path);continue;}
                var importer=AssetImporter.GetAtPath(path) as TextureImporter;
                if(importer==null) {AssetDatabase.ImportAsset(path);importer=AssetImporter.GetAtPath(path) as TextureImporter;}
                importer.textureType=TextureImporterType.Sprite; importer.spriteImportMode=SpriteImportMode.Single;
                importer.filterMode=FilterMode.Point; importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.mipmapEnabled=false; importer.spritePixelsPerUnit=1; importer.npotScale=TextureImporterNPOTScale.None;
                importer.alphaIsTransparency=true; importer.isReadable=true;
                var pivot=path.Contains("warden_spike_left_") ? new Vector2(0,.5f) : path.Contains("warden_spike_right_") ? new Vector2(1,.5f) :
                    path.Contains("warden_spike_top_") ? new Vector2(.5f,1) : new Vector2(.5f,.5f);
                var settings=new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                settings.spriteAlignment=(int)SpriteAlignment.Custom; settings.spritePivot=pivot; settings.spriteMeshType=SpriteMeshType.FullRect; importer.SetTextureSettings(settings);
                foreach(var platform in new[]{"DefaultTexturePlatform","Standalone","WebGL","iPhone","Android","VisionOS","tvOS","WindowsStoreApps"})
                {
                    var target=importer.GetPlatformTextureSettings(platform); target.name=platform;
                    target.textureCompression=TextureImporterCompression.Uncompressed; target.crunchedCompression=false;
                    importer.SetPlatformTextureSettings(target);
                }
                importer.SaveAndReimport();
            }
            var library=AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Link(library); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
        }
        public static void Link(ArtLibrary library)
        {
            Sprite Boss(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(ArtLibrary.BossSpriteFolder+"/"+name+".png");
            Sprite Wall(string name)=>AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SpritesFolder+"/boss/"+name+".png");
            library.MagnetBody=Boss("magnet_body"); library.WardenBody=Boss("warden_body");
            library.ShellWarn=Boss("shellking_shell_warn"); library.ShellOn=Boss("shellking_shell_on");
            library.MagnetWarn=Boss("magnet_ring_warn"); library.MagnetPull=Boss("magnet_ring_pull");
            library.MimicFlashes=new[]{Boss("mimic_flash_1"),Boss("mimic_flash_2"),Boss("mimic_flash_3")};
            library.WardenSpikeLeftOn=Wall("warden_spike_left_on"); library.WardenSpikeLeftFade=Wall("warden_spike_left_fade");
            library.WardenSpikeRightOn=Wall("warden_spike_right_on"); library.WardenSpikeRightFade=Wall("warden_spike_right_fade");
            library.WardenSpikeTopOn=Wall("warden_spike_top_on"); library.WardenSpikeTopFade=Wall("warden_spike_top_fade");
            library.WardenWallflash=Wall("warden_wallflash"); library.BumperRings=Wall("bumper_rings");
            library.Roles=SkillDefinition.All.Select(s=>new ArtLibrary.RoleArt{Role=s.Role,
                Icon=AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SpritesFolder+"/role_icons/role_"+s.Role.ToLowerInvariant()+".png")}).ToArray();
            library.SpikeWallsThumbnail=AssetDatabase.LoadAssetAtPath<Sprite>(PlaceholderArtGenerator.SpritesFolder+"/arena_thumbs/thumb_spike-walls_v3.png");
        }
    }
}
