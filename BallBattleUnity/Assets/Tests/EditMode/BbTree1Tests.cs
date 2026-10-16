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
    /// <summary>BB-12g: SKILL TREE page (select, ADD, RESET, saved at once, branch lock) and its place in BALLS.</summary>
    public class BbTree1Tests
    {
        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbTree1Root");
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

        static BallProgress Saved(MemorySaveStore s, string id = "blade") => MetaRules.Ball(s.Load().Progress, id);

        void Buy(TreeView v, TreeView.Box b)
        {
            v.BoxButton(b).SimulateClick();
            Assert.That(v.CanAddSelected, Is.True, b.ToString());
            v.AddButton.SimulateClick();
        }

        [Test]
        public void LevelOneSpendsItsOnePointAndItIsSaved()
        {
            var store = Store(1);
            var v = TreeView.Create(root.transform, art);
            v.Show(store, "blade");
            Assert.That(v.PointsText, Does.Contain("POINTS 1 FREE OF 1"));
            v.BoxButton(TreeView.Box.Opener).SimulateClick();
            Assert.That(v.CanAddSelected, Is.False);
            Assert.That(v.InfoState, Does.Contain("INSTINCT"));
            Buy(v, TreeView.Box.Power);
            Assert.That(Saved(store).Pow, Is.EqualTo(1));
            Assert.That(v.BoxLabel(TreeView.Box.Power), Does.EndWith("1/2"));
            v.BoxButton(TreeView.Box.Vitality).SimulateClick();
            Assert.That(v.CanAddSelected, Is.False, "no point left");
            v.AddButton.SimulateClick();
            Assert.That(Saved(store).Vit, Is.Zero);
        }

        [Test]
        public void ABranchPathToTheFinaleLocksTheOtherBranchUntilReset()
        {
            var store = Store(10);
            var v = TreeView.Create(root.transform, art);
            v.Show(store, "blade");
            Buy(v, TreeView.Box.Vitality);
            Buy(v, TreeView.Box.Instinct);
            Buy(v, TreeView.Box.A2);
            Buy(v, TreeView.Box.Opener);
            Buy(v, TreeView.Box.A3);
            Buy(v, TreeView.Box.FinA);
            var t = Saved(store);
            Assert.That((t.SkillUps, t.Branch, t.Finale, MetaTree.Spent(t)), Is.EqualTo((2, 1, true, 9)));
            v.BoxButton(TreeView.Box.B2).SimulateClick();
            Assert.That(v.CanAddSelected, Is.False);
            Assert.That(v.InfoState, Does.Contain("OTHER BRANCH"));
            Assert.That(v.PointsText, Does.Contain("POINTS 1 FREE OF 10"));
            v.ResetButton.SimulateClick();
            Assert.That(MetaTree.Spent(Saved(store)), Is.Zero);
            Assert.That(v.PointsText, Does.Contain("POINTS 10 FREE OF 10"));
        }

        [Test]
        public void TheTreeOpensFromTheBallDetailAndBackReturnsThere()
        {
            var store = Store(3);
            var balls = BallsView.Create(root.transform, art);
            balls.Show(store);
            balls.OpenDetail("blade");
            balls.TreeButton.SimulateClick();
            Assert.That(balls.Tree.IsOpen, Is.True);
            balls.Tree.transform.Find("TreeBack").GetComponent<PixelButton>().SimulateClick();
            Assert.That((balls.Tree.IsOpen, balls.DetailOpen), Is.EqualTo((false, true)));
        }

        [Test]
        public void BoxesFitTheScreenAndTheirTextFitsTheBoxes()
        {
            foreach (var id in new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" })
            {
                var v = TreeView.Create(root.transform, art);
                v.Show(Store(10, id), id);
                foreach (TreeView.Box b in Enum.GetValues(typeof(TreeView.Box)))
                {
                    var a = TreeView.BoxArea(b);
                    Assert.That(a.xMin >= -135 && a.xMax <= 135, Is.True);
                    foreach (TreeView.Box o in Enum.GetValues(typeof(TreeView.Box)))
                        if (o != b) Assert.That(a.Overlaps(TreeView.BoxArea(o)), Is.False);
                    foreach (var t in v.BoxButton(b).GetComponentsInChildren<PixelText>().Where(t => t.Text.Length > 0))
                        Assert.That(t.Bounds.xMin >= a.xMin + 1 && t.Bounds.xMax <= a.xMax - 1, Is.True, $"{id} {b}: '{t.Text}' {t.Bounds} in {a}");
                }
                foreach (TreeView.Box b in Enum.GetValues(typeof(TreeView.Box)))
                {
                    v.Select(b);
                    foreach (var t in v.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Length > 0))
                        Assert.That(t.Bounds.xMin >= -135 && t.Bounds.xMax <= 135, Is.True, $"{id}: '{t.Text}' {t.Bounds}");
                }
                Object.DestroyImmediate(v.gameObject);
            }
        }

        [Test]
        public void ARunStartedAfterSpendingUsesTheTree()
        {
            var store = new MemorySaveStore();
            var p = new ProgressData();
            var id = RunState.StartChoices(9, UnlockRules.Weapons(p))[0];
            var t = MetaRules.Ball(p, id);
            t.Level = 4;
            MetaTree.Add(t, TreeNode.Vitality); MetaTree.Add(t, TreeNode.Instinct); MetaTree.Add(t, TreeNode.SkillA);
            store.Save(new SaveData { Progress = p });
            var flow = new RunFlow(store);
            flow.Open();
            flow.NewRun(9);
            flow.ChooseWeapon(id);
            Assert.That((flow.Run.Build.SkillLevel, flow.Run.Build.TreeOn, flow.Run.Build.TreeGrowth), Is.EqualTo((2, true, 1)));
            var reloaded = store.Load().Run.ToRun();
            Assert.That((reloaded.Build.TreeOn, reloaded.Build.TreeGrowth, reloaded.Build.SkillLevel), Is.EqualTo((true, 1, 2)), "the tree is saved with the run");
        }

        [Test]
        public void CaptureTreeScreens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_TREE1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_TREE1_OUT not set");
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
                var balls = BallsView.Create(root.transform, art);
                balls.Show(store);
                balls.OpenDetail("venom");
                Shoot("1-detail-tree-button");
                balls.TreeButton.SimulateClick();
                var v = balls.Tree;
                Shoot("2-tree-empty");
                Buy(v, TreeView.Box.Power);
                Buy(v, TreeView.Box.Instinct);
                Buy(v, TreeView.Box.B2);
                Buy(v, TreeView.Box.Opener);
                Shoot("3-tree-middle");
                Buy(v, TreeView.Box.B3);
                Buy(v, TreeView.Box.FinB);
                v.Select(TreeView.Box.FinB);
                Shoot("4-tree-finale-b");
                v.Select(TreeView.Box.A2);
                Shoot("5-other-branch-locked");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
