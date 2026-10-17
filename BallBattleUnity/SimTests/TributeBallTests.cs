using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-14: the two tribute balls - BGLGXLQ (chorus: ring of five colours, UNITY) and the snake (serpent: prey, tail, cuts).</summary>
    public class TributeBallTests
    {
        static MatchSim Match(string a, string b, uint seed) =>
            new MatchSim(new MatchConfig(), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });

        static List<SimEvent> Play(MatchSim m, int maxTicks = 20000)
        {
            var all = new List<SimEvent>();
            while (m.Outcome == MatchOutcome.Ongoing && m.Tick < maxTicks)
            {
                m.Step();
                all.AddRange(m.Events);
            }
            return all;
        }

        [Test]
        public void BothBallsAreRegisteredWithSkillsAndTreeNumbers()
        {
            foreach (var id in new[] { ChorusRule.WeaponId, SerpentRule.WeaponId })
            {
                Assert.That(WeaponRegistry.All.Any(e => e.Id == id), Is.True, id);
                Assert.That(SkillDefinition.ForWeapon(id), Is.Not.Null, id);
                Assert.That(TreeTuning.HpPct(id), Is.GreaterThan(0), id);
                Assert.That(TreeTuning.DamagePct(id), Is.GreaterThan(0), id);
            }
            Assert.That(TreeTuning.WeaponOrder.Length, Is.EqualTo(TreeTuning.HpPerRank.Length));
            Assert.That(TreeTuning.WeaponOrder.Length, Is.EqualTo(TreeTuning.DamagePerRank.Length));
        }

        [Test]
        public void ChorusOrbsLightEachColourOnceInAnyOrderAndUnityStartsANewRing()
        {
            int unities = 0, outOfOrder = 0, touches = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var m = Match("chorus", "blade", seed);
                var chorus = (ChorusRule)m.Balls[0].Weapon;
                var lit = new System.Collections.Generic.HashSet<int>();
                var ringsBefore = 0;
                var last = 0;
                while (m.Outcome == MatchOutcome.Ongoing && m.Tick < 20000)
                {
                    m.Step();
                    foreach (var e in m.Events.Where(e => e.A == 0))
                    {
                        if (e.Type == SimEventType.ChorusLit)
                        {
                            var c = (int)(e.Value.Raw >> Fix.FractionBits);
                            Assert.That(c, Is.InRange(ChorusRule.Pink, ChorusRule.Red));
                            Assert.That(lit.Add(c), Is.True, $"seed {seed}: colour {c} lit twice in one ring");
                            Assert.That(e.Variant, Is.EqualTo((byte)lit.Count), "Variant = colours lit so far");
                            Assert.That(e.B, Is.EqualTo(1).Or.EqualTo(-1), "an orb touching the enemy, or HYMN (-1)");
                            var enemy = m.Balls[1];
                            if (e.B == 1) touches++;
                            if (e.B == 1) Assert.That((enemy.Pos - e.Point).LengthSq, Is.LessThanOrEqualTo(FixMath.Sq(TributeTuning.ChorusOrbRadius + enemy.Radius + 2)), "the orb touched the enemy");
                            if (c != last + 1) outOfOrder++;
                            last = c;
                        }
                        if (e.Type == SimEventType.ChorusUnity)
                        {
                            Assert.That(lit.Count, Is.EqualTo(ChorusRule.Colors), "UNITY only when all five are lit");
                            unities++;
                            Assert.That(e.Variant, Is.EqualTo((byte)ringsBefore), "Variant = rings completed before");
                            Assert.That(e.Value, Is.EqualTo((TributeTuning.ChorusUnityBase + TributeTuning.ChorusUnityPerRing * ringsBefore) *
                                (m.IsPlainVersus ? TributeTuning.ChorusVersusAbilityScale : Fix.One)), "UNITY damage");
                            ringsBefore++;
                            lit.Clear();
                            last = 0;
                        }
                    }
                    Assert.That(chorus.Lit, Is.EqualTo(lit.Count));
                }
                Assert.That(chorus.Rings, Is.EqualTo(ringsBefore));
            }
            Assert.That(unities, Is.GreaterThan(20), "UNITY really happens in fights");
            Assert.That(outOfOrder, Is.GreaterThan(20), "colours light in any order");
            Assert.That(touches, Is.GreaterThan(100), "most colours are lit by orb touches");
        }

        [Test]
        public void ChorusOrbsCircleTheBall()
        {
            var m = Match("chorus", "blade", 5);
            var chorus = (ChorusRule)m.Balls[0].Weapon;
            m.Step();
            var a0 = chorus.OrbAngleDeg;
            for (var c = ChorusRule.Pink; c <= ChorusRule.Red; c++)
            {
                var d2 = (chorus.Orb(c) - m.Balls[0].Pos).LengthSq;
                Assert.That(d2, Is.InRange(FixMath.Sq(chorus.OrbDistance - Fix.Ratio(1, 4)), FixMath.Sq(chorus.OrbDistance + Fix.Ratio(1, 4))));
            }
            for (var i = 0; i < 10; i++) m.Step();
            Assert.That(chorus.OrbAngleDeg, Is.Not.EqualTo(a0), "the orbs turn");
        }

        [Test]
        public void BlueGivesOneShieldCharge()
        {
            var shields = 0;
            for (uint seed = 1; seed <= 10; seed++)
            {
                var events = Play(Match("chorus", "fang", seed));
                for (var i = 0; i < events.Count; i++)
                {
                    var e = events[i];
                    if (e.Type != SimEventType.ChorusLit || e.A != 0 || e.Value != (Fix)ChorusRule.Blue) continue;
                    Assert.That(events.Skip(i).Take(4).Any(x => x.Type == SimEventType.StatusApplied && x.A == 0 && x.Variant == (byte)StatusKind.Shield), Is.True);
                    shields++;
                }
            }
            Assert.That(shields, Is.GreaterThan(0));
        }

        [Test]
        public void PreyOnlyExistInMatchesWithTheSnake()
        {
            for (uint seed = 1; seed <= 5; seed++)
                Assert.That(Play(Match("blade", "fang", seed)).Any(e => e.Type == SimEventType.PreySpawned), Is.False);
            var m = Match("serpent", "blade", 3);
            var first = -1;
            while (m.Outcome == MatchOutcome.Ongoing && first < 0)
            {
                m.Step();
                if (m.Events.Any(e => e.Type == SimEventType.PreySpawned)) first = m.ActiveTick;
            }
            Assert.That(first, Is.EqualTo(TributeTuning.SerpentFirstPreyTicks), "first prey after 1.5 s");
        }

        [Test]
        public void TheSnakeEatsGrowsAndLosesSegmentsWhenCut()
        {
            int eaten = 0, cuts = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var m = Match("serpent", "blade", seed);
                var snake = (SerpentRule)m.Balls[0].Weapon;
                var length = 0;
                while (m.Outcome == MatchOutcome.Ongoing)
                {
                    m.Step();
                    foreach (var e in m.Events.Where(e => e.A == 0))
                    {
                        if (e.Type == SimEventType.PreyEaten)
                        {
                            eaten++;
                            length = System.Math.Min(TributeTuning.SerpentMaxSegments, length + 1);
                            Assert.That(e.B, Is.EqualTo(length), "B = tail length after eating");
                        }
                        if (e.Type == SimEventType.TailCut)
                        {
                            cuts++;
                            Assert.That(e.Value, Is.GreaterThan(Fix.Zero));
                            length -= (int)e.Value.Raw >> Fix.FractionBits;
                        }
                    }
                    Assert.That(snake.Segments, Is.EqualTo(length));
                    Assert.That(snake.PreyCount, Is.LessThanOrEqualTo(TributeTuning.SerpentPreyMax));
                }
                Assert.That(snake.Meals, Is.GreaterThanOrEqualTo(snake.Segments));
            }
            Assert.That(eaten, Is.GreaterThan(100));
            Assert.That(cuts, Is.GreaterThan(10));
        }

        [Test]
        public void TributeMatchesAreDeterministic()
        {
            foreach (var pair in new[] { ("chorus", "volley"), ("serpent", "aegis"), ("chorus", "serpent") })
            {
                var a = Match(pair.Item1, pair.Item2, 77);
                var b = Match(pair.Item1, pair.Item2, 77);
                Play(a);
                Play(b);
                Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()), pair.ToString());
                Assert.That(a.Tick, Is.EqualTo(b.Tick));
                var c = Match(pair.Item1, pair.Item2, 78);
                Play(c);
                Assert.That(c.ComputeHash(), Is.Not.EqualTo(a.ComputeHash()), "another seed plays another fight");
            }
        }

        [Test]
        public void RunsCanStartWithTheTributeBallsAndTheirTrees()
        {
            foreach (var id in new[] { "chorus", "serpent" })
            {
                var p = new ProgressData();
                p.Achievements.AddRange(new[] { UnlockRules.Boss1, UnlockRules.Win1 });   // unlocks both tribute balls
                uint seed = 1;
                while (!RunState.StartChoices(seed, UnlockRules.Weapons(p)).Contains(id)) seed++;   // a run offering this ball
                var t = Sim.Meta.MetaRules.Ball(p, id);
                t.Level = 10;
                Sim.Meta.MetaTree.Add(t, Sim.Meta.TreeNode.Vitality);
                Sim.Meta.MetaTree.Add(t, Sim.Meta.TreeNode.Instinct);
                Sim.Meta.MetaTree.Add(t, Sim.Meta.TreeNode.Opener);
                var run = RunState.Start(seed, id, p);
                Assert.That(run.Build.TreeStart, Is.True);
                run.Pick(0);
                var m = run.CreateMatch();
                if (id == "chorus") Assert.That(((ChorusRule)m.Balls[0].Weapon).Lit, Is.EqualTo(ChorusRule.Pink), "MORNING LIGHT");
                else Assert.That(((SerpentRule)m.Balls[0].Weapon).Segments, Is.EqualTo(2), "FIRST MEAL");
                Play(m);
            }
        }
    }
}
