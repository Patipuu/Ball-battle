using System.Collections.Generic;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>Check every generated card sentence, including cards a particular test seed never offers.</summary>
    public class UiDenseTextAuditTests
    {
        static IEnumerable<Card> Cards()
        {
            yield return new Card(CardKind.DamageUp);
            yield return new Card(CardKind.SpeedUp);
            yield return new Card(CardKind.MaxHp);
            yield return new Card(CardKind.Heal);
            yield return new Card(CardKind.SkillUp, level: 2);
            yield return new Card(CardKind.SkillUp, level: 3);
            foreach (var weapon in WeaponRegistry.All) yield return new Card(CardKind.SwapWeapon, weapon.Id);
            foreach (var trait in TraitRegistry.All)
                for (var level = 1; level <= 2; level++) yield return new Card(CardKind.Trait, trait.Id, level);
        }

        [TestCase(Lang.En)]
        [TestCase(Lang.Vi)]
        public void EveryCardDescriptionFitsItsTextColumn(Lang lang)
        {
            var previous = StringTable.Current;
            try
            {
                StringTable.Current = lang;
                var issues = new List<string>();
                foreach (var card in Cards())
                {
                    var label = $"{card.Kind} {card.Id} {card.Level}";
                    var title = UiVietnamese.Translate(CardText.Title(card));
                    var detail1 = UiVietnamese.Translate(CardText.Detail(card));
                    var detail2 = UiVietnamese.Translate(CardText.Detail2(card));
                    // Both card screens reserve the 209 px column right of their 28 px icon.
                    // Start Pick draws the title at 2x and has no title wrapping.
                    Check("title x2", title, 104, label, issues);
                    Check("detail 1", detail1, CardText.MaxDetailWidth, label, issues);
                    Check("detail 2", detail2, CardText.MaxDetailWidth, label, issues);
                }
                Assert.That(issues, Is.Empty, string.Join("\n", issues));
            }
            finally { StringTable.Current = previous; }
        }

        static void Check(string field, string text, int max, string card, List<string> issues)
        {
            var width = PixelFont.MeasureWidth(text);
            if (width > max) issues.Add($"{card} {field}: {width}>{max} {text}");
        }

        [TestCase(Lang.En)]
        [TestCase(Lang.Vi)]
        public void HowToStepTextStaysInsideEachBox(Lang lang)
        {
            var previous = StringTable.Current;
            try
            {
                StringTable.Current = lang;
                foreach (var step in HowToPlayView.Steps)
                {
                    var title = UiVietnamese.Translate(step.title);
                    var detail = UiVietnamese.Translate(step.detail);
                    Assert.That(PixelFont.MeasureWidth(title) * 2, Is.LessThanOrEqualTo(193), title);
                    Assert.That(PixelFont.MeasureWidth(detail), Is.LessThanOrEqualTo(193), detail);
                }
            }
            finally { StringTable.Current = previous; }
        }
    }
}
