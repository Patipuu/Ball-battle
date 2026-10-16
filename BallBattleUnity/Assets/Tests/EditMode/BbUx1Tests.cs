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
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>UI-UX1: weapon pick explains each ball before picking (select, details, PICK); Codex shows the same guide; V3 icons.</summary>
    public class BbUx1Tests
    {
        GameObject root;
        ArtLibrary art;
        static readonly string[] All = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            root = new GameObject("BbUx1Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        [Test]
        public void EveryBallHasAGuide()
        {
            foreach (var id in All)
            {
                var g = WeaponGuide.For(id);
                Assert.That(g, Is.Not.Null, id);
                Assert.That(new[] { g.Grows, g.Good, g.Weak }.All(s => !string.IsNullOrWhiteSpace(s)), Is.True, id);
            }
            Assert.That(WeaponGuide.For("nope"), Is.Null);
        }

        [Test]
        public void FirstTapExplainsSecondTapOrPickStartsTheRun()
        {
            var view = PickWeaponView.Create(root.transform, art);
            string picked = null;
            view.WeaponPicked += id => picked = id;
            view.Show(new[] { "blade", "venom", "aegis" });
            Assert.That(view.Selected, Is.EqualTo("blade"), "UI-CLEAN1: the first ball is explained from the start");
            Assert.That(picked, Is.Null, "showing it never picks it");
            view.GetComponentsInChildren<PixelButton>().Single(b => b.name == "Pick_venom").SimulateClick();
            Assert.That(picked, Is.Null, "first tap only explains");
            Assert.That(view.Selected, Is.EqualTo("venom"));
            Assert.That(view.DetailText, Does.Contain("DRAIN").And.Contain("POISONER").And.Contain("WEAK:"));
            Assert.That(view.PickButton.gameObject.activeSelf, Is.True);
            view.GetComponentsInChildren<PixelButton>().Single(b => b.name == "Pick_aegis").SimulateClick();
            Assert.That((picked, view.Selected), Is.EqualTo(((string)null, "aegis")), "tapping another ball switches the details");
            view.PickButton.SimulateClick();
            Assert.That(picked, Is.EqualTo("aegis"), "PICK starts with the selected ball");
            picked = null;
            view.GetComponentsInChildren<PixelButton>().Single(b => b.name == "Pick_aegis").SimulateClick();
            Assert.That(picked, Is.EqualTo("aegis"), "a second tap on the selected ball picks it too");
        }

        [Test]
        public void DetailsFitForEveryBall()
        {
            var view = PickWeaponView.Create(root.transform, art);
            for (var i = 0; i < All.Length; i += 3)
            {
                var choices = All.Skip(i).Take(3).ToArray();
                view.Show(choices);
                foreach (var id in choices)
                {
                    view.Select(id);
                    Bb0102Tests.AssertTextFitsAndNoOverlap(view.transform);
                    foreach (var t in view.GetComponentsInChildren<PixelText>().Where(t => t.Text.Length > 0 && t.WorldBounds.yMax <= PickWeaponView.PanelArea.yMax))
                    {
                        Assert.That(t.WorldBounds.xMin, Is.GreaterThanOrEqualTo(PickWeaponView.PanelArea.xMin + 1), $"{id}: '{t.Text}'");
                        Assert.That(t.WorldBounds.xMax, Is.LessThanOrEqualTo(PickWeaponView.PanelArea.xMax - 1), $"{id}: '{t.Text}'");
                    }
                    Assert.That(view.DetailText, Does.Contain(SkillDefinition.ForWeapon(id).Name), id);
                }
            }
        }

        [Test]
        public void CodexShowsTheGuideAndFits()
        {
            var codex = SkillCodexView.Create(root.transform, art);
            codex.Show();
            var next = codex.GetComponentsInChildren<PixelButton>().Single(b => b.LabelText == "NEXT");
            for (var i = 0; i < 8; i++)
            {
                var texts = codex.GetComponentsInChildren<PixelText>().Select(t => t.Text).ToList();
                Assert.That(texts.Any(t => t.StartsWith("HOW: ")) && texts.Any(t => t.StartsWith("GOOD: ")) && texts.Any(t => t.StartsWith("WEAK: ")), Is.True, $"page {i}");
                Bb0102Tests.AssertTextFitsAndNoOverlap(codex.transform);
                next.SimulateClick();
            }
        }

        [TestCase(BossIds.ShellKing)]
        [TestCase(BossIds.Magnet)]
        [TestCase(BossIds.Warden)]
        public void BossPortraitClearsTheLabelTextAndBorder(string boss)
        {
            // BB-QA bc3c460 finding 1: the 64x64 portrait overlapped the boss name label and the ENEMY box's top-left border.
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            run.FightIndex = RunTuning.BossFights[BossIds.SlotOf(boss)];
            run.BossIds[BossIds.SlotOf(boss)] = boss;
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That((v.EnemyIconX, v.EnemyIconY), Is.EqualTo((PrepareView.BossIconPos.x, PrepareView.BossIconPos.y)));
            Assert.That(v.EnemyNameX, Is.EqualTo(PrepareView.BossTextX));
            Assert.That(v.EnemySkillIcon.enabled, Is.False, "bosses other than MIMIC show no skill line, so no icon");
            // Opaque pixels of the sprite file (pivot centre, 1 px per unit, scale 1 for the radius-24 boss).
            var sprite = art.BossBody(boss);
            var tex = new Texture2D(2, 2);
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(sprite.texture)));
            for (var y = 0; y < tex.height; y++)
                for (var x = 0; x < tex.width; x++)
                {
                    if (tex.GetPixel(x, y).a == 0) continue;
                    var wx = v.EnemyIconX + x - tex.width / 2f; var wy = v.EnemyIconY + y - tex.height / 2f;   // pixel's lower-left corner
                    Assert.That(wx, Is.GreaterThanOrEqualTo(-124f), $"{boss} px {x},{y} on the box border");
                    Assert.That(wx + 1, Is.LessThanOrEqualTo(PrepareView.BossTextX - 1), $"{boss} px {x},{y} touches the text column");
                    Assert.That(wy + 1, Is.LessThanOrEqualTo(180f), $"{boss} px {x},{y} under the name label");
                    Assert.That(wy, Is.GreaterThanOrEqualTo(114f), $"{boss} px {x},{y} below the box");
                }
            Bb0102Tests.AssertTextFitsAndNoOverlap(v.transform);
        }

        [Test]
        public void NormalEnemyKeepsTheOldColumn()
        {
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That((v.EnemyIconX, v.EnemyIconY, v.EnemyNameX), Is.EqualTo((PrepareView.EnemyIconPos.x, PrepareView.EnemyIconPos.y, PrepareView.EnemyTextX)));
        }

        [Test]
        public void EveryBallHasA14pxSkillIcon()
        {
            foreach (var s in SkillDefinition.All)
            {
                var sp = art.SkillIcon(s.WeaponId);
                Assert.That(sp, Is.Not.Null, s.WeaponId);
                Assert.That((sp.rect.width, sp.rect.height), Is.EqualTo((14f, 14f)), s.WeaponId);
            }
            Assert.That(art.Skills.Select(t => t.Icon).Distinct().Count(), Is.EqualTo(SkillDefinition.All.Length), "one icon per ball");
        }

        [Test]
        public void SkillUpCardShowsTheBallsSkillIcon()
        {
            foreach (var s in SkillDefinition.All)
                Assert.That(CardFace.For(new Card(CardKind.SkillUp, s.WeaponId, 2), art).Icon, Is.SameAs(art.SkillIcon(s.WeaponId)), s.WeaponId);
            Assert.That(CardFace.For(new Card(CardKind.SwapWeapon, "blade"), art).Icon, Is.SameAs(art.CardSwap), "SWAP keeps its own icon");
        }

        [Test]
        public void PickTilesAndDetailsShowTheSkillIcon()
        {
            var view = PickWeaponView.Create(root.transform, art);
            var ids = new[] { "blade", "venom", "aegis" };
            view.Show(ids);
            foreach (var id in ids)
            {
                var tile = view.GetComponentsInChildren<PixelButton>().Single(b => b.name == "Pick_" + id);
                var icon = tile.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "SkillIcon");
                Assert.That(icon.sprite, Is.SameAs(art.SkillIcon(id)), id);
            }
            Assert.That(view.SkillIcon.enabled, Is.True, "UI-CLEAN1: the first ball is explained at once, with its skill icon");
            foreach (var id in ids)
            {
                view.Select(id);
                Assert.That(view.SkillIcon.enabled, Is.True, id);
                Assert.That(view.SkillIcon.sprite, Is.SameAs(art.SkillIcon(id)), id);
                Bb0102Tests.AssertTextFitsAndNoOverlap(view.transform);
            }
        }

        [Test]
        public void PrepareShowsSkillIconsBesideTheSkillLines()
        {
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.YouSkillIcon.enabled, Is.True);
            Assert.That(v.YouSkillIcon.sprite, Is.SameAs(art.SkillIcon(run.Build.WeaponId)));
            Assert.That(v.YouSkillTextX, Is.EqualTo(PrepareView.EnemyTextX + PrepareView.SkillIconGap));
            Assert.That(v.EnemySkillIcon.enabled, Is.True);
            Assert.That(v.EnemySkillIcon.sprite, Is.SameAs(art.SkillIcon(run.Enemy.WeaponId)));
            Bb0102Tests.AssertTextFitsAndNoOverlap(v.transform);
        }

        [Test]
        public void CodexSkillIconSitsLeftOfTheSkillName()
        {
            var codex = SkillCodexView.Create(root.transform, art);
            codex.Show();
            var next = codex.GetComponentsInChildren<PixelButton>().Single(b => b.LabelText == "NEXT");
            for (var i = 0; i < 8; i++)
            {
                Assert.That(codex.SkillIcon.enabled, Is.True, $"page {i}");
                var name = codex.GetComponentsInChildren<PixelText>().Single(t => t.TextScale == 2 && t.transform.localPosition.y == 34f);
                var iconRight = codex.SkillIcon.transform.localPosition.x + 14f;   // 14 px sprite at x2
                Assert.That(iconRight, Is.LessThanOrEqualTo(name.Bounds.xMin - 6f), $"page {i}: icon touches '{name.Text}'");
                Assert.That(codex.SkillIcon.transform.localPosition.x - 14f, Is.GreaterThanOrEqualTo(-135f), $"page {i}: icon off screen");
                next.SimulateClick();
            }
        }

        [Test]
        public void CardAndTraitIconsAreTheV3Set()
        {
            // UI-UX1: Assets/Art/Sprites/icons now hold the approved V3 icons (E:\ball-battle-art-hold\icons_v3). Every card/trait icon is
            // a 14x14 sprite; a quick fingerprint of one V3-only feature: V3 icons use a full-bleed 2-tone tile, so the corner pixel is opaque.
            foreach (var path in Directory.GetFiles("Assets/Art/Sprites/icons", "*.png"))
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
                Assert.That(tex, Is.Not.Null, path);
                Assert.That((tex.width, tex.height), Is.EqualTo((14, 14)), path);
            }
        }

        /// <summary>BB_UX1_OUT: weapon pick (nothing selected / SPORE selected), Codex page, Prepare with V3 icons, at 1x and 3x.</summary>
        [Test]
        public void CaptureUx1Screens()
        {
            var dir = Environment.GetEnvironmentVariable("BB_UX1_OUT");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB_UX1_OUT not set");
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
                var pick = PickWeaponView.Create(root.transform, art);
                pick.Show(new[] { "blade", "venom", "aegis" });
                Shoot("1-pick-weapon-none");
                pick.Select("venom");
                Shoot("2-pick-weapon-spore");
                pick.Select("aegis");
                Shoot("3-pick-weapon-mirror");
                pick.Hide();
                var codex = SkillCodexView.Create(root.transform, art);
                codex.Show();
                Shoot("4-codex-ember");
                codex.Hide();
                var p = new ProgressData();
                var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
                run.Pick(0);
                run.OfferCards[0] = OfferSlot.From(new Card(CardKind.Trait, "vampire", 1));
                run.OfferCards[1] = OfferSlot.From(new Card(CardKind.DamageUp));
                run.OfferCards[2] = OfferSlot.From(new Card(CardKind.Trait, "second-wind", 1));
                var prep = PrepareView.Create(root.transform, art);
                prep.Show(run);
                Shoot("5-prepare-v3-icons");
                run.OfferCards[2] = OfferSlot.From(new Card(CardKind.SkillUp, run.Build.WeaponId, 2));
                prep.Show(run);
                Shoot("8-prepare-skillup-icon");
                prep.Hide();
                var codex2 = SkillCodexView.Create(root.transform, art);
                codex2.Show();
                var next2 = codex2.GetComponentsInChildren<PixelButton>().Single(b => b.LabelText == "NEXT");
                for (var page = 0; page < 8; page++)
                {
                    if (page == 5 || page == 7) Shoot("9-codex-page" + page);
                    next2.SimulateClick();
                }
                codex2.Hide();
                foreach (var boss in new[] { BossIds.Magnet, BossIds.Warden, BossIds.ShellKing })
                {
                    var br = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
                    br.Pick(0);
                    br.FightIndex = RunTuning.BossFights[BossIds.SlotOf(boss)];
                    br.BossIds[BossIds.SlotOf(boss)] = boss;
                    var bp = PrepareView.Create(root.transform, art);
                    bp.Show(br);
                    Shoot("7-prepare-boss-" + boss);
                    Object.DestroyImmediate(bp.gameObject);
                }
                var sp = StartPickView.Create(root.transform, art);
                var s2 = RunState.Start(23, RunState.StartChoices(23, UnlockRules.Weapons(p))[0], p);
                s2.OfferCards[0] = OfferSlot.From(new Card(CardKind.SpeedUp));
                s2.OfferCards[1] = OfferSlot.From(new Card(CardKind.Trait, "thorns", 1));
                s2.OfferCards[2] = OfferSlot.From(new Card(CardKind.MaxHp));
                sp.Show(s2);
                Shoot("6-startpick-v3-icons");
            }
            finally { Object.DestroyImmediate(camGo); }
        }
    }
}
