using System.Globalization;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-6d QA: one line per Weapon/Projectile hit on SHELL KING while its shell is ON, with the contact angle relative to the shell's
    /// facing (degrees, 0 = dead centre, 180 = its back) and whether the shell stopped it. Written to the -bbTrace file and, with
    /// -bbAudioLog, to the audio log (ArenaView / AudioDirector).
    /// BB-FIX2: the angle is the one the Sim's block test used (SimEvent.ArcAngleTenths, measured against the boss position at the moment
    /// of the hit). It used to be recomputed here from the boss position at the END of the step; balls move after the contact (later
    /// substeps, collisions, walls), so the logged angle drifted from the judged one by several degrees.
    /// </summary>
    public static class ShellTrace
    {
        /// <summary>The trace line for <paramref name="e"/>, or null when it is not a hit on SHELL KING with the shell ON.</summary>
        public static string Line(SimEvent e, MatchSim sim)
        {
            if (sim == null || (e.Type != SimEventType.Hit && e.Type != SimEventType.ProjectileHit)) return null;
            if (!(sim.Boss is ShellKingRule) || !e.ArcMeasured || e.B != sim.BossBallIndex) return null;
            var angle = e.ArcAngleTenths / 10f;
            return string.Format(CultureInfo.InvariantCulture,
                "[Shell] {0} tick={1} active={2} attacker={3} angle={4:0.0} blocked={5} dmg={6:0.00}",
                e.Type, e.Tick, sim.ActiveTick, e.A, angle, e.Variant == 1 ? "true" : "false", e.Value.ToFloat());
        }

        /// <summary>(Display helper, not the block test.) Unsigned angle in degrees between <paramref name="facing"/> and the direction from <paramref name="centre"/> to <paramref name="point"/>.</summary>
        public static float AngleDeg(FixVec2 centre, FixVec2 facing, FixVec2 point)
        {
            var v = new Vector2((point.X - centre.X).ToFloat(), (point.Y - centre.Y).ToFloat());
            var f = new Vector2(facing.X.ToFloat(), facing.Y.ToFloat());
            return v.sqrMagnitude <= 0f ? 0f : Vector2.Angle(f, v);
        }
    }
}
