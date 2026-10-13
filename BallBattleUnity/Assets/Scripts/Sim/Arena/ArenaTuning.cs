namespace BallBattle.Sim
{
    /// <summary>Numbers for the arena variants (ArenaRegistry). Arena heights stay &lt;= 230 so the HUD fits.</summary>
    public static class ArenaTuning
    {
        public const float PillarRadius = 14f;

        public const float BumperRadius = 9f;
        public const float BumperOffset = 95f;
        public const float BumperBoost = 0.15f;

        /// <summary>Spike-walls arena: damage of one bounce inside a spike zone. At most one per ball per tick.</summary>
        public const int SpikeWallDamage = 3;

        /// <summary>Corners whose two walls carry a spike zone. TL + BR now; SpikeCorner.All spikes all four corners.</summary>
        public const SpikeCorner SpikeCorners = SpikeCorner.TopLeft | SpikeCorner.BottomRight;

        /// <summary>A spike zone covers SpikeZoneNum/SpikeZoneDen of the CURRENT wall length from its corner (follows the shrink).</summary>
        public const int SpikeZoneNum = 1;
        public const int SpikeZoneDen = 5;

        public const float LowGravity = 0.02f;

        public const float TightSize = 170f;
        public const float TightMinSize = 90f;

        public const float WideWidth = 260f;
        public const float WideHeight = 230f;
    }
}