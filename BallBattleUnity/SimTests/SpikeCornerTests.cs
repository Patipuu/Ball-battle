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
        const float R = 16f;   // MatchConfig.BallRadius

        static MatchConfig SpikeConfig(SpikeCorner corners = ArenaTuning.SpikeCorners)
        {
            var c = Stage.StillConfig();
            c.Layout = new ArenaLayout(null, ArenaTuning.SpikeWallDamage, corners);
            return c;
        }

        /// <summary>Ball 0 starts at <paramref name="start"/> with <paramref name="vel"/>; returns HP lost and the number of damaging wall events.</summary>
        static float Bounce(MatchConfig cfg, Vec2 start, Vec2 vel, out int hits)
        {
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), start, 0f), (Stage.L(new HarmlessRule()), new Vec2(0f, 0f), 0f));
            m.Balls[0].Vel = vel;
            var hp = m.Balls[0].Hp;
            hits = 0;
            for (var i = 0; i < 10; i++)
            {
                m.Step();
                hits += m.Events.Count(e => e.Type == SimEventType.WallBounce && e.A == 0 && e.Value > 0f);
            }
            return hp - m.Balls[0].Hp;
        }

        static ArenaRect Rect(MatchConfig cfg) => cfg.ArenaAt(0);

        [Test]
        public void WallMidpointIsHarmless()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            Assert.That(Bounce(cfg, new Vec2(r.Right - R - 3f, 0f), new Vec2(5f, 0f), out var hits), Is.EqualTo(0f));
            Assert.That(hits, Is.Zero);
            Assert.That(Bounce(cfg, new Vec2(0f, r.Bottom + R + 3f), new Vec2(0f, -5f), out _), Is.EqualTo(0f), "floor midpoint");
        }

        [Test]
        public void SpikedCornersHurtThreeOthersDoNot()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            float Lost(Vec2 start, Vec2 vel) => Bounce(cfg, start, vel, out _);
            Assert.That(Lost(new Vec2(r.Left + R + 3f, r.Top - 20f), new Vec2(-5f, 0f)), Is.EqualTo(3f), "TL zone, left wall");
            Assert.That(Lost(new Vec2(r.Left + 20f, r.Top - R - 3f), new Vec2(0f, 5f)), Is.EqualTo(3f), "TL zone, top wall");
            Assert.That(Lost(new Vec2(r.Right - R - 3f, r.Bottom + 20f), new Vec2(5f, 0f)), Is.EqualTo(3f), "BR zone, right wall");
            Assert.That(Lost(new Vec2(r.Right - 20f, r.Bottom + R + 3f), new Vec2(0f, -5f)), Is.EqualTo(3f), "BR zone, floor");
            Assert.That(Lost(new Vec2(r.Right - R - 3f, r.Top - 20f), new Vec2(5f, 0f)), Is.EqualTo(0f), "TR corner");
            Assert.That(Lost(new Vec2(r.Left + R + 3f, r.Bottom + 20f), new Vec2(-5f, 0f)), Is.EqualTo(0f), "BL corner");
        }

        [Test]
        public void ExactCornerTouchingBothSpikedWallsIsOneHit()
        {
            var cfg = SpikeConfig();
            var r = Rect(cfg);
            var lost = Bounce(cfg, new Vec2(r.Left + R + 2f, r.Top - R - 2f), new Vec2(-5f, 5f), out var hits);
            Assert.That(lost, Is.EqualTo(3f));
            Assert.That(hits, Is.EqualTo(1));
        }

        [Test]
        public void ZonesStayOneFifthOfTheShrunkWalls()
        {
            var cfg = SpikeConfig();
            cfg.ShrinkStartTick = 1;
            cfg.ShrinkDurationTicks = 10;
            var m = Stage.Build(cfg, (Stage.L(new HarmlessRule()), new Vec2(-20f, 0f), 0f), (Stage.L(new HarmlessRule()), new Vec2(20f, 0f), 0f));
            var before = m.Arena;
            for (var i = 0; i < 20; i++) m.Step();
            var r = m.Arena;
            Assert.That(r.Width, Is.LessThan(before.Width));
            Assert.That(r.Height, Is.LessThan(before.Height));
            var zx = r.Width / 5f;
            var zy = r.Height / 5f;
            const float e = 0.25f;
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
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Top - 20f, ArenaTuning.SpikeCorners), Is.False, "TR off by default");
            Assert.That(SpikeZones.Contains(r, ArenaWall.Right, r.Top - 20f, SpikeCorner.All), Is.True);
            Assert.That(SpikeZones.Contains(r, ArenaWall.Left, r.Bottom + 20f, SpikeCorner.All), Is.True);
            var all = SpikeConfig(SpikeCorner.All);
            Assert.That(Bounce(all, new Vec2(r.Right - R - 3f, r.Top - 20f), new Vec2(5f, 0f), out _), Is.EqualTo(3f), "TR with All");
            Assert.That(Bounce(all, new Vec2(r.Left + R + 3f, r.Bottom + 20f), new Vec2(-5f, 0f), out _), Is.EqualTo(3f), "BL with All");
            Assert.That(Bounce(all, new Vec2(r.Right - R - 3f, 0f), new Vec2(5f, 0f), out _), Is.EqualTo(0f), "midpoint still harmless");
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
        /// Recorded at ff7ab50 (before BB-FIX1a) for every arena; non-spike arenas must stay identical.
        /// spike-walls changed on purpose (corner zones, 3 damage): see docs/claude-runs/bbfix1a-spikes.md.
        /// </summary>
        static readonly (string arena, ulong digest)[] ExpectedByArena =
        {
            ("classic", 0x6F2265EA3A4812DFUL),
            ("pillar", 0x3C8F87B27D8DB9B5UL),
            ("bumpers", 0x447293047E50C7E1UL),
            ("spike-walls", 0xFCE4FB1B969DAAF0UL),
            ("low-gravity", 0x26A829E997CF5D92UL),
            ("tight", 0xA8649BD7FB44D43CUL),
            ("wide", 0x56C07FC05082E5A8UL),
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
                    h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(m.Balls[0].Hp));
                    h = Mix(h, (ulong)(uint)BitConverter.SingleToInt32Bits(m.Balls[1].Hp));
                }
            return h;
        }

        static ulong Mix(ulong h, ulong v) => (h ^ v) * 1099511628211UL;

        [Test]
        public void NonSpikeArenasPlayExactlyAsBefore()
        {
            foreach (var (arena, expected) in ExpectedByArena)
            {
                var actual = ArenaDigest(arena);
                TestContext.Out.WriteLine($"ARENA DIGEST {arena}: 0x{actual:X16}");
                if (arena == ArenaRegistry.SpikeWalls)
                    Assert.That(actual, Is.Not.EqualTo(expected), "spike-walls is meant to change");
                else
                    Assert.That(actual, Is.EqualTo(expected), arena);
            }
        }
    }
}
