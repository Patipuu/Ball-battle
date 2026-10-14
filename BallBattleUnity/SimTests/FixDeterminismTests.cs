using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>6a.6b/c part B: determinism on every arena, and spike zones that stay 1/5 of the shrinking walls.</summary>
    public class FixDeterminismTests
    {
        static readonly uint[] Seeds = { 11u, 222u, 3333u };

        static (ulong hash, int tick, int winner) Play(string arena, string a, string b, uint seed)
        {
            var m = new MatchSim(ArenaRegistry.Create(arena), seed, new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) });
            var h = SimHash.Seed;
            while (m.Outcome == MatchOutcome.Ongoing)
            {
                m.Step();
                if (m.Tick % 60 == 0) h = SimHash.Mix(h, unchecked((uint)m.ComputeHash()));   // the whole path, not just the end
            }
            return (SimHash.Mix(h, unchecked((uint)(m.ComputeHash() >> 32))), m.Tick, m.WinnerIndex);
        }

        [Test]
        public void EveryArenaThreeSeedsRunTwiceSameHash()
        {
            var ids = WeaponRegistry.All.Select(e => e.Id).ToArray();
            var k = 0;
            foreach (var arena in ArenaRegistry.All.Select(e => e.Id))
                foreach (var seed in Seeds)
                {
                    var a = ids[k % ids.Length];
                    var b = ids[(k * 3 + 1) % ids.Length];
                    k++;
                    var first = Play(arena, a, b, seed);
                    var second = Play(arena, a, b, seed);
                    Assert.That(second, Is.EqualTo(first), $"{arena} {a} vs {b} seed {seed}");
                }
        }

        [Test]
        public void SpikeZonesStayOneFifthDuringAndAfterTheShrink()
        {
            var cfg = ArenaRegistry.Create(ArenaRegistry.SpikeWalls);
            Assert.That((cfg.ShrinkStartTick, cfg.ShrinkDurationTicks, cfg.CapTicks), Is.EqualTo((5400, 1800, 10800)), "shrink timing unchanged");
            var corners = cfg.Layout.SpikeCorners;
            var start = cfg.ArenaAt(0);
            var ticks = new[] { 0, cfg.ShrinkStartTick, cfg.ShrinkStartTick + 1, cfg.ShrinkStartTick + cfg.ShrinkDurationTicks / 3,
                                cfg.ShrinkStartTick + cfg.ShrinkDurationTicks / 2, cfg.ShrinkStartTick + cfg.ShrinkDurationTicks - 1,
                                cfg.ShrinkStartTick + cfg.ShrinkDurationTicks, cfg.CapTicks };
            foreach (var t in ticks)
            {
                var r = cfg.ArenaAt(t);
                if (t > cfg.ShrinkStartTick) Assert.That(r.Width, Is.LessThan(start.Width), $"tick {t}: walls closed in");
                var segs = SpikeZones.Segments(r, corners);
                Assert.That(segs.Length, Is.EqualTo(4), "TL + BR, two walls each");
                foreach (var s in segs)
                {
                    var wall = s.Horizontal ? r.Width : r.Height;
                    Assert.That(s.Length, Is.EqualTo(SpikeZones.ZoneLength(wall)), $"tick {t} {s.Wall}");
                    Assert.That((long)s.Length.Raw * ArenaTuning.SpikeZoneDen, Is.LessThanOrEqualTo((long)wall.Raw * ArenaTuning.SpikeZoneNum));
                    Assert.That((long)(s.Length.Raw + 1) * ArenaTuning.SpikeZoneDen, Is.GreaterThan((long)wall.Raw * ArenaTuning.SpikeZoneNum), "largest Fix within 1/5");
                    // The inner end of the zone is the last point the sim's contact test counts; one raw unit further is out.
                    var inner = InnerEnd(r, s, out var step);
                    Assert.That(SpikeZones.Contains(r, s.Wall, inner, corners), Is.True, $"tick {t} {s.Wall} inner end");
                    Assert.That(SpikeZones.Contains(r, s.Wall, inner + step, corners), Is.False, $"tick {t} {s.Wall} past the end");
                }
            }

            // A real match on the arena reports the same rect as the config at that tick.
            var m = new MatchSim(cfg, 5u, new[] { WeaponRegistry.Create(WeaponRegistry.All[0].Id), WeaponRegistry.Create(WeaponRegistry.All[1].Id) });
            for (var i = 0; i < 20; i++) m.Step();
            Assert.That(m.Arena.Width, Is.EqualTo(cfg.ArenaAt(m.ActiveTick).Width));
        }

        /// <summary>End of the zone away from its corner, and the direction (1 raw unit) that leaves the zone.</summary>
        static Fix InnerEnd(in ArenaRect r, SpikeSegment s, out Fix step)
        {
            var nearLow = s.Horizontal ? s.From == r.Left : s.From == r.Bottom;   // zone starts at the low corner
            step = nearLow ? Fix.Epsilon : -Fix.Epsilon;
            return nearLow ? s.To : s.From;
        }
    }

}
