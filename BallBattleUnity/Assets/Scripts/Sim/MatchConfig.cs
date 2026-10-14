namespace BallBattle.Sim
{
    /// <summary>
    /// All tunable match constants. Units: native pixels, ticks (60 per second); numbers are Q16.16 (exact unless listed
    /// in docs/claude-runs/6a6bc-fix.md as nearest-Fix).
    /// Mutable on purpose so tests can build edge-case matches; gameplay uses the defaults.
    /// </summary>
    public sealed class MatchConfig
    {
        public const int TicksPerSecond = 60;

        // Arena: square in the middle of the 270x480 portrait screen, HUD above and below.
        // Sizes chosen by pacing sweep (plans/.../reports/phase-02-pacing-sweep.md): median ~55 s with a +1/hit blade.
        public Fix ArenaWidth = 230;
        public Fix ArenaHeight = 230;
        public Fix MinArenaWidth = 110;
        public Fix MinArenaHeight = 110;
        public int ShrinkStartTick = 90 * TicksPerSecond;
        public int ShrinkDurationTicks = 30 * TicksPerSecond;
        /// <summary>Safety cap: match ends here and higher HP% wins (equal → draw).</summary>
        public int CapTicks = 180 * TicksPerSecond;

        /// <summary>Obstacles and wall hazards. Empty = the classic Step 1 arena.</summary>
        public ArenaLayout Layout = ArenaLayout.Empty;

        // Balls.
        public Fix BallRadius = 16;
        public Fix StartHp = 100;
        /// <summary>0.05 px/tick² (nearest Fix).</summary>
        public Fix Gravity = Fix.Ratio(1, 20);
        public Fix MaxSpeed = 10;
        public Fix StartSpeedMin = 5;
        public Fix StartSpeedMax = 7;
        /// <summary>Floor bounce never launches slower than this, so a ball never settles on the floor.</summary>
        public Fix MinFloorBounce = 5;
        /// <summary>Horizontal speed floor, so two balls never bounce vertically forever without meeting.</summary>
        public Fix MinHorizontalSpeed = Fix.Ratio(3, 2);

        // Combat.
        public int HitCooldownTicks = 15;
        public int HitHitstopTicks = 3;
        public int ParryHitstopTicks = 6;
        public int ParryCooldownTicks = 10;
        public Fix HitKnockback = Fix.Ratio(3, 2);
        public Fix ParryPush = 2;
        /// <summary>Extra reach for body-attack contact (ball-ball resolution leaves them exactly touching).</summary>
        public Fix BodyContactSlop = Fix.Ratio(1, 2);
        /// <summary>Push on a ball hit by a projectile (fraction of HitKnockback).</summary>
        public Fix ProjectileKnockbackScale = Fix.Ratio(1, 2);

        // Integration.
        public int MinSubsteps = 2;
        public int MaxSubsteps = 32;
        public Fix MaxSpinPerSubstepDeg = 4;

        /// <summary>
        /// Play area at a given active tick: full size, then linear shrink to the minimum, then fixed. Size during the
        /// shrink = full + (min - full) × elapsed / duration, rounded toward zero (integer ticks, long intermediate).
        /// </summary>
        public ArenaRect ArenaAt(int activeTick)
        {
            if (activeTick <= ShrinkStartTick) return ArenaRect.Centered(ArenaWidth, ArenaHeight);
            if (ShrinkDurationTicks <= 0 || activeTick >= ShrinkStartTick + ShrinkDurationTicks) return ArenaRect.Centered(MinArenaWidth, MinArenaHeight);
            var elapsed = activeTick - ShrinkStartTick;
            var w = ArenaWidth + FixMath.MulDiv(MinArenaWidth - ArenaWidth, elapsed, ShrinkDurationTicks);
            var h = ArenaHeight + FixMath.MulDiv(MinArenaHeight - ArenaHeight, elapsed, ShrinkDurationTicks);
            return ArenaRect.Centered(w, h);
        }
    }
}
