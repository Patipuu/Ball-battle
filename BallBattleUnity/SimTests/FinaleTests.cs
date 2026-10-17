using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-19c: the tree FINALE of every ball lives in its tapped skill. Each one is deterministic and the new numbers show up.</summary>
    public class FinaleTests
    {
        static readonly string[] Weapons = { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig", "chorus", "serpent" };

        static MatchSim Make(WeaponRule weapon, char finale, uint seed = 7)
        {
            var player = new BallLoadout(weapon) { TapEnabled = true, MaxHp = 1000, Hp = 1000, Tree = new TreeLoadout { Finale = finale } };
            var enemy = new BallLoadout(new BladeRule()) { MaxHp = 1000, Hp = 1000 };
            return new MatchSim(new MatchConfig(), seed, new[] { player, enemy }, true);
        }

        static void Steps(MatchSim m, int n)
        {
            for (var i = 0; i < n && m.Outcome == MatchOutcome.Ongoing; i++) m.Step();
        }

        static MatchSim Ready(WeaponRule w, char finale)
        {
            var m = Make(w, finale);
            Steps(m, 260);
            Assert.That(m.Balls[0].Skill.IsReady, "ready after warm-up");
            return m;
        }

        static int Fx(MatchSim m, FxKind k)
        {
            var n = 0;
            for (var i = 0; i < FxPool.Capacity; i++) if (m.Fx.Get(i).Active && m.Fx.Get(i).Kind == k) n++;
            return n;
        }

        [Test] public void EveryFinaleIsDeterministicUnderRandomTaps()
        {
            foreach (var id in Weapons)
                foreach (var fin in new[] { 'A', 'B' })
                {
                    var hashes = new ulong[2];
                    for (var run = 0; run < 2; run++)
                    {
                        var m = Make(WeaponRegistry.Create(id), fin, 11);
                        var rng = 12345u;
                        for (var t = 0; t < 2400 && m.Outcome == MatchOutcome.Ongoing; t++)
                        {
                            rng = rng * 1664525u + 1013904223u;
                            if ((rng >> 24) % 23 == 0) m.Tap(0);
                            m.Step();
                        }
                        hashes[run] = m.ComputeHash();
                    }
                    Assert.That(hashes[1], Is.EqualTo(hashes[0]), id + " " + fin);
                }
        }

        [Test] public void MantisAfterspinArmsThreeHitsAfterTheBlink()
        {
            var m = Ready(new FangRule(), 'A');
            m.Tap(0); Steps(m, 1);
            Assert.That(((FangRule)m.Balls[0].Weapon).AfterspinHits, Is.GreaterThan(0));
            var none = Ready(new FangRule(), '-');
            none.Tap(0); Steps(none, 1);
            Assert.That(((FangRule)none.Balls[0].Weapon).AfterspinHits, Is.EqualTo(0));
        }

        [Test] public void MantisBloodlustLengthensTheCooldown()
        {
            var a = Make(new FangRule(), 'B').Balls[0].Skill.Act;
            var b = Make(new FangRule(), '-').Balls[0].Skill.Act;
            Assert.That(a.CooldownTicks, Is.EqualTo(b.CooldownTicks * 11 / 10));
        }

        [Test] public void SparklerBoltCarriesItsFinaleMark()
        {
            foreach (var pair in new[] { ('A', 1), ('B', 2), ('-', 0) })
            {
                var m = Ready(new VolleyRule(), pair.Item1);
                m.Tap(0); Steps(m, 2);
                var tags = new List<int>();
                for (var i = 0; i < ProjectilePool.Capacity; i++) if (m.Projectiles.Get(i).Active && m.Projectiles.Get(i).Radius >= 4) tags.Add(m.Projectiles.Get(i).Tag);
                Assert.That(tags, Does.Contain(pair.Item2), "finale " + pair.Item1);
            }
        }

        [Test] public void SporeOutbreakLeavesASmallCloudBehind()
        {
            var m = Ready(new VenomRule(), 'A');
            m.Tap(0); Steps(m, 310);
            Assert.That(Fx(m, FxKind.Cloud), Is.EqualTo(1), "the small cloud");
            var plain = Ready(new VenomRule(), '-');
            plain.Tap(0); Steps(plain, 310);
            Assert.That(Fx(plain, FxKind.Cloud), Is.EqualTo(0));
        }

        [Test] public void HiveQueenBlocksOneBlowAndStingStormAddsTwoDrones()
        {
            var q = Ready(new RigRule(), 'A');
            q.Tap(0); Steps(q, 2);
            var act = q.Balls[0].Skill.Act;
            Assert.That(act.Guard(q.Balls[1], Fix.One, DamageKind.Weapon), Is.True);
            Assert.That(act.Guard(q.Balls[1], Fix.One, DamageKind.Weapon), Is.False, "only one");
            var s = Ready(new RigRule(), 'B');
            s.Tap(0); Steps(s, 2);
            var p = Ready(new RigRule(), '-');
            p.Tap(0); Steps(p, 2);
            Assert.That(Fx(s, FxKind.Bee), Is.EqualTo(Fx(p, FxKind.Bee) + 2));
        }

        [Test] public void SeepFeedsTheCloudAndHydraHealsOnCoilBites()
        {
            Fix Power(int growth)
            {
                var m = Make(new VenomRule(), '-');
                m.Balls[0].Tree.Growth = growth;
                Steps(m, 260); m.Tap(0); Steps(m, 2);
                for (var i = 0; i < FxPool.Capacity; i++) if (m.Fx.Get(i).Active && m.Fx.Get(i).Kind == FxKind.Cloud) return m.Fx.Get(i).Power;
                return Fix.Zero;
            }
            Assert.That(Power(2), Is.GreaterThan(Power(0)));

            var s = Make(new SerpentRule(), 'A');
            Steps(s, 260);
            var ball = s.Balls[0];
            ((SerpentRule)ball.Weapon).ApplyTreeStart();
            ball.Hp = 500;
            ((SerpentRule)ball.Weapon).HydraHeal();
            Assert.That(ball.Hp, Is.GreaterThan((Fix)500));
        }

        static MatchSim AtLevel(WeaponRule w, int level, SkillBranch branch)
        {
            var player = new BallLoadout(w) { TapEnabled = true, SkillLevel = level, SkillBranch = branch, MaxHp = 1000, Hp = 1000, Tree = new TreeLoadout() };
            var enemy = new BallLoadout(new BladeRule()) { MaxHp = 1000, Hp = 1000 };
            var m = new MatchSim(new MatchConfig(), 7, new[] { player, enemy }, true);
            Steps(m, 260);
            Assert.That(m.Balls[0].Skill.IsReady);
            m.Tap(0); Steps(m, 2);
            return m;
        }

        [Test] public void SkillLevel3IsStrongerThanLevel2()
        {
            int Life(MatchSim m) { var n = 0; for (var i = 0; i < FxPool.Capacity; i++) if (m.Fx.Get(i).Active) n += m.Fx.Get(i).Life; return n; }
            Assert.That(Life(AtLevel(new BladeRule(), 3, SkillBranch.A)), Is.GreaterThan(Life(AtLevel(new BladeRule(), 2, SkillBranch.A))), "EMBER A");
            Assert.That(Fx(AtLevel(new RigRule(), 3, SkillBranch.A), FxKind.Bee), Is.GreaterThan(Fx(AtLevel(new RigRule(), 2, SkillBranch.A), FxKind.Bee)), "HIVE A");
            Fix Radius(MatchSim m, FxKind k) { for (var i = 0; i < FxPool.Capacity; i++) if (m.Fx.Get(i).Active && m.Fx.Get(i).Kind == k) return m.Fx.Get(i).Radius; return Fix.Zero; }
            Assert.That(Radius(AtLevel(new VenomRule(), 3, SkillBranch.A), FxKind.Cloud), Is.GreaterThan(Radius(AtLevel(new VenomRule(), 2, SkillBranch.A), FxKind.Cloud)), "SPORE A");
            Assert.That(Radius(AtLevel(new PikeRule(), 3, SkillBranch.A), FxKind.Hook), Is.GreaterThan(Radius(AtLevel(new PikeRule(), 2, SkillBranch.A), FxKind.Hook)), "ANGLER A");
            Fix Bolt(MatchSim m) { for (var i = 0; i < ProjectilePool.Capacity; i++) if (m.Projectiles.Get(i).Active && m.Projectiles.Get(i).Radius >= 4) return m.Projectiles.Get(i).Damage; return Fix.Zero; }
            Assert.That(Bolt(AtLevel(new VolleyRule(), 3, SkillBranch.B)), Is.GreaterThan(Bolt(AtLevel(new VolleyRule(), 2, SkillBranch.B))), "SPARKLER B");
        }

        [Test] public void EmberBackdraftShortensTheTrail()
        {
            var b = Ready(new BladeRule(), 'B'); b.Tap(0); Steps(b, 30);
            var p = Ready(new BladeRule(), '-'); p.Tap(0); Steps(p, 30);
            var lifeB = 0; var lifeP = 0;
            for (var i = 0; i < FxPool.Capacity; i++)
            {
                if (b.Fx.Get(i).Active) lifeB += b.Fx.Get(i).Life;
                if (p.Fx.Get(i).Active) lifeP += p.Fx.Get(i).Life;
            }
            Assert.That(lifeB, Is.LessThan(lifeP));
        }
    }
}
