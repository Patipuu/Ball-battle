namespace BallBattle.Sim
{
    public enum SimEventType : byte
    {
        /// <summary>A=attacker, B=target, Value=damage, Point=contact.</summary>
        Hit,
        /// <summary>A,B = the two balls, Point=blade contact.</summary>
        Parry,
        /// <summary>A=ball, Point=contact.</summary>
        WallBounce,
        /// <summary>A,B = balls, Value=closing speed.</summary>
        BallBounce,
        /// <summary>A=ball that died, B=killer (-1 if none).</summary>
        Death,
        /// <summary>A=ball whose weapon stats changed.</summary>
        StatChanged,
        /// <summary>A=winner index (-1 on draw).</summary>
        MatchEnd
    }

    /// <summary>One thing that happened during a tick. View/audio react to these; they never read Sim internals for effects.</summary>
    public struct SimEvent
    {
        public SimEventType Type;
        public int Tick;
        public int A;
        public int B;
        public float Value;
        public Vec2 Point;
    }

    public enum MatchOutcome : byte
    {
        Ongoing,
        Win,
        Draw
    }

    public enum MatchEndReason : byte
    {
        None,
        Knockout,
        TimeCap
    }
}
