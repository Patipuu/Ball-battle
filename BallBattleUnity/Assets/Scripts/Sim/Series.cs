namespace BallBattle.Sim
{
    /// <summary>
    /// Best-of-3 series rules: first to 2 round wins. A drawn round does not count and is replayed with a new seed.
    /// Every round seed is derived from the match seed and the round index, so replaying a match seed replays
    /// the whole series exactly (same rounds, same results) on the same build.
    /// </summary>
    public sealed class Series
    {
        public const int WinsNeeded = 2;

        public readonly string WeaponA;
        public readonly string WeaponB;
        public readonly uint MatchSeed;

        readonly int[] wins = new int[2];

        /// <summary>Rounds played so far, including drawn ones (index of the next round's seed).</summary>
        public int RoundsPlayed { get; private set; }
        public int Draws { get; private set; }
        public int WinsA => wins[0];
        public int WinsB => wins[1];
        public bool IsOver => wins[0] >= WinsNeeded || wins[1] >= WinsNeeded;
        /// <summary>0 or 1 once over, else -1.</summary>
        public int Winner => wins[0] >= WinsNeeded ? 0 : (wins[1] >= WinsNeeded ? 1 : -1);
        /// <summary>1-based number shown as "ROUND n": decided rounds + 1 (a replayed draw keeps its number).</summary>
        public int DisplayRound => wins[0] + wins[1] + 1;
        public uint CurrentRoundSeed => SeedFor(MatchSeed, RoundsPlayed);

        public Series(string weaponA, string weaponB, uint matchSeed)
        {
            WeaponA = weaponA;
            WeaponB = weaponB;
            MatchSeed = matchSeed;
        }

        /// <summary>Record the result of the current round: winner ball index (0 = A, 1 = B) or -1 for a draw.</summary>
        public void ReportRound(int winner)
        {
            if (IsOver) throw new System.InvalidOperationException("Series is already over");
            RoundsPlayed++;
            if (winner == 0 || winner == 1) wins[winner]++;
            else Draws++;
        }

        /// <summary>Well-mixed per-round seed (splitmix-style finalizer), never 0.</summary>
        public static uint SeedFor(uint matchSeed, int roundIndex)
        {
            unchecked
            {
                var x = matchSeed * 0x9E3779B1u + (uint)roundIndex * 0x85EBCA77u + 0x27D4EB2Fu;
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x == 0 ? 1u : x;
            }
        }
    }
}
