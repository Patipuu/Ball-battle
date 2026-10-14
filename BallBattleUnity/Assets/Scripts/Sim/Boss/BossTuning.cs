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
        public static Fix ShellKingHp = 150;   // BB-04 it1: 160 -> 150 (-6.25%)
        public static int ShellKingTraits = 0;
        /// <summary>ActiveTick of the first warning.</summary>
        public static int ShellFirstWarnTick = 60;
        public static int ShellWarnTicks = 30;
        public static int ShellOnTicks = 180;
        public static int ShellOffTicks = 150;
        /// <summary>Arc of the boss's edge covered by the shell, centred on the direction it faces.</summary>
        public static int ShellArcDeg = 120;

        // ---- MAGNET (fight 4): pulls the player toward its centre for a moment every cycle ----
        public static Fix MagnetHp = 200;   // BB-04 it2: 160 -> 200 (+25%)
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

        // ---- MIMIC (fight 8): copies the player's weapon and traits at fight start ----
        public static Fix MimicHp = 275;   // BB-04 it2: 220 -> 275 (+25%)

        // ---- WARDEN (fight 8): the walls step in every few seconds ----
        public static Fix WardenHp = 275;   // BB-04 it2: 220 -> 275 (+25%)
        /// <summary>Random traits, as the fight-8 boss had before BB-04.</summary>
        public static int WardenTraits = 2;
        public static int WardenFirstStepTick = 360;   // BB-04 it3: 480 -> 360 (-25%)
        public static int WardenStepEveryTicks = 480;
        public static int WardenWarnTicks = 60;
        /// <summary>Each step takes this percentage of the ORIGINAL width and height.</summary>
        public static int WardenStepPct = 12;   // BB-04 it3: 10 -> 12 (+20%)
        /// <summary>The walls stop at this percentage of the original size.</summary>
        public static int WardenMinPct = 45;   // BB-04 it3: 60 -> 45 (-25%)

        /// <summary>Number of steps the Warden can take ((100 − MinPct) / StepPct: 4 with the spec's 10%/60%, also 4 with 12%/45%).</summary>
        public static int WardenMaxSteps => WardenStepPct > 0 ? (100 - WardenMinPct) / WardenStepPct : 0;
    }
}
