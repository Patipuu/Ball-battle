using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    public partial class Night1ViewTests
    {
        [TestCaseSource(typeof(Art2Linker),nameof(Art2Linker.SpritePaths))]
        public void EveryReferencedArt2SpriteLoadsWithPixelImport(string path)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(sprite,Is.Not.Null,path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.filterMode,Is.EqualTo(FilterMode.Point));
            Assert.That(importer.textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed));
            Assert.That(importer.mipmapEnabled,Is.False); Assert.That(sprite.pixelsPerUnit,Is.EqualTo(1));
            foreach(var platform in new[]{"DefaultTexturePlatform","Standalone","WebGL","iPhone","Android","VisionOS","tvOS","WindowsStoreApps"})
                Assert.That(importer.GetPlatformTextureSettings(platform).textureCompression,Is.EqualTo(TextureImporterCompression.Uncompressed),platform);
            var expected=path.Contains("role_icons/")?new Vector2(12,12):path.Contains("magnet_ring_")?new Vector2(112,112):
                path.Contains("warden_spike_top_")?new Vector2(32,6):path.Contains("warden_spike_")?new Vector2(6,32):
                path.Contains("warden_wallflash")?new Vector2(64,8):path.Contains("bumper_rings")?new Vector2(14,14):
                path.Contains("thumb_")?new Vector2(48,48):new Vector2(64,64);
            Assert.That(sprite.rect.size,Is.EqualTo(expected));
            var pivot=path.Contains("warden_spike_left_")?new Vector2(0,.5f):path.Contains("warden_spike_right_")?new Vector2(1,.5f):
                path.Contains("warden_spike_top_")?new Vector2(.5f,1):new Vector2(.5f,.5f);
            Assert.That(sprite.pivot,Is.EqualTo(Vector2.Scale(pivot,expected)));
        }
        [Test]
        public void EveryArt2RoleMapsByRoleFilenameAndEveryLinkedSpriteExists()
        {
            Assert.That(art.Roles.Length,Is.EqualTo(8));
            foreach(var definition in SkillDefinition.All)
            {
                var icon=art.RoleIcon(definition.Role);Assert.That(icon,Is.Not.Null,definition.Role);
                Assert.That(AssetDatabase.GetAssetPath(icon),Does.EndWith("role_"+definition.Role.ToLowerInvariant()+".png"));
                var profile=WeaponProfileView.Create(root.transform,art,Vector2.zero,70);profile.Show(definition.WeaponId);
                Assert.That(profile.transform.Find("RoleIcon").GetComponent<SpriteRenderer>().sprite,Is.SameAs(icon));
                Assert.That(profile.transform.Find("RoleIcon").localScale,Is.EqualTo(Vector3.one));
                profile.gameObject.SetActive(false);
            }
            var linked=new[]{art.MagnetBody,art.WardenBody,art.ShellOn,art.ShellWarn,art.MagnetWarn,art.MagnetPull,
                art.WardenSpikeLeftOn,art.WardenSpikeLeftFade,art.WardenSpikeRightOn,art.WardenSpikeRightFade,
                art.WardenSpikeTopOn,art.WardenSpikeTopFade,art.WardenWallflash,art.BumperRings,art.SpikeWallsThumbnail}
                .Concat(art.MimicFlashes).Concat(art.Roles.Select(r=>r.Icon)).ToArray();
            Assert.That(linked.Length,Is.EqualTo(26));Assert.That(linked.All(s=>s!=null),Is.True);
            Assert.That(linked.Select(AssetDatabase.GetAssetPath).Distinct().Count(),Is.EqualTo(26));
        }
        ArenaView BossFight(string boss,string arenaId=ArenaRegistry.Classic)
        {
            var run=PrepareRun();run.ForcedBossId=boss;run.ForcedBossFight=0;
            var view=root.AddComponent<ArenaView>();view.Art=art;view.AutoRestart=false;view.ArenaId=arenaId;
            view.Boss=RunController.BossLookFor(run);view.StartMatch(run.CreateMatch(ArenaRegistry.Create(arenaId)),"YOU","BOSS");
            return view;
        }
        [TestCase(BossIds.ShellKing)] [TestCase(BossIds.Magnet)] [TestCase(BossIds.Mimic)] [TestCase(BossIds.Warden)]
        public void AllFourBossBodiesAreEnabledAndDrawnAtIntegerScale(string boss)
        {
            Assert.That(RunController.BossArtEnabled,Is.True);
            var view=BossFight(boss);view.Advance(1f/60f);
            var body=view.GetBallView(1);
            Assert.That(body.BodySprite,Is.SameAs(art.BossBody(boss)));
            Assert.That(body.Scale,Is.EqualTo(Mathf.Round(body.Scale)));
        }
        [Test]
        public void WardenArtInnerEdgesFollowCurrentSpikeZoneAndHideAfterWindow()
        {
            var view=BossFight(BossIds.Warden);
            while(view.Sim.ActiveTick<WardenRule.StepTick(1) && view.Sim.Outcome==MatchOutcome.Ongoing) view.Advance(1f/60f);
            Assert.That(view.Sim.BossSpikedWallMask,Is.Not.Zero);
            var strips=view.BossArt.GetComponentsInChildren<SpriteRenderer>().Where(s=>s.enabled && s.sprite.name.StartsWith("warden_spike_")).ToArray();
            Assert.That(strips,Is.Not.Empty);
            foreach(var strip in strips)
            {
                Assert.That(strip.transform.localScale,Is.EqualTo(Vector3.one));
                if(strip.sprite.name.Contains("left"))Assert.That(strip.bounds.max.x,Is.EqualTo(Mathf.Round(view.Sim.Arena.Left.ToFloat())).Within(.001f));
                if(strip.sprite.name.Contains("right"))Assert.That(strip.bounds.min.x,Is.EqualTo(Mathf.Round(view.Sim.Arena.Right.ToFloat())).Within(.001f));
                if(strip.sprite.name.Contains("top"))Assert.That(strip.bounds.min.y,Is.EqualTo(Mathf.Round(view.Sim.Arena.Top.ToFloat())).Within(.001f));
            }
            while(view.Sim.ActiveTick<WardenRule.StepTick(1)+BossTuning.SpikeWindowTicks && view.Sim.Outcome==MatchOutcome.Ongoing) view.Advance(1f/60f);
            Assert.That(view.Sim.BossSpikedWallMask,Is.Zero);Assert.That(view.BossArt.StripsShown,Is.Zero);
        }
        [Test]
        public void ArenaSelectorLoadsV3SpikeWallsThumbnailAtNativeScale()
        {
            var menu=MenuView.Create(root.transform,art);menu.ShowVersus();menu.SelectArena(ArenaRegistry.SpikeWalls);
            var thumbnail=menu.GetComponentsInChildren<SpriteRenderer>().Single(s=>s.name=="ArenaThumbnail");
            Assert.That(thumbnail.sprite,Is.SameAs(art.SpikeWallsThumbnail));Assert.That(thumbnail.enabled,Is.True);
            Assert.That(thumbnail.transform.localScale,Is.EqualTo(Vector3.one));
        }
        void ShootBoth(Camera camera,string name)
        {
            Shoot(camera,Path.Combine(Out,"phaseD-"+name+"-1x.png"));
            Shoot(camera,Path.Combine(Out,"phaseD-"+name+"-3x.png"),3);
        }
        [Test]
        public void CaptureArt2BossesSpikesBumpersRolesAndArenaSelectorAtOneAndThree()
        {
            if(!Phase("D"))Assert.Ignore("Phase D capture only");
            var camera=CaptureCamera();
            foreach(var boss in BossIds.All)
            {
                var view=BossFight(boss);
                var target=boss==BossIds.Warden?WardenRule.StepTick(1)+8:boss==BossIds.ShellKing?BossTuning.ShellWarnTicks+120:320;
                while(view.Sim.ActiveTick<target && view.Sim.Outcome==MatchOutcome.Ongoing)view.Advance(1f/60f);
                ShootBoth(camera,boss+"-fight");
                if(boss==BossIds.Warden)
                {
                    Assert.That(view.Sim.BossSpikedWallMask,Is.Not.Zero);ShootBoth(camera,"warden-spike-window");
                }
                view.gameObject.SetActive(false);
                // root.AddComponent<ArenaView> shares the root; reactivate it while hiding only this encounter's children.
                root.SetActive(true);foreach(Transform child in root.transform)if(child!=camera.transform)child.gameObject.SetActive(false);
            }
            var bumperGo=new GameObject("BumperCapture");bumperGo.transform.SetParent(root.transform);
            var bumpers=bumperGo.AddComponent<ArenaView>();bumpers.Art=art;bumpers.AutoRestart=false;bumpers.ArenaId=ArenaRegistry.Bumpers;
            bumpers.StartMatch("blade","brawler",92);bumpers.Advance(1f/60f);ShootBoth(camera,"bumpers");bumperGo.SetActive(false);
            var pick=PickWeaponView.Create(root.transform,art);pick.Show(new[]{"blade","venom","aegis"});ShootBoth(camera,"pick-role-icons");pick.Hide();
            var menu=MenuView.Create(root.transform,art);menu.ShowVersus();menu.SelectArena(ArenaRegistry.SpikeWalls);ShootBoth(camera,"arena-spike-walls");
        }
    }
}
