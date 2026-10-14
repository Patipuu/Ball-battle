namespace BallBattle.Sim
{
    /// <summary>Axis-aligned play area, centered on the origin, y up. Units = native pixels (Q16.16).</summary>
    public struct ArenaRect
    {
        public Fix Left;
        public Fix Right;
        public Fix Bottom;
        public Fix Top;

        public Fix Width => Right - Left;
        public Fix Height => Top - Bottom;

        /// <summary>Half sizes are width/2 and height/2 rounded toward zero (exact for the integer sizes the game uses).</summary>
        public static ArenaRect Centered(Fix width, Fix height)
        {
            var hw = width / 2;
            var hh = height / 2;
            return new ArenaRect { Left = -hw, Right = hw, Bottom = -hh, Top = hh };
        }

        public override string ToString() => $"[{FixMath.Format(Left, 2)},{FixMath.Format(Right, 2)}]x[{FixMath.Format(Bottom, 2)},{FixMath.Format(Top, 2)}]";
    }
}
