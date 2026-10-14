namespace BallBattle.Sim
{
    /// <summary>Numbers for the arena variants (ArenaRegistry). Arena heights stay &lt;= 230 so the HUD fits.</summary>
    public static class ArenaTuning
    {
        public static readonly Fix PillarRadius = 14;

        public static readonly Fix BumperRadius = 9;
        public static readonly Fix BumperOffset = 95;
        /// <summary>0.15 (nearest Fix).</summary>
        public static readonly Fix BumperBoost = Fix.Ratio(15, 100);

        /// <summary>Spike-walls arena: damage of one bounce inside a spike zone. At most one per ball per tick.</summary>
        public const int SpikeWallDamage = 3;

        /// <summary>Corners whose two walls carry a spike zone. TL + BR now; SpikeCorner.All spikes all four corners.</summary>
        public const SpikeCorner SpikeCorners = SpikeCorner.TopLeft | SpikeCorner.BottomRight;

        /// <summary>A spike zone covers SpikeZoneNum/SpikeZoneDen of the CURRENT wall length from its corner (follows the shrink).</summary>
        public const int SpikeZoneNum = 1;
        public const int SpikeZoneDen = 5;

        /// <summary>0.02 px/tick² (nearest Fix).</summary>
        public static readonly Fix LowGravity = Fix.Ratio(2, 100);

        public static readonly Fix TightSize = 170;
        public static readonly Fix TightMinSize = 90;

        public static readonly Fix WideWidth = 260;
        public static readonly Fix WideHeight = 230;
    }
}