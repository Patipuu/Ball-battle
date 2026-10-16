using System.Linq;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-FIX2 diagnostics (Explicit, report only): the WARDEN-on-bumpers repro (seed 23, forced WARDEN, retry fight).</summary>
    [Explicit("BB-FIX2 diagnostics"), Category("Slow"), NonParallelizable]
    public class BbFix2Diagnostics
    {
        [Test]
        public void WardenBumpersRepro()
        {
            var sb = new StringBuilder();
            var progress = new ProgressData();
            foreach (var weapon in RunState.StartChoices(23, UnlockRules.Weapons(progress)))
            {
                var run = RunState.Start(23, weapon, progress);
                run.ApplyForcedBoss(BossIds.Warden);
                for (var attempt = 0; attempt < 2 && !run.Over; attempt++)
                {
                    var m = run.CreateMatch(ArenaRegistry.Create(ArenaRegistry.Bumpers));
                    sb.AppendLine($"weapon {weapon} fight {run.FightIndex} retry={run.IsRetry} seed={run.FightSeed} arena {m.Config.ArenaWidth.ToFloat()}x{m.Config.ArenaHeight.ToFloat()}");
                    var last = new int[4];
                    var offAt = new int[] { -1, -1, -1, -1 };
                    while (m.Outcome == MatchOutcome.Ongoing)
                    {
                        m.Step();
                        foreach (var e in m.Events)
                        {
                            if (e.Type == SimEventType.ObstacleHit && e.B < 4) last[e.B] = m.ActiveTick;
                            if (e.Type == SimEventType.Boss && (e.Variant == (byte)BossEvent.WardenStep))
                                sb.AppendLine($"  step at active {m.ActiveTick}: arena {m.Arena.Left.ToFloat()}..{m.Arena.Right.ToFloat()} x {m.Arena.Bottom.ToFloat()}..{m.Arena.Top.ToFloat()}");
                        }
                        for (var k = 0; k < 4; k++) if (offAt[k] < 0 && !m.ObstacleActive(k)) offAt[k] = m.ActiveTick;
                    }
                    sb.AppendLine($"  end active {m.ActiveTick} winner {m.WinnerIndex}; last corner-bumper hit per bumper {string.Join(",", last)}; switched off at {string.Join(",", offAt)}");
                    run.ApplyResult(m);
                    if (m.WinnerIndex == 0) break;
                }
            }
            TestContext.Out.WriteLine(sb.ToString());
        }

        /// <summary>Real SHELL KING fights: judged angle (Sim, at the hit) vs the angle the old trace computed (end-of-step boss position).</summary>
        [Test]
        public void ShellTraceDrift()
        {
            int judged = 0, drift1 = 0, misleading = 0;
            double maxDrift = 0;
            var examples = new StringBuilder();
            foreach (var entry in BallBattle.Sim.Weapons.WeaponRegistry.All)
                for (uint seed = 1; seed <= 25; seed++)
                {
                    var shell = new ShellKingRule();
                    var boss = new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("blade")) { Radius = RunTuning.BossRadius, MaxHp = BossTuning.ShellKingHp, Boss = shell };
                    var m = new MatchSim(new MatchConfig(), seed, new[] { new BallLoadout(entry.Create()), boss });
                    while (m.Outcome == MatchOutcome.Ongoing)
                    {
                        m.Step();
                        foreach (var e in m.Events)
                        {
                            if ((e.Type != SimEventType.Hit && e.Type != SimEventType.ProjectileHit) || e.B != 1 || !e.ArcMeasured) continue;
                            judged++;
                            var c = m.Balls[1].Pos;
                            double vx = (e.Point.X - c.X).ToFloat(), vy = (e.Point.Y - c.Y).ToFloat();
                            double fx = shell.Facing.X.ToFloat(), fy = shell.Facing.Y.ToFloat();
                            var old = System.Math.Abs(System.Math.Atan2(vx * fy - vy * fx, vx * fx + vy * fy)) * 180 / System.Math.PI;
                            var d = System.Math.Abs(old - e.ArcAngleTenths / 10.0);
                            if (d > maxDrift) maxDrift = d;
                            if (d > 1) drift1++;
                            if (e.Variant == 0 && old < 60)
                            {
                                misleading++;
                                if (misleading <= 6) examples.AppendLine($"  {entry.Id} seed {seed} tick {e.Tick} {e.Type}: old trace {old:0.0} judged {e.ArcAngleTenths / 10.0:0.0} blocked=false");
                            }
                        }
                    }
                }
            TestContext.Out.WriteLine($"SHELL DRIFT judged={judged} drift>1deg={drift1} max={maxDrift:0.0} unblocked-but-old-trace<60={misleading}");
            TestContext.Out.WriteLine(examples.ToString());
        }
    }
}
