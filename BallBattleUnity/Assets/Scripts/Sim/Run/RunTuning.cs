namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Run economy and difficulty numbers. Index [stage] where stage 0/1/2 = fights 1-3 / 4-6 / 7-8.
    /// Balance values are static fields (not const) so sweeps can try alternatives; gameplay never writes them.
    /// </summary>
    public static class RunTuning
    {
        public const int Fights = 8;
        /// <summary>0-based fight indices of the giant bosses (fights 3, 6, 8).</summary>
        public static readonly int[] BossFights = { 2, 5, 7 };
        public const int Lives = 3;

        public const int StartCoins = 2;
        public const int WinCoins = 3;
        public const int LossCoins = 1;
        public const int RerollCost = 1;
        public const int ExtraPickCost = 3;
        /// <summary>Free pick + at most one bought pick per fight.</summary>
        public const int MaxPicksPerFight = 2;
        public const int OfferSize = 3;
        public const int StartWeaponChoices = 3;

        public const int MaxTraits = 3;
        public const int MaxTraitLevel = 2;

        public static float WinHealPct = 0.3f;
        public static float HealCardPct = 0.5f;
        public static float MaxHpCardBonus = 15f;
        public static float DamageCardPct = 0.15f;
        public static float SpeedCardPct = 0.1f;

        public const float PlayerStartHp = 100f;

        // Enemies. First pass: see plans/261009-1035-.../reports/phase-02-vertical-slice-playtest-report.md for bot win rates.
        public static readonly int[] EnemyMinTraits = { 0, 0, 1 };
        public static readonly int[] EnemyMaxTraits = { 0, 1, 2 };
        public static readonly float[] EnemyHpPct = { 0f, 0f, 0.1f };
        public static readonly float[] EnemyLevel2Chance = { 0f, 0.25f, 0.5f };
        public static readonly float[] BossHp = { 160f, 220f, 300f };
        public static readonly int[] BossTraits = { 0, 1, 2 };
        public static float BossRadius = 24f;

        public static int StageOf(int fightIndex) => fightIndex < 3 ? 0 : (fightIndex < 6 ? 1 : 2);

        public static bool IsBoss(int fightIndex)
        {
            foreach (var b in BossFights)
                if (b == fightIndex) return true;
            return false;
        }

        /// <summary>Well-mixed seed from a run seed and two small integers (splitmix-style), never 0.</summary>
        public static uint Derive(uint seed, int a, int b)
        {
            unchecked
            {
                var x = seed * 0x9E3779B1u + (uint)a * 0x85EBCA77u + (uint)b * 0xC2B2AE3Du + 0x27D4EB2Fu;
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
