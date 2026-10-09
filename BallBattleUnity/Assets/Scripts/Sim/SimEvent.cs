namespace BallBattle.Sim
{
    public enum SimEventType : byte
    {
        /// <summary>A=attacker, B=target, Value=damage, Point=contact.</summary>
        Hit,
        /// <summary>A,B = the two balls, Point=blade contact.</summary>
        Parry,
        /// <summary>A=ball, Value=spike-wall damage taken (0 if none), Point=contact.</summary>
        WallBounce,
        /// <summary>A,B = balls, Value=closing speed.</summary>
        BallBounce,
        /// <summary>A=ball that died, B=last ball that damaged it (-1 if none). Several deaths in one step come in ball-index order.</summary>
        Death,
        /// <summary>A=ball whose weapon stats changed.</summary>
        StatChanged,
        /// <summary>A=winner index (-1 on draw).</summary>
        MatchEnd,
        /// <summary>A=target, B=source ball (-1 none), Value=dps / charges / heal per second, Variant=(byte)StatusKind.</summary>
        StatusApplied,
        /// <summary>Poison pulse. A=target, B=main source (-1 none), Value=damage, Variant=(byte)StatusKind.Poison.</summary>
        StatusTick,
        /// <summary>A shield charge ate a hit. A=shielded ball, B=attacker, Point=contact.</summary>
        ShieldBlocked,
        /// <summary>A=ball, B=obstacle index, Value=damage dealt to the ball (0 if harmless), Point=contact.</summary>
        ObstacleHit,
        /// <summary>A=ball, Value=HP actually restored (also emitted when a trait prevents death).</summary>
        Heal,
        /// <summary>Non-hit damage from MatchSim.DealDamage (reflect, custom effects). A=source ball (-1 none), B=target, Value=damage, Variant=(byte)DamageKind. Poison pulses use StatusTick; arena hazards ride on ObstacleHit/WallBounce Value.</summary>
        Damage,
        /// <summary>A=owner ball, B=projectile slot, Point=spawn position.</summary>
        ProjectileFired,
        /// <summary>A=owner ball, B=target ball, Value=damage (0 if shield-blocked), Point=contact.</summary>
        ProjectileHit,
        /// <summary>A=deflecting ball (new owner), B=projectile slot, Point=contact.</summary>
        ProjectileDeflected
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
        /// <summary>Sub-type for events that need one (see each SimEventType); 0 otherwise.</summary>
        public byte Variant;
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
