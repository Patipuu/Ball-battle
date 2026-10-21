using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>BB-BAL3: the DAMAGE card text names what it now also boosts, and still fits the card.</summary>
    public class BbBal3Tests
    {
        [Test]
        public void DamageCardTextNamesSporePoisonAndMirrorReflectAndFits()
        {
            var card = new Card(CardKind.DamageUp);
            Assert.That(CardText.Detail(card), Is.EqualTo("MELEE, SHOTS, POISON, REFLECT"));
            Assert.That(CardText.Detail2(card), Is.Empty);
            Assert.That(PixelFont.MeasureWidth(CardText.Detail(card)), Is.LessThanOrEqualTo(CardText.MaxDetailWidth));
            Assert.That(PixelFont.MeasureWidth(CardText.Detail2(card)), Is.LessThanOrEqualTo(CardText.MaxDetailWidth));
        }
    }
}
