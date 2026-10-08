namespace BallBattle.Sim
{
    /// <summary>Axis-aligned play area, centered on the origin, y up. Units = native pixels.</summary>
    public struct ArenaRect
    {
        public float Left;
        public float Right;
        public float Bottom;
        public float Top;

        public float Width => Right - Left;
        public float Height => Top - Bottom;

        public static ArenaRect Centered(float width, float height)
        {
            return new ArenaRect
            {
                Left = -width * 0.5f,
                Right = width * 0.5f,
                Bottom = -height * 0.5f,
                Top = height * 0.5f
            };
        }

        public override string ToString() => $"[{Left:0.##},{Right:0.##}]x[{Bottom:0.##},{Top:0.##}]";
    }
}
