using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-FIX2 (Rules 11): SHELL KING arc edge (Bug C) and obstacles buried by moving walls in boss fights (Bug B).</summary>
    public class BbFix2Tests
    {
        static readonly Fix BossR = RunTuning.BossRadius;

        static void StepTo(MatchSim m, int activeTick, System.Action<MatchSim> after = null)
        {
            while (m.ActiveTick < activeTick) { m.Step(); after?.Invoke(m); }
        }

        // ---------------- Bug C: SHELL KING arc ----------------

        /// <summary>Attacker (ball 0) far left while the shell turns on, so it faces (-1, 0); SHELL KING (ball 1) at the centre.</summary>
        static (MatchSim m, ShellKingRule shell) ShellFight(WeaponRule weapon)
        {
            var shell = new ShellKingRule();
            var m = Stage.Build(Stage.StillConfig(),
                (new BallLoadout(weapon) { MaxHp = 9999 }, FT.V(-90, 0), 0f),
                (new BallLoadout(new HarmlessRule()) { Radius = BossR, MaxHp = 9999, Boss = shell }, FT.V(0, 0), 0f));
            StepTo(m, BossTuning.ShellFirstWarnTick + BossTuning.ShellWarnTicks, s => Stage.Park(s, FT.V(-90, 0), FT.V(0, 0)));
            Assert.That(shell.ShellOn, Is.True);
            Assert.That((shell.Facing.X, shell.Facing.Y), Is.EqualTo((-Fix.One, Fix.Zero)));
            return (m, shell);
        }

        /// <summary>Unit direction <paramref name="tenths"/> (0.1 degree) off the shell's facing (-1, 0), on the upper side.</summary>
        static FixVec2 Off(int tenths) => new FixVec2(-FixedTrig.Cos(tenths), FixedTrig.Sin(tenths));

        /// <summary>One WRECKER body blow from <paramref name="tenths"/> off the facing: the contact point lies on the line between the
        /// centres, so its angle is exactly that direction. Returns the attacker's Hit event on the boss.</summary>
        static (SimEvent hit, MatchSim m, BrawlerRule brawler) BodyBlow(int tenths)
        {
            var brawler = (BrawlerRule)WeaponRegistry.Create(BrawlerRule.WeaponId);
            var (m, _) = ShellFight(brawler);
            var at = Off(tenths) * (BossR + m.Balls[0].Radius - 1);
            Stage.Park(m, at, FT.V(0, 0));
            m.Step();
            var hits = m.Events.Where(e => e.Type == SimEventType.Hit && e.A == 0 && e.B == 1).ToList();
            Assert.That(hits.Count, Is.EqualTo(1), "one body contact");
            return (hits[0], m, brawler);
        }

        [TestCase(599, true)]
        [TestCase(600, true)]
        [TestCase(601, false)]
        public void ArcEdgeIsSixtyDegreesAndTheEventCarriesTheJudgedAngle(int tenths, bool blocked)
        {
            var (hit, _, _) = BodyBlow(tenths);
            Assert.That(hit.ArcMeasured, Is.True);
            Assert.That(hit.ArcAngleTenths, Is.EqualTo(tenths), "the angle the block test used, on the event");
            Assert.That(hit.Variant == 1, Is.EqualTo(blocked), $"{tenths / 10f} degrees: blocked={blocked}");
        }

        [Test]
        public void ContactAngleIsMeasuredFromTheBossCentreAgainstTheFacing()
        {
            var (m, shell) = ShellFight(new HarmlessRule());
            var c = m.Balls[1].Pos;
            Assert.That(shell.ContactAngleTenths(c + Off(599) * 30), Is.EqualTo(599));
            Assert.That(shell.ContactAngleTenths(c + Off(601) * 30), Is.EqualTo(601));
            Assert.That(shell.ContactAngleTenths(c + new FixVec2(Fix.Zero, -Fix.FromInt(30))), Is.EqualTo(900), "below: 90 degrees too");
            Assert.That(shell.ContactAngleTenths(c + FT.V(30, 0)), Is.EqualTo(1800), "its back");
            Assert.That(shell.InArc(c + Off(599) * 30), Is.True);
            Assert.That(shell.InArc(c + Off(601) * 30), Is.False);
        }

        [Test]
        public void BlockedWreckerBlowDoesNotGrowItsDamageOrSpeed()
        {
            var (inside, m, brawler) = BodyBlow(599);
            Assert.That(inside.Variant, Is.EqualTo(1));
            Assert.That(brawler.DamageFactor, Is.EqualTo(WeaponTuning.BrawlerStartDamagePerSpeed), "no damage growth");
            Assert.That(brawler.MaxSpeedBonus, Is.EqualTo(WeaponTuning.BrawlerStartSpeedBonus), "no speed growth");
            Assert.That(m.Balls[0].HitCount, Is.Zero);
            var (outside, _, grown) = BodyBlow(601);
            Assert.That(outside.Variant, Is.EqualTo(0));
            Assert.That(grown.DamageFactor, Is.GreaterThan(WeaponTuning.BrawlerStartDamagePerSpeed), "control: an unblocked blow grows it");
        }

        [Test]
        public void BlockedEmberHitDoesNotGrowItsDamage()
        {
            foreach (var (deg, blocked) in new[] { (30, true), (90, false) })
            {
                var blade = (BladeRule)WeaponRegistry.Create(BladeRule.WeaponId);
                blade.SpinDegPerTick = Fix.Zero;
                var (m, shell) = ShellFight(blade);
                var u = Off(deg * 10);
                var reach = blade.BladeInner + blade.BladeLength + BossR - 2;   // the tip sits 2 px inside the boss: contact = tip
                var at = u * reach;
                var events = new List<SimEvent>();
                for (var i = 0; i < 3; i++)
                {
                    Stage.Park(m, at, FT.V(0, 0));
                    m.Balls[0].WeaponAngleDeg = Fix.FromInt(360 - deg);   // pointing back at the boss centre
                    m.Step();
                    events.AddRange(m.Events.Where(e => e.Type == SimEventType.Hit && e.A == 0));
                }
                Assert.That(events, Is.Not.Empty, $"{deg}: the blade touched");
                Assert.That(events.All(e => e.ArcMeasured && (e.Variant == 1) == (e.ArcAngleTenths <= 600)), Is.True);
                if (blocked)
                {
                    Assert.That(events.All(e => e.Variant == 1), Is.True);
                    Assert.That(blade.CurrentDamage, Is.EqualTo(WeaponTuning.BladeStartDamage), "no damage growth after blocked hits");
                }
                else Assert.That(blade.CurrentDamage, Is.GreaterThan(WeaponTuning.BladeStartDamage), "control: unblocked hits grow it");
            }
        }

        /// <summary>
        /// Real fights (balls moving, substeps, collisions after the contact): every hit SHELL KING's shell judged is blocked exactly
        /// when its logged angle is at most 60.0 degrees, for melee and shots. Before BB-FIX2 the logged angle was recomputed after the
        /// step and could sit several degrees off the judged one.
        /// </summary>
        [Test]
        public void InRealFightsBlockedEqualsLoggedAngleAtMostSixty()
        {
            var judged = 0;
            foreach (var entry in WeaponRegistry.All)
                for (uint seed = 1; seed <= 4; seed++)
                {
                    var shell = new ShellKingRule();
                    var boss = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { Radius = BossR, MaxHp = BossTuning.ShellKingHp, Boss = shell };
                    var m = new MatchSim(new MatchConfig(), seed, new[] { new BallLoadout(entry.Create()), boss });
                    while (m.Outcome == MatchOutcome.Ongoing)
                    {
                        m.Step();
                        foreach (var e in m.Events)
                        {
                            if ((e.Type != SimEventType.Hit && e.Type != SimEventType.ProjectileHit) || e.B != 1 || !e.ArcMeasured) continue;
                            judged++;
                            Assert.That(e.Variant == 1, Is.EqualTo(e.ArcAngleTenths <= 600), $"{entry.Id} seed {seed} tick {e.Tick}: angle {e.ArcAngleTenths / 10f}");
                        }
                    }
                }
            Assert.That(judged, Is.GreaterThan(100), "enough judged hits");
        }

        // ---------------- Bug B: obstacles buried by moving walls ----------------

        static readonly Fix Diameter = Fix.FromInt(32);   // the smallest ball (player radius 16)

        /// <summary>The repro: -bbForceBoss warden -bbForceArena bumpers, seed 23 (every start weapon, the fight and its retry). The four
        /// corner bumpers are buried by step 1: switched off at ActiveTick 450 and never hit afterwards.</summary>
        [Test]
        public void WardenSeed23BumpersBuriedAtStepOne()
        {
            var progress = new ProgressData();
            var fights = 0;
            foreach (var weapon in RunState.StartChoices(23, UnlockRules.Weapons(progress)))
            {
                var run = RunState.Start(23, weapon, progress);
                run.ApplyForcedBoss(BossIds.Warden);
                for (var attempt = 0; attempt < 2 && !run.Over; attempt++)
                {
                    var m = run.CreateMatch(ArenaRegistry.Create(ArenaRegistry.Bumpers));
                    var lastHit = -1;
                    var offAt = new[] { -1, -1, -1, -1 };
                    while (m.Outcome == MatchOutcome.Ongoing)
                    {
                        m.Step();
                        foreach (var e in m.Events) if (e.Type == SimEventType.ObstacleHit && e.B < 4) lastHit = System.Math.Max(lastHit, m.ActiveTick);
                        for (var k = 0; k < 4; k++) if (offAt[k] < 0 && !m.ObstacleActive(k)) offAt[k] = m.ActiveTick;
                    }
                    var label = $"{weapon} retry={run.IsRetry} ended {m.ActiveTick}";
                    if (m.ActiveTick >= WardenRule.StepTick(1))
                        Assert.That(offAt, Is.EqualTo(new[] { 450, 450, 450, 450 }), label + ": off on the step-1 tick");
                    Assert.That(lastHit, Is.LessThanOrEqualTo(WardenRule.StepTick(1)), label + $": last corner-bumper hit {lastHit}");
                    fights++;
                    var retry = !run.IsRetry;
                    run.ApplyResult(m);
                    if (m.WinnerIndex == 0 || !retry) break;
                }
            }
            Assert.That(WardenRule.StepTick(1), Is.EqualTo(450));
            Assert.That(fights, Is.GreaterThanOrEqualTo(3));
        }

        static (MatchSim m, WardenRule warden) WardenFight(ArenaLayout layout)
        {
            var cfg = Stage.StillConfig();
            cfg.Layout = layout;
            var warden = new WardenRule();
            var m = Stage.Build(cfg, (new BallLoadout(new HarmlessRule()), FT.V(-60, 30), 0f),
                (new BallLoadout(new HarmlessRule()) { Radius = BossR, MaxHp = 9999, Boss = warden }, FT.V(-40, -40), 0f));
            return (m, warden);
        }

        /// <summary>Ball 0 placed overlapping pillar 0 from the left and moving into it; the other ball parked away.</summary>
        static void IntoPillar(MatchSim m)
        {
            Stage.Park(m, FT.V(100 - 14 - 16 + 2, 0), FT.V(-40, -40));
            m.Balls[0].Vel = FT.V(2, 0);
        }

        [Test]
        public void PillarCoveredByAWardenStepStopsCollidingThatTick()
        {
            // Pillar 0 at (100, 0) r14: right gap 115 - 114 = 1 at the start (the start layout is never switched off), -9.35 after step 1.
            // Pillar 1 at the centre never comes near a wall (gap >= 50 at the smallest WARDEN walls).
            var layout = new ArenaLayout(new[] { Obstacle.Circle(FT.V(100, 0), 14), Obstacle.Circle(FT.V(0, 0), 14) });
            var step = WardenRule.StepTick(1);

            var (control, _) = WardenFight(layout);
            StepTo(control, step - 2, s => Stage.Park(s, FT.V(-60, 30), FT.V(-40, -40)));
            Assert.That(control.ObstacleActive(0), Is.True, "untouched before the step");
            IntoPillar(control);
            control.Step();
            Assert.That(control.ActiveTick, Is.EqualTo(step - 1));
            Assert.That(control.Events.Any(e => e.Type == SimEventType.ObstacleHit && e.B == 0), Is.True, "control: the tick before, it collides");

            var (m, _) = WardenFight(layout);
            StepTo(m, step - 1, s => Stage.Park(s, FT.V(-60, 30), FT.V(-40, -40)));
            IntoPillar(m);
            m.Step();
            Assert.That(m.ActiveTick, Is.EqualTo(step));
            Assert.That(m.ObstacleActive(0), Is.False, "buried by step 1");
            Assert.That(m.Events.Any(e => e.Type == SimEventType.ObstacleHit && e.B == 0), Is.False, "no collision on the step tick");
            StepTo(m, WardenRule.StepTick(BossTuning.WardenMaxSteps) + 10, s => Stage.Park(s, FT.V(-40, 30), FT.V(-30, -30)));
            Assert.That(m.ObstacleActive(1), Is.True, "the centre pillar is never buried");
        }

        /// <summary>The normal shrink after ShrinkStartTick follows the same rule in a boss fight (SHELL KING here): every obstacle goes
        /// off on the first ActiveTick its moved wall leaves a gap below a ball diameter, computed from MatchConfig.ArenaAt.</summary>
        [Test]
        public void NormalShrinkBuriesObstaclesOnTheSameTick()
        {
            var cfg = Stage.StillConfig();
            cfg.ShrinkStartTick = 5400;
            var bumpers = ArenaRegistry.Create(ArenaRegistry.Bumpers).Layout;
            var obstacles = Enumerable.Range(0, bumpers.ObstacleCount).Select(k => bumpers.Get(k)).ToList();
            obstacles.Add(Obstacle.Circle(FT.V(0, -50), 8));
            cfg.Layout = new ArenaLayout(obstacles.ToArray());
            var shell = new ShellKingRule();
            var m = Stage.Build(cfg, (new BallLoadout(new HarmlessRule()), FT.V(-20, 20), 0f),
                (new BallLoadout(new HarmlessRule()) { Radius = BossR, MaxHp = 9999, Boss = shell }, FT.V(25, 20), 0f));

            // Expected tick per obstacle: first t > ShrinkStartTick where a moved wall leaves less than a diameter.
            int Expected(Obstacle o)
            {
                for (var t = cfg.ShrinkStartTick + 1; t <= cfg.ShrinkStartTick + cfg.ShrinkDurationTicks; t++)
                {
                    var a = cfg.ArenaAt(t);
                    if (a.Right - (o.A.X + o.Radius) < Diameter || (o.A.X - o.Radius) - a.Left < Diameter
                        || a.Top - (o.A.Y + o.Radius) < Diameter || (o.A.Y - o.Radius) - a.Bottom < Diameter) return t;
                }
                return -1;
            }
            var expected = obstacles.Select(Expected).ToArray();
            Assert.That(expected.Take(4).All(t => t == 5401), Is.True, "corner bumpers: the first moving tick");
            Assert.That(expected[4], Is.GreaterThan(5401));

            var offAt = Enumerable.Repeat(-1, obstacles.Count).ToArray();
            while (m.ActiveTick < cfg.ShrinkStartTick + cfg.ShrinkDurationTicks)
            {
                m.Step();
                Stage.Park(m, FT.V(-20, 20), FT.V(25, 20));
                for (var k = 0; k < obstacles.Count; k++) if (offAt[k] < 0 && !m.ObstacleActive(k)) offAt[k] = m.ActiveTick;
            }
            Assert.That(offAt, Is.EqualTo(expected));
        }
    }
}
