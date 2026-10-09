namespace BallBattle.Sim.Weapons
{
    /// <summary>Target picking for ranged weapons. Index loop on purpose: no enumerator allocation in the tick.</summary>
    static class WeaponAim
    {
        /// <summary>Closest living ball other than <paramref name="self"/>, or null. Ties go to the lower index.</summary>
        public static BallState NearestFoe(MatchSim sim, BallState self)
        {
            BallState best = null;
            var bestDistSq = float.MaxValue;
            for (var i = 0; i < sim.Balls.Count; i++)
            {
                var other = sim.Balls[i];
                if (other == self || !other.Alive || other.Hp <= 0f) continue;
                var d = (other.Pos - self.Pos).LengthSq;
                if (d < bestDistSq) { bestDistSq = d; best = other; }
            }
            return best;
        }
    }
}