namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Run economy and difficulty numbers. Index [stage] where stage 0/1/2 = fights 1-3 / 4-6 / 7-8.
    /// Balance values are static fields (not const) so sweeps can try alternatives; gameplay never writes them.
    /// </summary>
    public static class RunTuning
    {
        public const int Fights = 8;
        /// <summary>0-based fight indices of the boss slots (fights 4 and 8). BB-04: slot 0 = SHELL KING or MAGNET, slot 1 = MIMIC or WARDEN (Boss.BossIds.Slots).</summary>
        public static readonly int[] BossFights = { 3, 7 };
        public const int Retries = 1;
        /// <summary>0.6 (nearest Fix).</summary>
        public static Fix RetryHealPct = Fix.Ratio(3, 5);

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

        public static Fix WinHealPct = Fix.Ratio(7, 20);
        public static Fix HealCardPct = Fix.Ratio(1, 2);
        public static Fix MaxHpCardBonus = 15;
        public static Fix DamageCardPct = Fix.Ratio(3, 20);
        public static Fix SpeedCardPct = Fix.Ratio(1, 10);

        public static readonly Fix PlayerStartHp = 100;

        // Enemies. First pass: see plans/261009-1035-.../reports/phase-02-vertical-slice-playtest-report.md for bot win rates.
        public static readonly int[] EnemyMinTraits = { 0, 0, 1 };
        public static readonly int[] EnemyMaxTraits = { 0, 1, 2 };
        public static readonly Fix[] EnemyHpPct = { 0, 0, Fix.Ratio(1, 10) };
        public static readonly Fix[] EnemyLevel2Chance = { 0, Fix.Ratio(1, 4), Fix.Ratio(1, 2) };
        /// <summary>Pre-BB-04 boss HP / traits per stage. Legacy: EnemyGenerator now uses Boss.BossTuning per boss; kept for old sweeps and tests.</summary>
        public static readonly Fix[] BossHp = { 160, 220, 300 };
        public static readonly int[] BossTraits = { 0, 1, 2 };
        public static Fix BossRadius = 24;

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
