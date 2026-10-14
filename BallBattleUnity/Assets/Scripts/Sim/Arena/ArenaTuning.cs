namespace BallBattle.Sim
{
    /// <summary>Numbers for the arena variants (ArenaRegistry). Arena heights stay &lt;= 230 so the HUD fits.</summary>
    public static class ArenaTuning
    {
        public static readonly Fix PillarRadius = 14;

        public static readonly Fix BumperRadius = Fix.Ratio(27, 4);   // BB-6c: 9 -> 6.75
        public static readonly Fix BumperOffset = 95;
        /// <summary>0.15 (nearest Fix).</summary>
        public static readonly Fix BumperBoost = Fix.Ratio(15, 100);

        /// <summary>Spike-walls arena: damage of one bounce inside a spike zone. BB-6c: 3 -> 2.25.</summary>
        public static readonly Fix SpikeWallDamage = Fix.Ratio(9, 4);

        /// <summary>
        /// After spike damage a ball takes no more spike damage for this many ACTIVE ticks (hit-stop ticks do not count), per ball
        /// (BB-6c). A corner contact (two spiked faces on consecutive ticks) therefore costs SpikeWallDamage once, not twice. Only spike damage
        /// is blocked: the bounce itself, obstacles and weapon damage are unaffected.
        /// </summary>
        public const int SpikeImmunityTicks = 10;

        /// <summary>Corners whose two walls carry a spike zone. TL + BR now; SpikeCorner.All spikes all four corners.</summary>
        public const SpikeCorner SpikeCorners = SpikeCorner.TopLeft | SpikeCorner.BottomRight;

        /// <summary>A spike zone covers SpikeZoneNum/SpikeZoneDen of the CURRENT wall length from its corner (follows the shrink).</summary>
        public const int SpikeZoneNum = 3;    // BB-6c: 1/5 -> 3/20 of the wall
        public const int SpikeZoneDen = 20;

        /// <summary>0.02 px/tick² (nearest Fix).</summary>
        public static readonly Fix LowGravity = Fix.Ratio(2, 100);

        public static readonly Fix TightSize = 170;
        public static readonly Fix TightMinSize = 90;

        public static readonly Fix WideWidth = 260;
        public static readonly Fix WideHeight = 230;
    }
}
