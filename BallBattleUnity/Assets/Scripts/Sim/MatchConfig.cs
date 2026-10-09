namespace BallBattle.Sim
{
    /// <summary>
    /// All tunable match constants. Units: native pixels, ticks (60 per second).
    /// Mutable on purpose so tests can build edge-case matches; gameplay uses the defaults.
    /// </summary>
    public sealed class MatchConfig
    {
        public const int TicksPerSecond = 60;

        // Arena: square in the middle of the 270x480 portrait screen, HUD above and below.
        // Sizes chosen by pacing sweep (plans/.../reports/phase-02-pacing-sweep.md): median ~55 s with a +1/hit blade.
        public float ArenaWidth = 230f;
        public float ArenaHeight = 230f;
        public float MinArenaWidth = 110f;
        public float MinArenaHeight = 110f;
        public int ShrinkStartTick = 90 * TicksPerSecond;
        public int ShrinkDurationTicks = 30 * TicksPerSecond;
        /// <summary>Safety cap: match ends here and higher HP% wins (equal → draw).</summary>
        public int CapTicks = 180 * TicksPerSecond;

        /// <summary>Obstacles and wall hazards. Empty = the classic Step 1 arena.</summary>
        public ArenaLayout Layout = ArenaLayout.Empty;

        // Balls.
        public float BallRadius = 16f;
        public float StartHp = 100f;
        public float Gravity = 0.05f;
        public float MaxSpeed = 10f;
        public float StartSpeedMin = 5f;
        public float StartSpeedMax = 7f;
        /// <summary>Floor bounce never launches slower than this, so a ball never settles on the floor.</summary>
        public float MinFloorBounce = 5f;
        /// <summary>Horizontal speed floor, so two balls never bounce vertically forever without meeting.</summary>
        public float MinHorizontalSpeed = 1.5f;

        // Combat.
        public int HitCooldownTicks = 15;
        public int HitHitstopTicks = 3;
        public int ParryHitstopTicks = 6;
        public int ParryCooldownTicks = 10;
        public float HitKnockback = 1.5f;
        public float ParryPush = 2f;
        /// <summary>Extra reach for body-attack contact (ball-ball resolution leaves them exactly touching).</summary>
        public float BodyContactSlop = 0.5f;
        /// <summary>Push on a ball hit by a projectile (fraction of HitKnockback).</summary>
        public float ProjectileKnockbackScale = 0.5f;

        // Integration.
        public int MinSubsteps = 2;
        public int MaxSubsteps = 32;
        public float MaxSpinPerSubstepDeg = 4f;

        /// <summary>Play area at a given active tick: full size, then linear shrink to the minimum, then fixed.</summary>
        public ArenaRect ArenaAt(int activeTick)
        {
            float t;
            if (activeTick <= ShrinkStartTick) t = 0f;
            else if (ShrinkDurationTicks <= 0 || activeTick >= ShrinkStartTick + ShrinkDurationTicks) t = 1f;
            else t = (activeTick - ShrinkStartTick) / (float)ShrinkDurationTicks;

            var w = ArenaWidth + (MinArenaWidth - ArenaWidth) * t;
            var h = ArenaHeight + (MinArenaHeight - ArenaHeight) * t;
            return ArenaRect.Centered(w, h);
        }
    }
}
