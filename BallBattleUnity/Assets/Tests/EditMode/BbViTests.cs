using BallBattle.View;
using BallBattle.EditorTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;

namespace BallBattle.Tests
{
    public class BbViTests
    {
        Lang previous;

        [SetUp]
        public void SetUp()
        {
            previous = StringTable.Current;
            StringTable.Current = Lang.Vi;
        }

        [TearDown]
        public void TearDown() => StringTable.Current = previous;

        [Test]
        public void TreeNamesFollowTheApprovedVietnameseGlossary()
        {
            Assert.That(UiVietnamese.Translate("REKINDLE"), Is.EqualTo("LỬA CHƯA TÀN"));
            Assert.That(UiVietnamese.Translate("NO BRAKES"), Is.EqualTo("LAO KHÔNG PHANH"));
            Assert.That(UiVietnamese.Translate("OUTBREAK"), Is.EqualTo("PHÁT TÁC"));
            Assert.That(UiVietnamese.Translate("PAYBACK"), Is.EqualTo("ĂN MIẾNG TRẢ MIẾNG"));
        }

        [Test]
        public void TemplatesUseVietnameseWordOrderAndTranslateTheirParts()
        {
            Assert.That(UiVietnamese.Translate("NEXT: FIGHT 4/8  SHELL KING  RETRY"),
                Is.EqualTo("TIẾP: TRẬN 4/8  SHELL KING  ĐẤU LẠI"));
            Assert.That(UiVietnamese.Translate("HOW: BLADE DMG +1 WITH EVERY HIT"),
                Is.EqualTo("LỐI ĐÁNH: MỖI ĐÒN TRÚNG, ĐAO +1 SÁT THƯƠNG"));   // UI-CLEAN1: shortened to fit the 270 px panel
            Assert.That(UiVietnamese.Translate("GOLD CHEST"), Is.EqualTo("RƯƠNG VÀNG"));
            Assert.That(UiVietnamese.Translate("WIN  +15 COINS"), Is.EqualTo("THẮNG  +15 XU"));
            Assert.That(UiVietnamese.Translate("FIGHT 4/8  BOSS"), Is.EqualTo("TRẬN 4/8  GẶP TRÙM"));
            Assert.That(UiVietnamese.Translate("COMMON"), Is.EqualTo("THƯỜNG"));
            Assert.That(UiVietnamese.Translate("RUN WON!"), Is.EqualTo("PHÁ ĐẢO!"));
            Assert.That(UiVietnamese.Translate("TOP SPEED +10%. START SPEED SAME"),
                Is.EqualTo("TỐC ĐỘ TỐI ĐA +10%. ĐẦU TRẬN GIỮ NGUYÊN"));
            Assert.That(TraitNames.List("SECOND WIND 2 THORNS 2 VAMPIRE"),
                Is.EqualTo("HỒI SỨC 2 PHẢN GAI 2 HÚT MÁU"));
        }

        [Test]
        public void EveryVietnameseTranslationHasAVisibleGlyph()
        {
            var asset = Resources.Load<TextAsset>("Strings/ui-vi");
            Assert.That(asset, Is.Not.Null);
            var font = PixelFont.Default;
            foreach (var line in asset.text.Split('\n'))
            {
                if (line.Length == 0 || line[0] == '#') continue;
                var tab = line.IndexOf('\t');
                if (tab < 0) continue;
                var copy = line.Substring(tab + 1).TrimEnd('\r').Replace("\\s", " ");
                for (var i = 0; i < 8; i++) copy = copy.Replace("{" + i + "}", "1");
                foreach (var c in PixelFont.Normalize(copy))
                    Assert.That(font.Has(c), Is.True, $"Missing glyph {c} in: {copy}");
            }
        }

        [Test]
        public void EverySkillTreeCardFitsInVietnamese()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            var root = new GameObject("ViTreeAudit");
            try
            {
                foreach (var id in new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" })
                {
                    var progress = new ProgressData();
                    MetaRules.Ball(progress, id).Level = 10;
                    var store = new MemorySaveStore();
                    store.Save(new SaveData { Progress = progress });
                    var tree = TreeView.Create(root.transform, art);
                    tree.Show(store, id);
                    foreach (TreeView.Box box in Enum.GetValues(typeof(TreeView.Box)))
                    {
                        tree.Select(box);
                        foreach (var text in tree.GetComponentsInChildren<PixelText>(false))
                            Assert.That(text.Bounds.xMin >= -135 && text.Bounds.xMax <= 135, Is.True,
                                $"{id} {box}: {text.Text} outside screen");
                        tree.OpenCard(box);
                        var area = TreeView.CardArea;
                        foreach (var text in tree.transform.Find("Card").GetComponentsInChildren<PixelText>(false))
                        {
                            if (text.Text.Length == 0) continue;
                            Assert.That(text.Bounds.xMin >= area.xMin + 2 && text.Bounds.xMax <= area.xMax - 2, Is.True,
                                $"{id} {box}: {text.Text} outside card");
                        }
                        tree.CloseCard();
                    }
                    UnityEngine.Object.DestroyImmediate(tree.gameObject);
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void EveryPickPanelShowsVietnameseRolesAndBranchesWithinThePanel()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            var root = new GameObject("ViPickAudit");
            try
            {
                var pick = PickWeaponView.Create(root.transform, art);
                foreach (var data in BallBattle.Sim.Skills.SkillDefinition.All)
                {
                    pick.Show(new[] { data.WeaponId });
                    var lines = pick.DetailText.Split('|');
                    Assert.That(lines[0], Does.Contain(UiVietnamese.TranslateRole(data.Role)), data.WeaponId);
                    Assert.That(lines[5], Does.StartWith("CẤP 2A " + UiVietnamese.Translate(data.SummaryA)), data.WeaponId);
                    Assert.That(lines[6], Does.StartWith("CẤP 2B " + UiVietnamese.Translate(data.SummaryB)), data.WeaponId);
                    for (var i = 0; i < lines.Length; i++)
                        Assert.That(PixelFont.MeasureWidth(lines[i]), Is.LessThanOrEqualTo(i == 4 ? 225 : 242),
                            $"{data.WeaponId} pick row {i}: {lines[i]}");
                }
                Assert.That(UiVietnamese.Translate("HUNTER"), Is.EqualTo("HUNTER"));
                Assert.That(UiVietnamese.TranslateRole("HUNTER"), Is.EqualTo("THỢ SĂN"));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void SettingsLanguageButtonsRequestARealSwitch()
        {
            var art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            var root = new GameObject("ViSettingsAudit");
            try
            {
                var settings = SettingsView.Create(root.transform, art, MenuView.Order);
                Lang? requested = null;
                settings.LanguageChanged += language => requested = language;
                settings.transform.Find("LanguageEN").GetComponent<PixelButton>().SimulateClick();
                Assert.That(requested, Is.EqualTo(Lang.En));
                requested = null;
                settings.transform.Find("LanguageVI").GetComponent<PixelButton>().SimulateClick();
                Assert.That(requested, Is.Null, "selecting the active language leaves it active");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }
}
