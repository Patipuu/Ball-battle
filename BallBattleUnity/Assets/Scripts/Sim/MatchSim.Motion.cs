namespace BallBattle.Sim
{
    /// <summary>Substep budget and ball integration.</summary>
    public sealed partial class MatchSim
    {
        /// <summary>π/180, nearest Fix (1144 raw); only used to size the substep budget.</summary>
        static readonly Fix DegToRad = Fix.Ratio(355, 113 * 180);

        /// <summary>
        /// Enough substeps that per substep: no blade turns more than MaxSpinPerSubstepDeg, no ball moves
        /// more than half its radius, two blade tips cannot close faster than the parry reach (otherwise
        /// fast/long blades could pass through each other without a parry), and projectiles alive at the
        /// start of the tick move less than their radius plus the thinnest blade/obstacle half-width.
        /// Capped at MaxSubsteps. Each count is the integer ceiling of the ratio.
        /// </summary>
        int SubstepCount()
        {
            var maxSpin = Fix.Zero;
            var maxSpeed = Fix.Zero;
            var maxTipSpeed = Fix.Zero;
            var minParryReach = Fix.MaxValue;
            var minRadius = Fix.MaxValue;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var speed = b.Vel.Length;
                if (speed > maxSpeed) maxSpeed = speed;
                if (b.Radius < minRadius) minRadius = b.Radius;
                var w = b.Weapon;
                if (!w.HasBlade) continue;
                var spin = b.SpinNow;
                if (spin > maxSpin) maxSpin = spin;
                var tip = spin * DegToRad * (w.BladeInner + b.BladeShift + w.BladeLength);
                if (tip > maxTipSpeed) maxTipSpeed = tip;
                if (w.BladeThickness < minParryReach) minParryReach = w.BladeThickness;
            }

            var bySpin = FixMath.CeilDiv(maxSpin, Config.MaxSpinPerSubstepDeg);
            var bySpeed = FixMath.CeilDiv(maxSpeed, minRadius / 2);
            var bySweep = maxTipSpeed > Fix.Zero ? FixMath.CeilDiv(maxTipSpeed * 2 + maxSpeed * 2, minParryReach) : 0;
            if (bySweep > bySpin) bySpin = bySweep;
            var n = Config.MinSubsteps;
            if (bySpin > n) n = bySpin;
            if (bySpeed > n) n = bySpeed;
            var byShots = ProjectileSubsteps(minParryReach);
            if (byShots > n) n = byShots;
            return n < Config.MaxSubsteps ? n : Config.MaxSubsteps;
        }

        /// <summary>One substep; cumulative quotient differences preserve the configured per-tick rates.</summary>
        void Integrate(int substeps, int index)
        {
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                b.Vel.Y -= FixMath.SubstepPart(Config.Gravity, index, substeps);
                b.Pos += FixMath.SubstepPart(b.Vel, index, substeps);
                if (b.Weapon.HasBlade)
                    b.WeaponAngleDeg = FixMath.Wrap(b.WeaponAngleDeg + FixMath.SubstepPart(b.SpinNow * b.SpinDir, index, substeps), 360);
            }
        }
    }
}
