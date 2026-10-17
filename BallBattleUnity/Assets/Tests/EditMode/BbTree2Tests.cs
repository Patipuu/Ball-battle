using System;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>BB-12h/12i: per-ball icon nodes, tap-to-open node card with UPGRADE and CLOSE, and the card fitting on screen.</summary>
    public class BbTree2Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbTree2Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static MemorySaveStore Store(int level, string id = "blade")
        {
            var p = new ProgressData();
            MetaRules.Ball(p, id).Level = level;
            var s = new MemorySaveStore();
            s.Save(new SaveData { Progress = p });
            return s;
        }

        [Test]
        public void EveryBallHasItsOwnIcons()
        {
            var ids = new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };
            var seen = new System.Collections.Generic.HashSet<Sprite>();
            foreach (var id in ids)
            {
                var v = TreeView.Create(root.transform, art);
                v.Show(Store(3, id), id);
                Assert.That(v.BoxIcon(TreeView.Box.Vitality), Is.SameAs(TreeIcons.Get(TreeIcons.Kind.Heart)), "VITALITY is shared");
                Assert.That(v.BoxIcon(TreeView.Box.Power), Is.SameAs(TreeIcons.Get(TreeIcons.Kind.Sword)), "POWER is shared");
                foreach (var b in new[] { TreeView.Box.Instinct, TreeView.Box.Opener, TreeView.Box.A2, TreeView.Box.B2, TreeView.Box.FinA, TreeView.Box.FinB })
                {
                    var key = TreeIcons.BallKey(id, TreeView.NodeOf(b), b == TreeView.Box.B2 || b == TreeView.Box.FinB ? 'B' : 'A');
                    Assert.That(TreeIconArt.Rows.ContainsKey(key), Is.True, key);
                    Assert.That(seen.Add(v.BoxIcon(b)), Is.True, $"{key}: its own icon, not shared with another node or ball");
                }
                Assert.That(v.BoxIcon(TreeView.Box.A3), Is.SameAs(v.BoxIcon(TreeView.Box.A2)), "LV2 and LV3 of a branch share the icon (the corner number differs)");
                Object.DestroyImmediate(v.gameObject);
            }
            foreach (var rows in TreeIcons.AllRows.Concat(TreeIconArt.Rows.Values))
            {
                Assert.That(rows.Length, Is.LessThanOrEqualTo(TreeIcons.Size));
                Assert.That(rows.All(r => r.Length == TreeIcons.Size && r.All(c => ".oWH".IndexOf(c) >= 0)), Is.True);
            }
        }

        [Test]
        public void TappingOpensTheCardAndItsUpgradeBuysTheNode()
        {
            var store = Store(2);
            var v = TreeView.Create(root.transform, art);
            v.Show(store, "blade");
            Assert.That(v.CardOpen, Is.False);
            Assert.That(v.CaptionText, Does.Contain("VITALITY"));
            PixelInput.SetTestClock(0f);
            try
            {
                PixelInput.InjectTap(TreeView.BoxArea(TreeView.Box.Power).center);
                PixelInput.Dispatch(() => v.GetComponentsInChildren<PixelButton>(false));
            }
            finally { PixelInput.SetTestClock(null); }
            Assert.That(v.CardOpen, Is.True);
            Assert.That(v.CardName, Is.EqualTo("POWER"));
            Assert.That(v.InfoState, Does.StartWith("READY"));
            Assert.That(v.UpgradeButton.LabelText, Is.EqualTo("UPGRADE"));
            v.UpgradeButton.SimulateClick();
            Assert.That(MetaRules.Ball(store.Load().Progress, "blade").Pow, Is.EqualTo(1), "saved at once");
            Assert.That(v.CardOpen, Is.True, "the card stays open to show the new rank");
            v.UpgradeButton.SimulateClick();
            Assert.That(MetaRules.Ball(store.Load().Progress, "blade").Pow, Is.EqualTo(2));
            Assert.That(v.UpgradeButton.LabelText, Is.EqualTo("OWNED"));
            v.CloseButton.SimulateClick();
            Assert.That(v.CardOpen, Is.False);
        }

        [Test]
        public void ALockedNodeCardSaysWhyAndUpgradeDoesNothing()
        {
            var store = Store(1);
            var v = TreeView.Create(root.transform, art);
            v.Show(store, "blade");
            v.OpenCard(TreeView.Box.FinA);
            Assert.That(v.InfoState, Does.Contain("LV3"));
            v.UpgradeButton.SimulateClick();
            Assert.That(MetaTree.Spent(MetaRules.Ball(store.Load().Progress, "blade")), Is.Zero);
        }

        [Test]
        public void TheCardFitsOnScreenForEveryNodeOfEveryBall()
        {
            foreach (var id in new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" })
            {
                var v = TreeView.Create(root.transform, art);
                var store = Store(10, id);
                v.Show(store, id);
                var t = MetaRules.Ball(store.Load().Progress, id);
                MetaTree.Add(t, TreeNode.Vitality);
                foreach (TreeView.Box b in Enum.GetValues(typeof(TreeView.Box)))
                {
                    v.OpenCard(b);
                    var card = TreeView.CardArea;
                    foreach (var txt in v.transform.Find("Card").GetComponentsInChildren<PixelText>(false).Where(x => x.Text.Length > 0))
                        if (txt.transform.parent.name == "Card")
                            Assert.That(txt.Bounds.xMin >= card.xMin + 2 && txt.Bounds.xMax <= card.xMax - 2 && txt.Bounds.yMin >= card.yMin && txt.Bounds.yMax <= card.yMax,
                                Is.True, $"{id} {b}: '{txt.Text}' {txt.Bounds}");
                    Assert.That(TreeView.UpgradeArea.Overlaps(TreeView.CloseArea), Is.False);
                    v.CloseCard();
                }
                Object.DestroyImmediate(v.gameObject);
            }
        }

        [Test]
        public void CaptureIconTree()
        {
            var dir = Environment.GetEnvironmentVariable("BB_TREE2_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_TREE2_OUT not set");
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
                var store = Store(10, "venom");
                var v = TreeView.Create(root.transform, art);
                v.Show(store, "venom");
                Shoot("1-icons-empty");
                foreach (var b in new[] { TreeView.Box.Power, TreeView.Box.Instinct, TreeView.Box.B2, TreeView.Box.Opener })
                {
                    v.Select(b);
                    v.AddButton.SimulateClick();
                }
                v.Select(TreeView.Box.B3);
                Shoot("2-icons-middle");
                v.OpenCard(TreeView.Box.B3);
                Shoot("3-card-ready");
                v.UpgradeButton.SimulateClick();
                Shoot("4-card-owned");
                v.CloseCard();
                v.OpenCard(TreeView.Box.A2);
                Shoot("5-card-locked");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
