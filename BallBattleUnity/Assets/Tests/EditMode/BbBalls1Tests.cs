using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>BB-12c: BALLS page (tiles, detail, UPGRADE saved at once) and the BALLS button on the menu hub.</summary>
    public class BbBalls1Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbBalls1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static MemorySaveStore StoreWith(Action<ProgressData> setup)
        {
            var p = new ProgressData();
            setup?.Invoke(p);
            var s = new MemorySaveStore();
            s.Save(new SaveData { Progress = p });
            return s;
        }

        [Test]
        public void EveryBallHasATileLockedOnesAreDimmedAndOff()
        {
            var v = BallsView.Create(root.transform, art);
            v.Show(StoreWith(null));
            Assert.That(v.TileCount, Is.EqualTo(WeaponRegistry.All.Count));
            var unlocked = UnlockRules.Weapons(new ProgressData());
            foreach (var e in WeaponRegistry.All)
            {
                var open = unlocked.Contains(e.Id);
                Assert.That(v.TileButton(e.Id).enabled, Is.EqualTo(open), e.Id);
                Assert.That(v.TileText(e.Id), open ? Does.Contain("LV 1") : Does.Contain("LOCKED"), e.Id);
            }
            Assert.That(v.GoldText, Is.EqualTo("GOLD 0"));
        }

        [Test]
        public void TilesShowShardsAndUpWhenALevelIsAffordable()
        {
            var v = BallsView.Create(root.transform, art);
            v.Show(StoreWith(p => { MetaRules.Ball(p, "blade").Shards = 3; MetaRules.Ball(p, "fang").Shards = 6; p.Gold = 10; }));
            Assert.That(v.TileText("blade"), Does.EndWith("3/5"));
            Assert.That(v.TileText("fang"), Does.EndWith("UP!"));
        }

        [Test]
        public void UpgradeSpendsSavesAndRefreshes()
        {
            var store = StoreWith(p => { MetaRules.Ball(p, "pike").Shards = 7; p.Gold = 15; });
            var v = BallsView.Create(root.transform, art);
            v.Show(store);
            v.TileButton("pike").SimulateClick();
            Assert.That((v.DetailOpen, v.DetailId), Is.EqualTo((true, "pike")));
            Assert.That(v.DetailLevelText, Is.EqualTo("LEVEL 1 / 10"));
            Assert.That(v.UpgradeReady, Is.True);
            v.UpgradeButton.SimulateClick();
            var saved = store.Load().Progress;
            Assert.That((MetaRules.LevelOf(saved, "pike"), MetaRules.Ball(saved, "pike").Shards, saved.Gold), Is.EqualTo((2, 2, 5)));
            Assert.That(v.DetailLevelText, Is.EqualTo("LEVEL 2 / 10"));
            Assert.That(v.FlashShown, Is.True);
            Assert.That(v.UpgradeReady, Is.False, "LV2 -> 3 needs 10 shards");
            v.UpgradeButton.SimulateClick();
            Assert.That(MetaRules.LevelOf(store.Load().Progress, "pike"), Is.EqualTo(2), "nothing happens when it cannot");
            Assert.That(v.DetailCostText, Is.EqualTo("NEXT LEVEL: 10 SHARDS  20 GOLD"));
        }

        [Test]
        public void MaxedBallHidesUpgrade()
        {
            var v = BallsView.Create(root.transform, art);
            v.Show(StoreWith(p => MetaRules.Ball(p, "blade").Level = MetaTuning.MaxLevel));
            v.OpenDetail("blade");
            Assert.That(v.UpgradeButton.gameObject.activeSelf, Is.False);
            Assert.That(v.TileText("blade"), Does.EndWith("MAX"));
        }

        [Test]
        public void EverythingStaysOnScreenAndTilesDoNotOverlap()
        {
            var v = BallsView.Create(root.transform, art);
            v.Show(StoreWith(p => { MetaRules.Ball(p, "volley").Shards = 99; p.Achievements.Add(UnlockRules.Win3); }));
            for (var i = 0; i < WeaponRegistry.All.Count; i++)
            {
                var a = BallsView.TileArea(i);
                Assert.That(a.xMin >= -135 && a.xMax <= 135 && a.yMin >= -240 && a.yMax <= 240, Is.True, a.ToString());
                for (var j = i + 1; j < WeaponRegistry.All.Count; j++) Assert.That(a.Overlaps(BallsView.TileArea(j)), Is.False);
                Assert.That(a.Overlaps(BallsView.BackArea), Is.False);
            }
            void OnScreen()
            {
                foreach (var t in v.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Length > 0))
                {
                    var b = t.Bounds;
                    Assert.That(b.xMin >= -135 && b.xMax <= 135 && b.yMin >= -240 && b.yMax <= 240, Is.True, $"'{t.Text}' {b}");
                }
            }
            OnScreen();
            v.OpenDetail("volley");
            OnScreen();
        }

        [Test]
        public void MenuHasBallsWithAnUpBadgeAndNothingOverlaps()
        {
            var m = MenuView.Create(root.transform, art);
            m.Store = StoreWith(p => { MetaRules.Ball(p, "blade").Shards = 5; p.Gold = 10; });
            m.SetRunInfo(new ProgressData(), null);
            Assert.That(m.BallsBadgeShown, Is.False);
            m.SetRunInfo(m.Store.Load().Progress, null);
            Assert.That(m.BallsBadgeShown, Is.True);
            var areas = new[] { MenuView.RunArea, MenuView.BallsArea, MenuView.VersusArea, MenuView.CodexArea, MenuView.HowToArea, MenuView.ChestArea };
            for (var i = 0; i < areas.Length; i++)
            {
                Assert.That(areas[i].yMin, Is.GreaterThanOrEqualTo(-240f));
                for (var j = i + 1; j < areas.Length; j++) Assert.That(areas[i].Overlaps(areas[j]), Is.False, $"{areas[i]} {areas[j]}");
            }
            foreach (var t in m.Hub.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Length > 0))
                foreach (var a in areas.Skip(1))
                {
                    var owner = m.Hub.GetComponentsInChildren<PixelButton>(true).FirstOrDefault(b => b.Area == a);
                    if (owner != null && t.transform.IsChildOf(owner.transform)) continue;
                    Assert.That(t.Bounds.Overlaps(a), Is.False, $"'{t.Text}' over {a}");
                }
            m.BallsButton.SimulateClick();
            Assert.That((m.Balls.IsOpen, m.HubVisible), Is.EqualTo((true, false)));
            m.ShowHub();
            Assert.That((m.Balls.IsOpen, m.HubVisible), Is.EqualTo((false, true)));
        }

        [Test]
        public void CaptureBallsScreens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_BALLS1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_BALLS1_OUT not set");
            Directory.CreateDirectory(dir);
            var camGo = new GameObject("Cam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true; cam.orthographicSize = 240f; cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background; camGo.transform.position = new Vector3(0, 0, -10);
                void Shoot(string name)
                {
                    foreach (var zoom in new[] { 1, 3 })
                    {
                        var rt = new RenderTexture(270 * zoom, 480 * zoom, 24) { filterMode = FilterMode.Point };
                        cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
                        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
                        File.WriteAllBytes(Path.Combine(dir, $"{name}-{zoom}x.png"), tex.EncodeToPNG());
                        RenderTexture.active = null; cam.targetTexture = null;
                        Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
                    }
                }
                var store = StoreWith(p =>
                {
                    p.Achievements.Add(UnlockRules.Boss2);
                    MetaRules.Ball(p, "blade").Level = 4; MetaRules.Ball(p, "blade").Shards = 27;
                    MetaRules.Ball(p, "fang").Shards = 3;
                    MetaRules.Ball(p, "pike").Level = 10;
                    MetaRules.Ball(p, "volley").Shards = 6;
                    p.Gold = 160;
                });
                var m = MenuView.Create(root.transform, art);
                m.Store = store;
                m.SetRunInfo(store.Load().Progress, null);
                Shoot("1-menu-balls-button");
                m.ShowBalls();
                Shoot("2-balls-grid");
                m.Balls.OpenDetail("blade");
                Shoot("3-detail-ready");
                m.Balls.PressUpgrade();
                Shoot("4-detail-level-up");
                m.Balls.OpenDetail("fang");
                Shoot("5-detail-need-shards");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
