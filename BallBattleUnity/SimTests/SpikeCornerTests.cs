using System;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// BB-FIX1a spike-walls: damage only inside the corner spike zones (1/5 of the current wall length from each spiked corner,
    /// TL + BR), 3 per hit, one per ball per tick; zones follow the shrink. Plus per-arena outcome digests proving every
    /// non-spike arena plays exactly as before the change.
    /// </summary>
    public class SpikeCornerTests
    {
        static readonly Fix R = Fix.FromInt(16);   // MatchConfig.BallRadius

        static MatchConfig SpikeConfig(SpikeCorner corners = ArenaTuning.SpikeCorners)
        {
            var c = Stage.StillConfig();
            c.Layout = new ArenaLayout(null, ArenaTuning.SpikeWallDamage, corners);
            return c;
        }

        /// <summary>Ball 0 starts at <paramref name="start"/> with <paramref name="vel"/>; returns HP lost and the number of damaging wall events.</summary>
        static Fix Bounce(MatchConfig cfg, FixVec2 start, FixVec2 vel, out int hits)
        {
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), start, 0f), (Stage.L(new HarmlessRule()), FT.V(0f, 0f), 0f));
            m.Balls[0].Vel = vel;
            var hp = m.Balls[0].Hp;
            hits = 0;
            for (var i = 0; i < 10; i++)
            {
                m.Step();
                hits += m.Events.Count(e => e.Type == SimEventType.WallBounce && e.A == 0 && e.Value > FT.X(0f));
            }
            return hp - m.Balls[0].Hp;
        }

        static ArenaRect Rect(MatchConfig cfg) => cfg.ArenaAt(0);

        [Test]
        public void WallMidpointIsHarmless()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            Assert.That(Bounce(cfg, FT.V(r.Right - R - FT.X(3f), FT.X(0f)), FT.V(FT.X(5f), FT.X(0f)), out var hits), Is.EqualTo(FT.X(0f)));
            Assert.That(hits, Is.Zero);
            Assert.That(Bounce(cfg, FT.V(FT.X(0f), r.Bottom + R + FT.X(3f)), FT.V(FT.X(0f), -FT.X(5f)), out _), Is.EqualTo(FT.X(0f)), "floor midpoint");
        }

        [Test]
        public void SpikedCornersHurtThreeOthersDoNot()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            Fix Lost(FixVec2 start, FixVec2 vel) => Bounce(cfg, start, vel, out _);
            Assert.That(Lost(FT.V(r.Left + R + FT.X(3f), r.Top - FT.X(20f)), FT.V(-FT.X(5f), FT.X(0f))), Is.EqualTo(FT.X(3f)), "TL zone, left wall");
            Assert.That(Lost(FT.V(r.Left + FT.X(20f), r.Top - R - FT.X(3f)), FT.V(FT.X(0f), FT.X(5f))), Is.EqualTo(FT.X(3f)), "TL zone, top wall");
            Assert.That(Lost(FT.V(r.Right - R - FT.X(3f), r.Bottom + FT.X(20f)), FT.V(FT.X(5f), FT.X(0f))), Is.EqualTo(FT.X(3f)), "BR zone, right wall");
            Assert.That(Lost(FT.V(r.Right - FT.X(20f), r.Bottom + R + FT.X(3f)), FT.V(FT.X(0f), -FT.X(5f))), Is.EqualTo(FT.X(3f)), "BR zone, floor");
            Assert.That(Lost(FT.V(r.Right - R - FT.X(3f), r.Top - FT.X(20f)), FT.V(FT.X(5f), FT.X(0f))), Is.EqualTo(FT.X(0f)), "TR corner");
            Assert.That(Lost(FT.V(r.Left + R + FT.X(3f), r.Bottom + FT.X(20f)), FT.V(-FT.X(5f), FT.X(0f))), Is.EqualTo(FT.X(0f)), "BL corner");
        }

        [Test]
        public void ExactCornerTouchingBothSpikedWallsIsOneHit()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            var lost = Bounce(cfg, FT.V(r.Left + R + FT.X(2f), r.Top - R - FT.X(2f)), FT.V(-FT.X(5f), FT.X(5f)), out var hits);
            Assert.That(lost.F(), Is.EqualTo(3f));
            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void ZonesStayOneFifthOfTheShrunkWalls()
        {
            var cfg = SpikeConfig();
            cfg.ShrinkStartTick = 1;
            cfg.ShrinkDurationTicks = 10;
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), FT.V(-20f, 0f), 0f), (Stage.L(new HarmlessRule()), FT.V(20f, 0f), 0f));
            var before = m.Arena;
            for (var i = 0; i < 20; i++) m.Step();
            var r = m.Arena;
            Assert.That(r.Width, Is.LessThan(before.Width));
            Assert.That(r.Height, Is.LessThan(before.Height));
            var zx = r.Width / FT.X(5f);
            var zy = r.Height / FT.X(5f);
            var e = FT.X(0.25);
            var c = ArenaTuning.SpikeCorners;
            // TL: left wall from the top, top wall from the left.
            Assert.That(SpikeZones.Contains(r, ArenaWall.Left, r.Top - (zy - e), c), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Left, r.Top - (zy + e), c), Is.False);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Top, r.Left + (zx - e), c), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Top, r.Left + (zx + e), c), Is.False);
            // BR: right wall from the bottom, floor from the right.
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Bottom + (zy - e), c), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Bottom + (zy + e), c), Is.False);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Bottom, r.Right - (zx - e), c), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Bottom, r.Right - (zx + e), c), Is.False);
        }

        [Test]
        public void AllFourCornersSwitch()
        {
            var r = Rect(SpikeConfig());
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Top - FT.X(20f), ArenaTuning.SpikeCorners), Is.False, "TR off by default");
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Top - FT.X(20f), SpikeCorner.All), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Left, r.Bottom + FT.X(20f), SpikeCorner.All), Is.True);
            var all = SpikeConfig(SpikeCorner.All);
            Assert.That(Bounce(all, FT.V(r.Right - R - FT.X(3f), r.Top - FT.X(20f)), FT.V(FT.X(5f), FT.X(0f)), out _), Is.EqualTo(FT.X(3f)), "TR with All");
            Assert.That(Bounce(all, FT.V(r.Left + R + FT.X(3f), r.Bottom + FT.X(20f)), FT.V(-FT.X(5f), FT.X(0f)), out _), Is.EqualTo(FT.X(3f)), "BL with All");
            Assert.That(Bounce(all, FT.V(r.Right - R - FT.X(3f), FT.X(0f)), FT.V(FT.X(5f), FT.X(0f)), out _), Is.EqualTo(FT.X(0f)), "midpoint still harmless");
        }

        [Test]
        public void SpikeArenaIsDeterministic()
        {
            foreach (var (a, b, seed) in new[] { ("blade", "fang", 7u), ("rig", "volley", 3u), ("brawler", "pike", 11u) })
            {
                MatchSim Run()
                {
                    var m = new MatchSim(ArenaRegistry.Create(ArenaRegistry.SpikeWalls), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
                    while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                    return m;
                }
                Assert.That(Run().ComputeHash(), Is.EqualTo(Run().ComputeHash()), $"{a} vs {b} seed {seed}");
            }
        }

        // ---- per-arena outcome digests (independent of SimVersion.Rules) ----

        /// <summary>
        /// Per-arena digests, re-pinned for the fixed-point sim (6a.6b/c; the digest now mixes the Fix raw HP values). Every
        /// arena is pinned exactly; old (float, BB-FIX1a) → new values are listed in docs/claude-runs/6a6bc-fix.md.
        /// </summary>
        static readonly (string arena, ulong digest)[] ExpectedByArena =
        {
            ("classic", 0x7BEA200668ED5694UL),
            ("pillar", 0x9BAA96D99356D870UL),
            ("bumpers", 0xB76BDBC50DCFB6D0UL),
            ("spike-walls", 0x81D75579528D171BUL),
            ("low-gravity", 0x265057B248B104E2UL),
            ("tight", 0x87CD89A941BEDBEAUL),
            ("wide", 0xBBFD43849E96D984UL),
        };

        /// <summary>Winner, length and both final HPs of 8 weapons vs blade, seeds 1-2, on one arena.</summary>
        public static ulong ArenaDigest(string arenaId)
        {
            var h = 14695981039346656037UL;
            foreach (var w in WeaponRegistry.All)
                for (uint seed = 1; seed <= 2; seed++)
                {
                    var m = new MatchSim(ArenaRegistry.Create(arenaId), seed, new[] { w.Create(), WeaponRegistry.Create("blade") });
                    while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                    h = Mix(h, (ulong)(m.WinnerIndex + 2));
                    h = Mix(h, (ulong)m.Tick);
                    h = Mix(h, (ulong)(uint)m.Balls[0].Hp.Raw);
                    h = Mix(h, (ulong)(uint)m.Balls[1].Hp.Raw);
                }
            return h;
        }

        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;

        [Test]
        public void EveryArenaMatchesItsDigest()
        {
            var actual = ExpectedByArena.Select(e => (e.arena, digest: ArenaDigest(e.arena))).ToArray();
            foreach (var (arena, digest) in actual) TestContext.Out.WriteLine($"ARENA DIGEST {arena}: 0x{digest:X16}UL");
            Assert.That(actual, Is.EqualTo(ExpectedByArena));
        }
    }
}
