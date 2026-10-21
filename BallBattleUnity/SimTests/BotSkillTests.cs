using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Rules 23: Run enemies press the same active skill as the player (BallLoadout.AutoSkill).</summary>
    public class BotSkillTests
    {
        static MatchSim Match(string enemyWeapon, bool bot)
        {
            var player = new BallLoadout(WeaponRegistry.Create("aegis"));
            var enemy = new BallLoadout(WeaponRegistry.Create(enemyWeapon)) { TapEnabled = bot, AutoSkill = bot };
            return new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 77, new[] { player, enemy }, true);
        }

        [Test]
        public void BotEmberLaysTheSameFireTrailAsThePlayer()
        {
            var m = Match("blade", true);
            Assert.That(m.Balls[1].Skill.Act, Is.InstanceOf<EmberTrail>(), "the player's active skill, not the old automatic one");
            for (var i = 0; i < 1800 && m.Outcome == MatchOutcome.Ongoing && m.Balls[1].Skill.FireCount == 0; i++) m.Step();
            Assert.That(m.Balls[1].Skill.FireCount, Is.GreaterThan(0), "the bot used its skill");
        }

        [Test]
        public void WithoutAutoSkillTheOldRuleStays()
        {
            var m = Match("blade", false);
            Assert.That(m.Balls[1].Skill.Act, Is.Null);
            Assert.That(m.Balls[1].AutoSkill, Is.False);
        }

        [Test]
        public void BotWreckerRamsAtFullChargeWithoutBackfire()
        {
            var m = Match("brawler", true);
            var act = (WreckerCharge)m.Balls[1].Skill.Act;
            var sawCharge = false;
            for (var i = 0; i < 2400 && m.Outcome == MatchOutcome.Ongoing && m.Balls[1].Skill.FireCount == 0; i++)
            {
                m.Step();
                if (act.Charging) sawCharge = true;
            }
            Assert.That(sawCharge, Is.True, "wound up first");
            Assert.That(m.Balls[1].Skill.FireCount, Is.GreaterThan(0), "then rammed");
        }

        [Test]
        public void RunEnemiesUseSkillsExceptInTheTapLesson()
        {
            var weapons = UnlockRules.Weapons(new ProgressData());
            var run = RunState.Start(5, RunState.StartChoices(5, weapons)[0], new ProgressData());
            var m = run.CreateMatch();
            Assert.That(m.Balls[1].AutoSkill, Is.True);
            Assert.That(m.Balls[1].Skill?.Act, Is.Not.Null);
            Assert.That(m.Balls[0].AutoSkill, Is.False, "the player still taps");

            var lesson = RunState.Start(5, RunState.StartChoices(5, weapons)[0], new ProgressData());
            lesson.TutorialRun = true;
            Assert.That(lesson.CreateMatch().Balls[1].AutoSkill, Is.False, "first tutorial fight: calm foe");
        }
    }
}
