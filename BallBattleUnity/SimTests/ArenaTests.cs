using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class ArenaTests
    {
        static MatchSim Make(string arena, string a, string b, uint seed)
            => new MatchSim(ArenaRegistry.Create(arena), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });

        static readonly string[] Arenas = ArenaRegistry.All.Select(e => e.Id).ToArray();

        [Test]
        public void SevenArenasAndAllFitTheHud()
        {
            Assert.That(Arenas.Length, Is.EqualTo(7));
            foreach (var id in Arenas)
            {
                var c = ArenaRegistry.Create(id);
                Assert.That(c.ArenaHeight, Is.LessThanOrEqualTo(230f), id);
                Assert.That(c.ArenaWidth, Is.LessThanOrEqualTo(260f), id);
                Assert.That(c.MinArenaHeight, Is.LessThanOrEqualTo(c.ArenaHeight), id);
            }
        }

        [Test]
        public void ClassicIsTheDefaultConfig()
        {
            var d = new MatchConfig();
            var c = ArenaRegistry.Create(ArenaRegistry.Classic);
            Assert.That(c.ArenaWidth, Is.EqualTo(d.ArenaWidth));
            Assert.That(c.Gravity, Is.EqualTo(d.Gravity));
            Assert.That(c.Layout.ObstacleCount, Is.EqualTo(0));
            Assert.That(c.Layout.WallDamage, Is.EqualTo(0f));
        }

        [Test]
        public void ForSeedCoversEveryArena()
        {
            var seen = Enumerable.Range(0, 70).Select(i => ArenaRegistry.ForSeed((uint)i)).Distinct().Count();
            Assert.That(seen, Is.EqualTo(7));
        }

        [Test]
        public void BallsStayInsideAndOutOfObstacles()
        {
            var ids = WeaponRegistry.All.Select(e => e.Id).ToArray();
            foreach (var arena in Arenas)
                for (var w = 0; w < ids.Length; w++)
                    for (uint seed = 1; seed <= 3; seed++)
                    {
                        var m = Make(arena, ids[w], ids[(w + 3) % ids.Length], seed);
                        var layout = m.Config.Layout;
                        while (m.Outcome == MatchOutcome.Ongoing)
                        {
                            m.Step();
                            var r = m.Arena;
                            foreach (var b in m.Balls)
                            {
                                Assert.That(b.Pos.X, Is.InRange(r.Left, r.Right), $"{arena} {ids[w]} s{seed} t{m.ActiveTick}");
                                Assert.That(b.Pos.Y, Is.InRange(r.Bottom, r.Top), $"{arena} {ids[w]} s{seed} t{m.ActiveTick}");
                                for (var i = 0; i < layout.ObstacleCount; i++)
                                {
                                    var o = layout.Get(i);
                                    Assert.That((b.Pos - o.ClosestCore(b.Pos)).Length, Is.GreaterThan(o.Radius), $"{arena} {ids[w]} s{seed} t{m.ActiveTick} inside obstacle");
                                }
                            }
                        }
                    }
        }

        [Test]
        public void SpikeWallsHurtAndArenasAreDeterministic()
        {
            var a = Make(ArenaRegistry.SpikeWalls, "blade", "fang", 7);
            var b = Make(ArenaRegistry.SpikeWalls, "blade", "fang", 7);
            for (var i = 0; i < 600; i++) { a.Step(); b.Step(); }
            Assert.That(a.ComputeHash(), Is.EqualTo(b.ComputeHash()));
            Assert.That(a.Balls.Any(x => x.Hp < x.MaxHp), "wall hits cost HP");
        }
    }
}