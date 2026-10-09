namespace BallBattle.Sim
{
    /// <summary>Numbers for the arena variants (ArenaRegistry). Arena heights stay &lt;= 230 so the HUD fits.</summary>
    public static class ArenaTuning
    {
        public const float PillarRadius = 14f;

        public const float BumperRadius = 9f;
        public const float BumperOffset = 95f;
        public const float BumperBoost = 0.15f;

        public const float SpikeWallDamage = 2f;

        public const float LowGravity = 0.02f;

        public const float TightSize = 170f;
        public const float TightMinSize = 90f;

        public const float WideWidth = 260f;
        public const float WideHeight = 230f;
    }
}