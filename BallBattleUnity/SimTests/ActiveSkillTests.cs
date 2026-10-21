using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-19b: the ten tapped skills of the Run player. Each one fires, shows its event and stays deterministic.</summary>
    public class ActiveSkillTests
    {
        static MatchSim Make(WeaponRule weapon, uint seed = 7, int level = 1, SkillBranch branch = SkillBranch.None, WeaponRule foe = null)
        {
            var player = new BallLoadout(weapon) { TapEnabled = true, SkillLevel = level, SkillBranch = branch, MaxHp = 1000, Hp = 1000 };
            var enemy = new BallLoadout(foe ?? new BladeRule()) { MaxHp = 1000, Hp = 1000 };
            return new MatchSim(new MatchConfig(), seed, new[] { player, enemy }, true);
        }

        static void Steps(MatchSim m, int n, List<SimEvent> log = null)
        {
            for (var i = 0; i < n && m.Outcome == MatchOutcome.Ongoing; i++)
            {
                m.Step();
                log?.AddRange(m.Events);
            }
        }

        static int Count(List<SimEvent> log, SkillFxEvent ev)
        {
            var n = 0;
            foreach (var e in log) if (e.Type == SimEventType.SkillFx && e.Variant == (byte)ev) n++;
            return n;
        }

        /// <summary>Warm up until the skill is ready, tap, and step <paramref name="after"/> more ticks.</summary>
        static List<SimEvent> TapAndWatch(MatchSim m, int after)
        {
            var log = new List<SimEvent>();
            Steps(m, 260);
            Assert.That(m.Balls[0].Skill.IsReady, "ready after warm-up");
            Assert.That(m.Tap(0), Is.True);
            Steps(m, after, log);
            return log;
        }

        [Test] public void EmberLeavesAFireTrail()
        {
            var m = Make(new BladeRule());
            var log = TapAndWatch(m, 40);
            Assert.That(Count(log, SkillFxEvent.EmberDash), Is.EqualTo(1));
            Assert.That(m.Fx.ActiveCount, Is.GreaterThan(3));
            Assert.That(m.Fx.Get(0).Kind, Is.EqualTo(FxKind.FireTrail));
            Assert.That(m.Balls[0].Skill.IsReady, Is.False, "cooldown started");
        }

        [Test] public void MantisBlinksBehindAndSlashes()
        {
            var m = Make(new FangRule());
            var hp = m.Balls[1].Hp;
            var log = TapAndWatch(m, 2);
            Assert.That(Count(log, SkillFxEvent.MantisBlink), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.MantisSlash), Is.GreaterThanOrEqualTo(1));
            Assert.That(m.Balls[1].Hp, Is.LessThan(hp));
        }

        [Test] public void AnglerThrowsAHookThatHitsOrMisses()
        {
            var m = Make(new PikeRule());
            var log = TapAndWatch(m, 150);
            Assert.That(Count(log, SkillFxEvent.HookThrown), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.HookHit) + Count(log, SkillFxEvent.HookMiss), Is.EqualTo(1));
        }

        [Test] public void WreckerChargesThenRamsOnTheSecondTap()
        {
            var m = Make(new BrawlerRule());
            Steps(m, 260);
            m.Tap(0); Steps(m, 1);
            var wreck = (WreckerCharge)m.Balls[0].Skill.Act;
            Assert.That(wreck.Charging, Is.True);
            Steps(m, 40);
            var held = m.Balls[0].Pos;
            Steps(m, 20);
            Assert.That(m.Balls[0].Pos.X, Is.EqualTo(held.X), "held in place while charging");
            Assert.That(m.Balls[0].Pos.Y, Is.EqualTo(held.Y));
            Assert.That(wreck.Charge, Is.GreaterThan(Fix.Zero));
            var log = new List<SimEvent>();
            m.Tap(0); Steps(m, 2, log);
            Assert.That(wreck.Charging, Is.False);
            Assert.That(Count(log, SkillFxEvent.WreckLaunch), Is.EqualTo(1));
            Assert.That(m.Balls[0].Skill.IsReady, Is.False);
        }

        [Test] public void WreckerBacksFiresWhenHeldTooLong()
        {
            var m = Make(new BrawlerRule());
            Steps(m, 260);
            m.Tap(0);
            var log = new List<SimEvent>();
            Steps(m, 260, log);
            Assert.That(Count(log, SkillFxEvent.WreckBackfire), Is.EqualTo(1));
        }

        [Test] public void SparklerFiresARicochetBolt()
        {
            var m = Make(new VolleyRule());
            var log = TapAndWatch(m, 3);
            Assert.That(Count(log, SkillFxEvent.BoltFired), Is.EqualTo(1));
            Assert.That(m.Projectiles.ActiveCount, Is.GreaterThan(0));
        }

        [Test] public void SporeOpensACloud()
        {
            var m = Make(new VenomRule());
            var log = TapAndWatch(m, 60);
            Assert.That(Count(log, SkillFxEvent.CloudOpen), Is.EqualTo(1));
            var clouds = 0;
            for (var i = 0; i < FxPool.Capacity; i++) if (m.Fx.Get(i).Active && m.Fx.Get(i).Kind == FxKind.Cloud) clouds++;
            Assert.That(clouds, Is.EqualTo(1));
        }

        [Test] public void MirrorOpensAParryWindowThatCatchesOrMisses()
        {
            var m = Make(new AegisRule());
            var log = TapAndWatch(m, 80);
            Assert.That(Count(log, SkillFxEvent.ParryOpen), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.ParryHit) + Count(log, SkillFxEvent.ParryMiss), Is.EqualTo(1));
        }

        [Test] public void MirrorMissedWindowCostsExtraCooldown()
        {
            var m = Make(new AegisRule(), 7, 1, SkillBranch.None, new PikeRule());
            Steps(m, 260);
            var skill = m.Balls[0].Skill;
            m.Tap(0); Steps(m, 1);
            // park the foe far away so nothing can land in the window
            m.Balls[1].StunTicks = 100;
            m.Balls[1].Pos = new FixVec2(m.Arena.Right - 20, m.Arena.Top - 20);
            m.Balls[0].Pos = new FixVec2(m.Arena.Left + 20, m.Arena.Bottom + 20);
            var log = new List<SimEvent>();
            Steps(m, 40, log);
            Assert.That(Count(log, SkillFxEvent.ParryMiss), Is.EqualTo(1));
            Assert.That(skill.ReadyAt, Is.GreaterThan(m.ActiveTick + skill.CooldownTicks - 5));
        }

        [Test] public void HiveReleasesBeesThatSting()
        {
            var m = Make(new RigRule());
            var log = TapAndWatch(m, 240);
            Assert.That(Count(log, SkillFxEvent.BeesReleased), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.BeeSting), Is.GreaterThan(0));
        }

        [Test] public void HiveBeeStingsRemoveEnemyHp()
        {
            var m = Make(new RigRule(), foe: new HarmlessRule());
            Steps(m, 260);
            var before = m.Balls[1].Hp;
            Assert.That(m.Tap(0), Is.True);
            var log = new List<SimEvent>();
            Steps(m, 240, log);
            var beeDamage = Fix.Zero;
            foreach (var e in log)
                if (e.Type == SimEventType.Damage && e.A == 0 && e.B == 1 &&
                    e.Variant == (byte)DamageKind.Ability) beeDamage += e.Value;
            Assert.That(Count(log, SkillFxEvent.BeeSting), Is.GreaterThan(0));
            Assert.That(beeDamage, Is.GreaterThan(Fix.Zero), "bee sting must pass through the damage pipeline");
            Assert.That(before - m.Balls[1].Hp, Is.GreaterThanOrEqualTo(beeDamage));
        }

        [Test] public void ChorusLitColourLastsFifteenSecondsFromItsMostRecentTouch()
        {
            var player = new BallLoadout(new ChorusRule()) { TapEnabled = true, MaxHp = 10000 };
            var enemy = new BallLoadout(new HarmlessRule()) { MaxHp = 10000 };
            var m = Stage.Build(Stage.StillConfig(), (player, FT.V(0, 0), 0f), (enemy, FT.V(80, 0), 0f));
            var chorus = (ChorusRule)m.Balls[0].Weapon;
            Assert.That(SkillTuning.UnityFadeTicks, Is.EqualTo(15 * MatchConfig.TicksPerSecond));
            Stage.Park(m, FT.V(0, 0), chorus.Orb(ChorusRule.Pink));
            m.Step();
            Assert.That(chorus.IsLit(ChorusRule.Pink), Is.True);
            var firstLitTick = m.ActiveTick;
            while (m.ActiveTick < firstLitTick + SkillTuning.UnityFadeTicks - 10)
            {
                Stage.Park(m, FT.V(0, 0), FT.V(80, 0));
                m.Step();
            }
            Assert.That(chorus.IsLit(ChorusRule.Pink), Is.True);
            Stage.Park(m, FT.V(0, 0), chorus.Orb(ChorusRule.Pink));
            m.Step();
            var refreshedAt = m.ActiveTick;
            while (m.ActiveTick < refreshedAt + 20)
            {
                Stage.Park(m, FT.V(0, 0), FT.V(80, 0));
                m.Step();
            }
            Assert.That(chorus.IsLit(ChorusRule.Pink), Is.True, "touch refreshed the expiring colour");
            while (m.ActiveTick < refreshedAt + SkillTuning.UnityFadeTicks)
            {
                Stage.Park(m, FT.V(0, 0), FT.V(80, 0));
                m.Step();
            }
            Assert.That(chorus.IsLit(ChorusRule.Pink), Is.False);
        }

        [Test] public void ChorusReleasesUnityByHand()
        {
            var m = Make(new ChorusRule());
            var log = new List<SimEvent>();
            Steps(m, 260, log);
            var chorus = (ChorusRule)m.Balls[0].Weapon;
            for (var i = 0; i < 1500 && chorus.Lit == 0 && m.Outcome == MatchOutcome.Ongoing; i++) Steps(m, 1, log);
            Assume.That(chorus.Lit, Is.GreaterThan(0), "an orb touched the foe");
            Assert.That(Count(log, SkillFxEvent.UnityRelease), Is.EqualTo(0), "never by itself");
            var lit = chorus.Lit;
            m.Tap(0);
            var after = new List<SimEvent>();
            Steps(m, 2, after);
            Assert.That(Count(after, SkillFxEvent.UnityRelease), Is.EqualTo(1));
            Assert.That(chorus.Lit, Is.LessThan(lit));
            Assert.That(chorus.Rings, Is.EqualTo(1));
        }

        [Test] public void ChorusFiveLitDoesNotBurstOnItsOwn()
        {
            var m = Make(new ChorusRule());
            var log = new List<SimEvent>();
            Steps(m, 3000, log);
            Assert.That(Count(log, SkillFxEvent.UnityRelease), Is.EqualTo(0));
            Assert.That(((ChorusRule)m.Balls[0].Weapon).Rings, Is.EqualTo(0));
        }

        [Test] public void HunterLungesAndCoils()
        {
            var m = Make(new SerpentRule());
            var log = TapAndWatch(m, 150);
            Assert.That(Count(log, SkillFxEvent.CoilLunge), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.CoilCatch), Is.EqualTo(1));
            Assert.That(Count(log, SkillFxEvent.CoilBite), Is.GreaterThan(0));
        }

        [Test] public void TappingBeforeReadyDoesNothing()
        {
            var m = Make(new BladeRule());
            Steps(m, 5);
            m.Tap(0);
            var log = new List<SimEvent>();
            Steps(m, 30, log);
            Assert.That(Count(log, SkillFxEvent.EmberDash), Is.EqualTo(0));
        }

        static readonly string[] Weapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };

        static WeaponRule New(string id)
        {
            switch (id)
            {
                case "blade": return new BladeRule();
                case "fang": return new FangRule();
                case "pike": return new PikeRule();
                case "brawler": return new BrawlerRule();
                case "volley": return new VolleyRule();
                case "venom": return new VenomRule();
                case "aegis": return new AegisRule();
                case "rig": return new RigRule();
                case "chorus": return new ChorusRule();
                default: return new SerpentRule();
            }
        }

        static ulong Play(string id, uint seed, int level, SkillBranch branch)
        {
            var m = Make(New(id), seed, level, branch, New(Weapons[(int)(seed % 10)]));
            for (var t = 0; t < 2400 && m.Outcome == MatchOutcome.Ongoing; t++)
            {
                if (t % 25 == (int)(seed % 25)) m.Tap(0);
                m.Step();
            }
            return m.ComputeHash();
        }

        [Test] public void EverySkillAndBranchIsDeterministicUnderRandomTaps()
        {
            foreach (var id in Weapons)
                foreach (var (lv, br) in new[] { (1, SkillBranch.None), (2, SkillBranch.A), (2, SkillBranch.B), (3, SkillBranch.A), (3, SkillBranch.B) })
                    for (uint seed = 1; seed <= 3; seed++)
                        Assert.That(Play(id, seed, lv, br), Is.EqualTo(Play(id, seed, lv, br)), $"{id} L{lv}{br} seed {seed}");
        }

        [Test] public void EnemiesAndVersusKeepTheAutomaticSkill()
        {
            var m = Make(new BladeRule());
            Assert.That(m.Balls[1].Skill.Act, Is.Null);
            Assert.That(m.Balls[1].Skill.Manual, Is.False);
            Assert.That(m.Balls[0].Skill.Act, Is.Not.Null);
        }
    }
}
