using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    public class ViewDataTests
    {
        [Test]
        public void FontGlyphsAreWellFormedAndUnique()
        {
            var keys = PixelFontData.Glyphs.Select(g => g.Key).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "duplicate glyph");
            Assert.That(PixelFontData.Glyphs[0].Key, Is.EqualTo(' '), "PixelText treats index 0 as space");
            foreach (var g in PixelFontData.Glyphs)
            {
                var rows = g.Value.Split(' ');
                Assert.That(rows.Length, Is.EqualTo(PixelFontData.GlyphHeight), $"'{g.Key}' rows");
                foreach (var r in rows) Assert.That(r.Length, Is.EqualTo(PixelFontData.GlyphWidth), $"'{g.Key}' row width");
            }
        }

        [Test]
        public void HudCharactersAreAllInTheFont()
        {
            foreach (var e in WeaponRegistry.All)
            {
                var text = e.DisplayName + e.Create().StatLabel + "0123456789.";
                foreach (var c in text)
                    Assert.That(PixelFontData.IndexOf(c), Is.GreaterThanOrEqualTo(0), $"'{c}' missing from font ({e.Id})");
            }
        }

        [Test]
        public void TraitAndArenaTextIsInTheFontAndEveryTraitHasABadge()
        {
            foreach (var e in TraitRegistry.All)
            {
                Assert.That(TraitBadgeView.HasStyle(e.Id), Is.True, $"badge style for {e.Id}");
                foreach (var c in e.DisplayName + e.Blurb)
                    Assert.That(PixelFontData.IndexOf(c), Is.GreaterThanOrEqualTo(0), $"'{c}' missing from font (trait {e.Id})");
            }
            foreach (var a in ArenaRegistry.All)
                foreach (var c in "ARENA: " + a.DisplayName)
                    Assert.That(PixelFontData.IndexOf(c), Is.GreaterThanOrEqualTo(0), $"'{c}' missing from font (arena {a.Id})");
        }
        [Test]
        public void EveryCardTextFitsTheCard()
        {
            var cards = new System.Collections.Generic.List<Card>
            {
                new Card(CardKind.DamageUp), new Card(CardKind.SpeedUp), new Card(CardKind.Heal), new Card(CardKind.MaxHp),
            };
            foreach (var e in TraitRegistry.All) { cards.Add(new Card(CardKind.Trait, e.Id, 1)); cards.Add(new Card(CardKind.Trait, e.Id, 2)); }
            foreach (var w in WeaponRegistry.All) cards.Add(new Card(CardKind.SwapWeapon, w.Id));
            foreach (var c in cards)
            {
                var title = CardText.Title(c);
                var d1 = CardText.Detail(c);
                var d2 = CardText.Detail2(c);
                Assert.That(title.Length, Is.LessThanOrEqualTo(CardText.MaxTitleChars), $"title '{title}' ({c})");
                Assert.That(d1.Length, Is.InRange(1, CardText.MaxDetailChars), $"detail '{d1}' ({c})");
                Assert.That(d2.Length, Is.LessThanOrEqualTo(CardText.MaxDetailChars), $"detail2 '{d2}' ({c})");
                foreach (var ch in title + d1 + d2)
                    Assert.That(PixelFontData.IndexOf(ch), Is.GreaterThanOrEqualTo(0), $"'{ch}' missing from font ({c})");
            }
        }
        [Test]
        public void EveryWeaponHasPaletteAndArt()
        {
            var lib = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(lib, Is.Not.Null, "run BallBattle/Build Scenes");
            foreach (var e in WeaponRegistry.All)
            {
                Assert.That(Palette.Weapons.Any(w => w.Id == e.Id), Is.True, $"palette for {e.Id}");
                var art = lib.Get(e.Id);
                Assert.That(art.Ball, Is.Not.Null, $"ball sprite {e.Id}");
                Assert.That(art.Ball.pixelsPerUnit, Is.EqualTo(1f));
                Assert.That(art.Ball.texture.filterMode, Is.EqualTo(FilterMode.Point));
                var hasBlade = e.Create().HasBlade;
                Assert.That(art.Blade != null, Is.EqualTo(hasBlade), $"blade sprite iff weapon has a blade ({e.Id})");
            }
        }

        [Test]
        public void HeatGoesFromColdToHot()
        {
            Assert.That(Palette.Heat("blade", 1f), Is.EqualTo(0f));
            Assert.That(Palette.Heat("blade", 20f), Is.EqualTo(1f));
            Assert.That(Palette.Heat("blade", 99f), Is.EqualTo(1f));
        }
    }
}
