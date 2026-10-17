using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-18: the Chorus profile belongs only to plain So tài.</summary>
    public class ChorusVersusProfileReport
    {
        [Test]
        public void RunMatchKeepsOriginalChorusDamage()
        {
            var versus = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new ChorusRule()), new BallLoadout(new BrawlerRule())
            });
            var run = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new ChorusRule()), new BallLoadout(new BrawlerRule())
            }, true);
            Assert.That(versus.IsPlainVersus, Is.True);
            Assert.That(run.IsPlainVersus, Is.False);
            var hit = new HitContext(Fix.Zero, Fix.Zero);
            Assert.That(run.Balls[0].Weapon.Damage(hit), Is.EqualTo(TributeTuning.ChorusStartDamage));
            Assert.That(versus.Balls[0].Weapon.Damage(hit), Is.EqualTo(TributeTuning.ChorusStartDamage * TributeTuning.ChorusVersusMeleeScale));
        }

        [Test]
        public void TreeAlsoDisablesVersusProfile()
        {
            var m = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new ChorusRule()) { Tree = new TreeLoadout() },
                new BallLoadout(new BrawlerRule())
            });
            Assert.That(m.IsPlainVersus, Is.False);
        }

        [Test]
        public void IncomingAbilityGuardAppliesOnlyInVersus()
        {
            var versus = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new ChorusRule()), new BallLoadout(new SerpentRule())
            });
            var run = new MatchSim(new MatchConfig(), 1, new[]
            {
                new BallLoadout(new ChorusRule()), new BallLoadout(new SerpentRule())
            }, true);
            Assert.That(versus.DealDamage(versus.Balls[1], versus.Balls[0], 10, DamageKind.Ability),
                Is.EqualTo(10 * TributeTuning.ChorusVersusAbilityDamageScale));
            Assert.That(run.DealDamage(run.Balls[1], run.Balls[0], 10, DamageKind.Ability), Is.EqualTo((Fix)10));
        }
    }
}
