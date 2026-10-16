namespace BallBattle.Sim
{
    /// <summary>BB-14: small hooks the tribute balls (chorus, serpent) need from the match.</summary>
    public sealed partial class MatchSim
    {
        internal void EmitTribute(SimEventType type, BallState ball, int b, Fix value, FixVec2 point, byte variant = 0) =>
            Emit(type, ball.Index, b, value, point, variant);

        internal void EmitStat(BallState ball) => Emit(SimEventType.StatChanged, ball.Index, -1, Fix.Zero, ball.Pos);

        /// <summary>A seeded value in [min, max) from the match random (prey positions).</summary>
        internal Fix RandomFix(Fix min, Fix max) => rng.RangeFixOrMin(min, max);

        /// <summary>The nearest other living ball, or null.</summary>
        internal BallState NearestEnemy(BallState self)
        {
            BallState best = null;
            long bestD = long.MaxValue;
            foreach (var b in balls)
            {
                if (b == self || !Active(b)) continue;
                var d = FixVec2.Dot(b.Pos - self.Pos, b.Pos - self.Pos);
                if (d < bestD) { bestD = d; best = b; }
            }
            return best;
        }

        static bool HasTribute(System.Collections.Generic.IReadOnlyList<BallLoadout> loadouts)
        {
            foreach (var l in loadouts)
                if (l.Weapon is Weapons.ChorusRule || l.Weapon is Weapons.SerpentRule) return true;
            return false;
        }
    }
}
