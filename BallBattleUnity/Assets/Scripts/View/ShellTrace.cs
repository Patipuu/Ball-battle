using System.Globalization;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-6d QA: one line per Weapon/Projectile hit on SHELL KING while its shell is ON, with the contact angle relative to the shell's
    /// facing (degrees, 0 = dead centre, 180 = its back) and whether the shell stopped it. Read-only View code built from data the Sim
    /// already exposes (the event's contact Point and Variant, ShellKingRule.Facing, the boss position): no new Sim field, so no hash
    /// or golden changes. Written to the -bbTrace file and, with -bbAudioLog, to the audio log (ArenaView / AudioDirector).
    /// </summary>
    public static class ShellTrace
    {
        /// <summary>The trace line for <paramref name="e"/>, or null when it is not a hit on SHELL KING with the shell ON.</summary>
        public static string Line(SimEvent e, MatchSim sim)
        {
            if (sim == null || (e.Type != SimEventType.Hit && e.Type != SimEventType.ProjectileHit)) return null;
            if (!(sim.Boss is ShellKingRule shell) || !shell.ShellOn || e.B != sim.BossBallIndex) return null;
            var boss = sim.Balls[e.B];
            var angle = AngleDeg(boss.Pos, shell.Facing, e.Point);
            return string.Format(CultureInfo.InvariantCulture,
                "[Shell] {0} tick={1} active={2} attacker={3} angle={4:0.0} blocked={5} dmg={6:0.00}",
                e.Type, e.Tick, sim.ActiveTick, e.A, angle, e.Variant == 1 ? "true" : "false", e.Value.ToFloat());
        }

        /// <summary>Unsigned angle in degrees between <paramref name="facing"/> and the direction from <paramref name="centre"/> to <paramref name="point"/>.</summary>
        public static float AngleDeg(FixVec2 centre, FixVec2 facing, FixVec2 point)
        {
            var v = new Vector2((point.X - centre.X).ToFloat(), (point.Y - centre.Y).ToFloat());
            var f = new Vector2(facing.X.ToFloat(), facing.Y.ToFloat());
            return v.sqrMagnitude <= 0f ? 0f : Vector2.Angle(f, v);
        }
    }
}
