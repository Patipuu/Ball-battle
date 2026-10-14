using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// BB-6c spike re-hit immunity: after spike damage a ball takes no spike damage for ArenaTuning.SpikeImmunityTicks (10)
    /// ACTIVE ticks; per ball; only spike damage is blocked.
    /// </summary>
    public class SpikeImmunityTests
    {
        static readonly Fix R = Fix.FromInt(16);   // MatchConfig.BallRadius
        static readonly Fix Spike = ArenaTuning.SpikeWallDamage;

        static MatchConfig SpikeConfig(SpikeCorner corners = ArenaTuning.SpikeCorners, Obstacle[] obstacles = null)
        {
            var c = Stage.StillConfig();
            c.Layout = new ArenaLayout(obstacles, ArenaTuning.SpikeWallDamage, corners);
            return c;
        }

        /// <summary>Narrow arena (56 px wide), all four corners spiked: a ball moving sideways near the top touches a spike zone every few ticks.</summary>
        static MatchConfig Narrow()
        {
            var c = SpikeConfig(SpikeCorner.All);
            c.ArenaWidth = FT.X(56f);
            return c;
        }

        struct Contact
        {
            public int Ball, Tick, ActiveTick;
            public Fix Damage;
        }

        /// <summary>Steps <paramref name="ticks"/> times and returns every WallBounce (with the ActiveTick it happened on).</summary>
        static List<Contact> Run(MatchSim m, int ticks, System.Action<MatchSim, List<Contact>> afterStep = null)
        {
            var list = new List<Contact>();
            for (var i = 0; i < ticks; i++)
            {
                m.Step();
                foreach (var e in m.Events.Where(e => e.Type == SimEventType.WallBounce))
                    list.Add(new Contact { Ball = e.A, Tick = e.Tick, ActiveTick = m.ActiveTick, Damage = e.Value });
                afterStep?.Invoke(m, list);
            }
            return list;
        }

        static MatchSim Two(MatchConfig cfg, FixVec2 p0, FixVec2 v0, FixVec2 p1, FixVec2 v1)
        {
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), p0, 0f), (Stage.L(new HarmlessRule()), p1, 0f));
            m.Balls[0].Vel = v0;
            m.Balls[1].Vel = v1;
            return m;
        }

        [Test]
        public void A_CornerContactOnTwoFacesInConsecutiveTicksCostsThreeOnce()
        {
            var cfg = SpikeConfig();
            var r = cfg.ArenaAt(0);
            // 4 px from the left wall, 3 px under the top: left wall on tick 1, top wall on tick 2 (both inside the TL zone).
            var m = Two(cfg, FT.V(r.Left + R + FT.X(4f), r.Top - R - FT.X(3f)), FT.V(-FT.X(5f), FT.X(2f)), FT.V(FT.X(0f), FT.X(0f)), FT.V(FT.X(0f), FT.X(0f)));
            var hp = m.Balls[0].Hp;
            var contacts = Run(m, 12).Where(c => c.Ball == 0).ToList();
            Assert.That(contacts.Count, Is.EqualTo(2), "two wall faces touched");
            Assert.That(contacts[1].Tick - contacts[0].Tick, Is.EqualTo(1), "on consecutive ticks");
            Assert.That(contacts.Count(c => c.Damage > Fix.Zero), Is.EqualTo(1), "one spike hit");
            Assert.That(hp - m.Balls[0].Hp, Is.EqualTo(Spike), "exactly one spike's damage from the corner, not two");
        }

        [Test]
        public void B_ANewSpikeTouchTenOrMoreActiveTicksLaterHurtsAgain()
        {
            var cfg = Narrow();
            var r = cfg.ArenaAt(0);
            var m = Two(cfg, FT.V(FT.X(0f), r.Top - FT.X(20f)), FT.V(FT.X(6f), FT.X(0f)), FT.V(FT.X(0f), FT.X(0f)), FT.V(FT.X(0f), FT.X(0f)));
            var hp = m.Balls[0].Hp;
            var contacts = Run(m, 80).Where(c => c.Ball == 0).ToList();
            var hits = contacts.Where(c => c.Damage > Fix.Zero).ToList();
            Assert.That(hits.Count, Is.GreaterThanOrEqualTo(3));
            Assert.That(contacts.Count, Is.GreaterThan(hits.Count), "some spike touches were blocked by the immunity");
            foreach (var h in hits) Assert.That(h.Damage, Is.EqualTo(Spike), "a new hit is a full 3");
            for (var i = 1; i < hits.Count; i++)
            {
                var gap = hits[i].ActiveTick - hits[i - 1].ActiveTick;
                Assert.That(gap, Is.GreaterThanOrEqualTo(ArenaTuning.SpikeImmunityTicks), "never inside the 10 active ticks");
                // The first touch once the immunity is over hurts: no touch between the end of the window and the hit.
                Assert.That(contacts.Any(c => c.ActiveTick >= hits[i - 1].ActiveTick + ArenaTuning.SpikeImmunityTicks && c.ActiveTick < hits[i].ActiveTick),
                            Is.False, "first touch after the window takes damage");
            }
            Assert.That(hp - m.Balls[0].Hp, Is.EqualTo(Spike * hits.Count));
        }

        [Test]
        public void C_HitstopDoesNotShortenTheImmunity()
        {
            var cfg = Narrow();
            var r = cfg.ArenaAt(0);
            var m = Two(cfg, FT.V(FT.X(0f), r.Top - FT.X(20f)), FT.V(FT.X(6f), FT.X(0f)), FT.V(FT.X(0f), FT.X(0f)), FT.V(FT.X(0f), FT.X(0f)));
            Contact? first = null;
            const int Hitstop = 8;
            var contacts = Run(m, 80, (sim, list) =>
            {
                if (first != null) return;
                var hit = list.FirstOrDefault(c => c.Ball == 0 && c.Damage > Fix.Zero);
                if (hit.Damage > Fix.Zero) { first = hit; sim.ExtendHitstop(Hitstop); }   // hit-stop starts right after the spike hit
            }).Where(c => c.Ball == 0).ToList();
            Assert.That(first, Is.Not.Null);
            var f = first.Value;
            var next = contacts.First(c => c.Damage > Fix.Zero && c.Tick > f.Tick);
            Assert.That(next.ActiveTick - f.ActiveTick, Is.GreaterThanOrEqualTo(ArenaTuning.SpikeImmunityTicks), "10 ACTIVE ticks");
            Assert.That(next.Tick - f.Tick, Is.GreaterThanOrEqualTo(ArenaTuning.SpikeImmunityTicks + Hitstop), "the hit-stop ticks did not count");
            Assert.That(contacts.Any(c => c.Damage == Fix.Zero && c.Tick - f.Tick >= ArenaTuning.SpikeImmunityTicks
                                          && c.ActiveTick - f.ActiveTick < ArenaTuning.SpikeImmunityTicks),
                        Is.True, "a touch 10+ Ticks but < 10 ActiveTicks after the hit was still blocked");
        }

        [Test]
        public void D_TwoBallsHittingSpikesInTheSameTickBothTakeDamage()
        {
            var cfg = Narrow();
            var r = cfg.ArenaAt(0);
            var m = Two(cfg, FT.V(FT.X(0f), r.Top - FT.X(20f)), FT.V(FT.X(6f), FT.X(0f)), FT.V(FT.X(0f), r.Bottom + FT.X(20f)), FT.V(FT.X(6f), FT.X(0f)));
            var hp0 = m.Balls[0].Hp;
            var hp1 = m.Balls[1].Hp;
            var hits = Run(m, 8).Where(c => c.Damage > Fix.Zero).ToList();
            var firstTick = hits.Min(c => c.Tick);
            var same = hits.Where(c => c.Tick == firstTick).Select(c => c.Ball).OrderBy(b => b).ToList();
            Assert.That(same, Is.EqualTo(new[] { 0, 1 }), "both balls hit on the same tick");
            Assert.That(hp0 - m.Balls[0].Hp, Is.EqualTo(Spike));
            Assert.That(hp1 - m.Balls[1].Hp, Is.EqualTo(Spike), "independent per ball");
        }

        [TestCase(0f, TestName = "E_PillarRightAfterASpikeHitStillBounces")]
        [TestCase(0.15f, TestName = "E_BumperRightAfterASpikeHitStillBouncesAndBoosts")]
        public void E_ObstacleRightAfterASpikeHitStillBounces(float boost)
        {
            var probe = SpikeConfig();
            var r0 = probe.ArenaAt(0);
            var y = r0.Top - FT.X(20f);
            var obstacle = Obstacle.Circle(new FixVec2(r0.Left + FT.X(56f), y), FT.X(8f), Fix.Zero, FT.X(boost));
            var cfg = SpikeConfig(ArenaTuning.SpikeCorners, new[] { obstacle });
            var r = cfg.ArenaAt(0);
            var m = Two(cfg, FT.V(r.Left + R + FT.X(3f), y), FT.V(-FT.X(5f), FT.X(0f)), FT.V(FT.X(0f), r.Bottom + FT.X(40f)), FT.V(FT.X(0f), FT.X(0f)));
            int? spikeAt = null, obstacleAt = null;
            Fix speedBefore = Fix.Zero, vxAfter = Fix.Zero, speedAfter = Fix.Zero;
            for (var i = 0; i < 20 && obstacleAt == null; i++)
            {
                speedBefore = m.Balls[0].Vel.Length;
                m.Step();
                foreach (var e in m.Events)
                {
                    if (e.A != 0) continue;
                    if (e.Type == SimEventType.WallBounce && e.Value > Fix.Zero && spikeAt == null) spikeAt = m.ActiveTick;
                    if (e.Type == SimEventType.ObstacleHit && spikeAt != null && obstacleAt == null)
                    {
                        obstacleAt = m.ActiveTick;
                        Assert.That(e.Value, Is.EqualTo(Fix.Zero), "a harmless obstacle");
                        vxAfter = m.Balls[0].Vel.X;
                        speedAfter = m.Balls[0].Vel.Length;
                    }
                }
            }
            Assert.That(spikeAt, Is.Not.Null, "spike hit first");
            Assert.That(obstacleAt, Is.Not.Null, "then the obstacle");
            Assert.That(obstacleAt.Value - spikeAt.Value, Is.LessThan(ArenaTuning.SpikeImmunityTicks), "inside the immunity window");
            Assert.That(vxAfter, Is.LessThan(Fix.Zero), "bounced back off the obstacle");
            if (boost > 0f) Assert.That(speedAfter, Is.GreaterThan(speedBefore), "the bumper still boosts");
        }
    }
}
