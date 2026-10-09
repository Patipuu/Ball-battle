using System;

namespace BallBattle.Sim
{
    /// <summary>Substep budget and ball integration.</summary>
    public sealed partial class MatchSim
    {
        /// <summary>
        /// Enough substeps that per substep: no blade turns more than MaxSpinPerSubstepDeg, no ball moves
        /// more than half its radius, two blade tips cannot close faster than the parry reach (otherwise
        /// fast/long blades could pass through each other without a parry), and projectiles alive at the
        /// start of the tick move less than their radius plus the thinnest blade/obstacle half-width.
        /// Capped at MaxSubsteps.
        /// </summary>
        int SubstepCount()
        {
            var maxSpin = 0f;
            var maxSpeed = 0f;
            var maxTipSpeed = 0f;
            var minParryReach = float.MaxValue;
            var minRadius = float.MaxValue;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var speed = b.Vel.Length;
                if (speed > maxSpeed) maxSpeed = speed;
                if (b.Radius < minRadius) minRadius = b.Radius;
                var w = b.Weapon;
                if (!w.HasBlade) continue;
                if (w.SpinDegPerTick > maxSpin) maxSpin = w.SpinDegPerTick;
                var tip = w.SpinDegPerTick * (MathF.PI / 180f) * (w.BladeInner + b.BladeShift + w.BladeLength);
                if (tip > maxTipSpeed) maxTipSpeed = tip;
                if (w.BladeThickness < minParryReach) minParryReach = w.BladeThickness;
            }

            var bySpin = (int)MathF.Ceiling(maxSpin / Config.MaxSpinPerSubstepDeg);
            var bySpeed = (int)MathF.Ceiling(maxSpeed / (minRadius * 0.5f));
            var bySweep = maxTipSpeed > 0f ? (int)MathF.Ceiling((2f * maxTipSpeed + 2f * maxSpeed) / minParryReach) : 0;
            bySpin = Math.Max(bySpin, bySweep);
            var n = Math.Max(Config.MinSubsteps, Math.Max(bySpin, bySpeed));
            n = Math.Max(n, ProjectileSubsteps(minParryReach));
            return Math.Min(n, Config.MaxSubsteps);
        }

        void Integrate(float dt)
        {
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                b.Vel.Y -= Config.Gravity * dt;
                b.Pos += b.Vel * dt;
                if (b.Weapon.HasBlade)
                {
                    var a = b.WeaponAngleDeg + b.SpinDir * b.Weapon.SpinDegPerTick * dt;
                    a %= 360f;
                    if (a < 0f) a += 360f;
                    b.WeaponAngleDeg = a;
                }
            }
        }
    }
}
