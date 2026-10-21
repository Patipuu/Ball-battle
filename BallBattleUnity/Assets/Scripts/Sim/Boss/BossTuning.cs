namespace BallBattle.Sim.Boss
{
    /// <summary>
    /// BB-04 boss numbers. Units: ticks are ACTIVE ticks (hit-stop excluded, 60 per second), HP in points, angles in degrees.
    /// Static fields (not const) so balance sweeps can try alternatives; gameplay never writes them. Every value that a
    /// boss rule reads is mixed into that rule's hash, so a sweep can never collide with a pinned hash. None of these is
    /// read by a Versus match (no boss rule = no effect and no hash change).
    /// </summary>
    public static class BossTuning
    {
        // ---- SHELL KING (fight 4): a 120-degree shell that blocks weapon and projectile hits on one side ----
        public static Fix ShellKingHp = 156;   // BB-04 it1: 160 -> 150 (-6.25%); BB-BAL1: 150 -> 156 (+4%)
        public static int ShellKingTraits = 0;
        /// <summary>ActiveTick of the first warning.</summary>
        public static int ShellFirstWarnTick = 60;
        public static int ShellWarnTicks = 30;
        public static int ShellOnTicks = 180;
        public static int ShellOffTicks = 150;
        /// <summary>Arc of the boss's edge covered by the shell, centred on the direction it faces.</summary>
        public static int ShellArcDeg = 120;

        // ---- MAGNET (fight 4): pulls the player toward its centre for a moment every cycle ----
        public static Fix MagnetHp = 225;   // BB-04: 200; BB-6e: 200 -> 225 (+12.5%)
        public static int MagnetTraits = 0;
        public static int MagnetFirstWarnTick = 120;
        public static int MagnetWarnTicks = 60;
        public static int MagnetPullTicks = 45;   // BB-04 it3: 60 -> 45 (-25%)
        /// <summary>Warning + pull + rest.</summary>
        public static int MagnetCycleTicks = 300;
        /// <summary>Pull per active tick as a fraction of MatchConfig.MaxSpeed (spec start 1/40).</summary>
        public static Fix MagnetPullPerTick = Fix.Ratio(1, 50);   // BB-04 it3: 1/40 -> 1/50 (-20%)
        /// <summary>Pull multiplier on a ball with the Heavy trait (any level): 1/2.</summary>
        public static Fix MagnetHeavyScale = Fix.Ratio(1, 2);

        // ---- MIMIC (fight 8): borrows the player's weapon, then adapts ----
        public static Fix MimicHp = 255;
        public static int MimicEchoFirstTick = 180, MimicEchoCycleTicks = 300, MimicEchoWarnTicks = 45;
        public static Fix MimicEchoRadius = 38, MimicEchoDamage = 16;

        // ---- WARDEN (fight 8): the walls step in every few seconds ----
        public static Fix WardenHp = 290;
        public static int WardenCrossFirstTick = 210, WardenCrossCycleTicks = 360, WardenCrossWarnTicks = 45;
        public static Fix WardenCrossHalfWidth = 16, WardenCrossDamage = 12;
        /// <summary>Random traits, as the fight-8 boss had before BB-04.</summary>
        public static int WardenTraits = 2;
        public static int WardenFirstStepTick = 450;   // BB-6e: 360 -> 450 (+25% vs BB-04 it3)
        public static int WardenStepEveryTicks = 480;
        public static int WardenWarnTicks = 60;
        /// <summary>Each step takes this percentage of the ORIGINAL width and height.</summary>
        public static int WardenStepPct = 9;   // BB-6e: 12 -> 9 (-25% vs BB-04 it3)
        /// <summary>The walls stop at this percentage of the original size.</summary>
        public static int WardenMinPct = 56;   // BB-6e: 45 -> 56 (+24.4% vs BB-04 it3)

        /// <summary>BB-6f: newly moved sides/ceiling spike the player during this many ActiveTicks after a WARDEN step (60-240).</summary>
        public static int SpikeWindowTicks = 144;   // BB-6f it2: 120 -> 144, WARDEN retry 74.2% -> 69.9%

        /// <summary>Damage per contact during a WARDEN spike window (player only, never the floor).</summary>
        public static Fix WardenSpikeDamage = 3;   // BB-6e it1: 2 (range 2-3); BB-BAL1: 2 -> 3

        // All non-Mimic bosses use one restrained touch hit plus their authored rule; none rolls a player weapon or trait.
        public static Fix ContactDamage(string id)
        {
            switch (id)
            {
                case BossIds.ShellKing: return 1;
                case BossIds.Magnet: return 1;
                case BossIds.Hunter: return Fix.Ratio(5, 2);
                case BossIds.Bell: return Fix.Ratio(3, 2);
                case BossIds.Warden: return 5;
                case BossIds.Ember: return Fix.Ratio(9, 2);
                case BossIds.Prism: return 4;
                default: return 3;
            }
        }
        public static Fix HunterHp = 180;
        public static Fix BellHp = 190;
        public static Fix EmberHp = 255;
        public static Fix PrismHp = 245;

        /// <summary>Number of steps the Warden can take ((100 − MinPct) / StepPct: 4 with the spec's 10%/60%, also 4 with 12%/45%).</summary>
        public static int WardenMaxSteps => WardenStepPct > 0 ? (100 - WardenMinPct) / WardenStepPct : 0;
    }
}
