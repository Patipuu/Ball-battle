namespace BallBattle.Sim.Boss
{
    /// <summary>Boss ids (saved in progress data and runs, so never rename one) and their Run slots.</summary>
    public static class BossIds
    {
        public const string ShellKing = "shellking", Magnet = "magnet", Mimic = "mimic", Warden = "warden";
        public const string Hunter = "hunter", Bell = "bell", Ember = "ember_boss", Prism = "prism";

        public static readonly string[] All = { ShellKing, Magnet, Hunter, Bell, Mimic, Warden, Ember, Prism };

        /// <summary>Candidates per boss slot: slot 0 = fight 4, slot 1 = fight 8 (RunTuning.BossFights).</summary>
        public static readonly string[][] Slots = { new[] { ShellKing, Magnet, Hunter, Bell }, new[] { Mimic, Warden, Ember, Prism } };

        public static bool Exists(string id)
        {
            foreach (var b in All)
                if (b == id) return true;
            return false;
        }

        /// <summary>Slot of a boss (0 or 1), -1 for an unknown id.</summary>
        public static int SlotOf(string id)
        {
            for (var s = 0; s < Slots.Length; s++)
                foreach (var b in Slots[s])
                    if (b == id) return s;
            return -1;
        }
    }

    /// <summary>New rule instance per boss ball per match.</summary>
    public static class BossRegistry
    {
        public static BossRule Create(string id)
        {
            switch (id)
            {
                case BossIds.ShellKing: return new ShellKingRule();
                case BossIds.Magnet: return new MagnetRule();
                case BossIds.Mimic: return new MimicRule();
                case BossIds.Warden: return new WardenRule();
                case BossIds.Hunter: return new HunterRule();
                case BossIds.Bell: return new BellRule();
                case BossIds.Ember: return new EmberRule();
                case BossIds.Prism: return new PrismRule();
                default: throw new System.ArgumentException($"Unknown boss '{id}'", nameof(id));
            }
        }
    }

    /// <summary>
    /// SHELL KING: every ShellWarnTicks + ShellOnTicks + ShellOffTicks (360) active ticks from ShellFirstWarnTick (60): a warning,
    /// then the shell is ON and faces the target as it was when it turned on (fixed for that cycle), then OFF. While ON, a Weapon or
    /// Projectile hit whose contact point lies within ShellArcDeg/2 of that direction (seen from the boss centre) deals 0 and does not
    /// count (no weapon growth, no on-hit effects); knockback, hit cooldown and hit-stop still happen. Poison, reflect and hazard damage
    /// never go through BlocksHit, so they ignore the shell.
    /// </summary>
    public sealed class ShellKingRule : BossRule
    {
        public override string Id => BossIds.ShellKing;

        /// <summary>Shell up now (View: draw the arc).</summary>
        public bool ShellOn { get; private set; }
        /// <summary>In the warning before the shell turns on (View: telegraph).</summary>
        public bool Warning { get; private set; }
        /// <summary>Unit vector the shell faces while ON (from the boss centre).</summary>
        public FixVec2 Facing { get; private set; } = FixVec2.Right;

        public static int CycleTicks => BossTuning.ShellWarnTicks + BossTuning.ShellOnTicks + BossTuning.ShellOffTicks;

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - BossTuning.ShellFirstWarnTick;
            if (t < 0) return;
            var p = t % CycleTicks;
            var target = Target;
            var targetIndex = target != null ? target.Index : -1;
            if (p == 0)
            {
                Warning = true;
                Sim.EmitBoss(BossEvent.ShellWarn, Self.Index, targetIndex, Fix.Zero, Self.Pos);
            }
            if (p == BossTuning.ShellWarnTicks)
            {
                Warning = false;
                ShellOn = true;
                if (target != null) Facing = (target.Pos - Self.Pos).NormalizedOr(Facing);
                Sim.EmitBoss(BossEvent.ShellOn, Self.Index, targetIndex, Fix.Zero, Self.Pos + Facing * Self.Radius);
                // The shield's tell also warns of a short frontal slam. Circling behind avoids both effects.
                if (target != null && (target.Pos - Self.Pos).LengthSq <= FixMath.Sq(95) &&
                    FixVec2.DotFix((target.Pos - Self.Pos).NormalizedOr(Facing), Facing) >= Fix.Ratio(1, 2))
                    Sim.DealDamage(Self, target, 20, DamageKind.Ability);
            }
            if (p == BossTuning.ShellWarnTicks + BossTuning.ShellOnTicks)
            {
                ShellOn = false;
                Sim.EmitBoss(BossEvent.ShellOff, Self.Index, targetIndex, Fix.Zero, Self.Pos);
            }
        }

        public override bool BlocksHit(BallState attacker, FixVec2 contact) => ShellOn && attacker != Self && InArc(contact);

        public override Fix ModifyIncomingDamage(Fix damage, DamageKind kind) =>
            ShellOn || Warning ? damage : damage * Fix.Ratio(6, 5);

        public override int HitArcAngleTenths(BallState attacker, FixVec2 contact) =>
            ShellOn && attacker != Self ? ContactAngleTenths(contact) : -1;

        /// <summary>
        /// BB-FIX2: is <paramref name="point"/> within the shell's arc seen from the boss centre? The spec's test: the contact point's
        /// angle to Facing, rounded to 0.1 degree, is at most ShellArcDeg/2 (60.0 blocks, 60.1 does not). One number for the block and the
        /// trace (ContactAngleTenths, carried on the hit event), measured at the moment of the hit.
        /// </summary>
        public bool InArc(FixVec2 point)
        {
            var a = ContactAngleTenths(point);
            return a >= 0 && a <= HalfArcTenths;
        }

        /// <summary>
        /// Unsigned angle between Facing and (<paramref name="point"/> − boss centre) in tenths of a degree (0 = dead centre, 1800 = the
        /// back), or -1 when the point is the centre. The vector is turned into the shell's own frame (along Facing, across it) and
        /// measured with one FixedTrig.Atan2, so it is rounded once, to the nearest 0.1 degree.
        /// </summary>
        public int ContactAngleTenths(FixVec2 point)
        {
            var v = point - Self.Pos;
            if (v.X.Raw == 0 && v.Y.Raw == 0) return -1;
            var along = FixVec2.DotFix(v, Facing);
            var across = v.Y * Facing.X - v.X * Facing.Y;
            var a = FixedTrig.Atan2(across, along);
            return a > 1800 ? 3600 - a : a;
        }

        static int HalfArcTenths => BossTuning.ShellArcDeg * 5;   // tenths of a degree: arc/2 × 10

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, ShellOn);
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, Facing);
            h = SimHash.Mix(h, BossTuning.ShellFirstWarnTick);
            h = SimHash.Mix(h, BossTuning.ShellWarnTicks);
            h = SimHash.Mix(h, BossTuning.ShellOnTicks);
            h = SimHash.Mix(h, BossTuning.ShellOffTicks);
            return SimHash.Mix(h, BossTuning.ShellArcDeg);
        }
    }

    /// <summary>
    /// MAGNET: every MagnetCycleTicks (300) active ticks from MagnetFirstWarnTick (120): a MagnetWarnTicks warning, then for
    /// MagnetPullTicks the other ball(s) gain MagnetPullPerTick × MatchConfig.MaxSpeed of velocity toward the boss centre per active
    /// tick (× MagnetHeavyScale with the Heavy trait), clamped to that ball's normal speed cap, then rest.
    /// </summary>
    public sealed class MagnetRule : BossRule
    {
        public override string Id => BossIds.Magnet;

        public bool Warning { get; private set; }
        public bool Pulling { get; private set; }

        public override void OnActiveTick()
        {
            var t = Sim.ActiveTick - BossTuning.MagnetFirstWarnTick;
            if (t < 0) return;
            var p = t % BossTuning.MagnetCycleTicks;
            var target = Target;
            var targetIndex = target != null ? target.Index : -1;
            if (p == 0)
            {
                Warning = true;
                Sim.EmitBoss(BossEvent.MagnetWarn, Self.Index, targetIndex, Fix.Zero, Self.Pos);
            }
            if (p == BossTuning.MagnetWarnTicks)
            {
                Warning = false;
                Pulling = true;
                Sim.EmitBoss(BossEvent.MagnetPull, Self.Index, targetIndex, Fix.Zero, Self.Pos);
            }
            if (p == BossTuning.MagnetWarnTicks + BossTuning.MagnetPullTicks)
            {
                Pulling = false;
                if (target != null)
                {
                    DamageNear(Self.Pos, 92, 22);
                    target.Vel += (target.Pos - Self.Pos).NormalizedOr(FixVec2.Right) * 5;
                }
                Sim.EmitBoss(BossEvent.MagnetEnd, Self.Index, targetIndex, Fix.Zero, Self.Pos);
            }
            if (!Pulling) return;
            foreach (var b in Sim.Balls)
                if (b != Self && b.Alive) Pull(b);
        }

        /// <summary>Velocity added to <paramref name="b"/> per pull tick (before the speed clamp).</summary>
        public Fix PullFor(BallState b)
        {
            var a = Sim.Config.MaxSpeed * BossTuning.MagnetPullPerTick;
            foreach (var t in b.Traits)
                if (t.Id == Traits.HeavyTrait.TraitId) return a * BossTuning.MagnetHeavyScale;
            return a;
        }

        void Pull(BallState b)
        {
            var dir = (Self.Pos - b.Pos).NormalizedOr(FixVec2.Zero);
            if (dir.X == Fix.Zero && dir.Y == Fix.Zero) return;
            b.Vel += dir * PullFor(b);
            Sim.ClampSpeed(b);
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, Pulling);
            h = SimHash.Mix(h, BossTuning.MagnetFirstWarnTick);
            h = SimHash.Mix(h, BossTuning.MagnetWarnTicks);
            h = SimHash.Mix(h, BossTuning.MagnetPullTicks);
            h = SimHash.Mix(h, BossTuning.MagnetCycleTicks);
            h = SimHash.Mix(h, BossTuning.MagnetPullPerTick);
            return SimHash.Mix(h, BossTuning.MagnetHeavyScale);
        }
    }

    /// <summary>
    /// MIMIC borrows the player's weapon and active skill at fight start, and periodically strikes a marked position.
    /// After 15 seconds it resists the damage type that actually hurt it most. Player traits are not copied.
    /// </summary>
    public sealed class MimicRule : BossRule
    {
        public override string Id => BossIds.Mimic;

        public bool Announced { get; private set; }
        public bool Adapted { get; private set; }
        public DamageKind AdaptedKind { get; private set; }
        public bool EchoWarning { get; private set; }
        public FixVec2 EchoMark { get; private set; }
        readonly Fix[] damageByKind = new Fix[6];

        public override void OnDamageTaken(DamageKind kind, Fix damage)
        {
            if (!Adapted && kind != DamageKind.Hazard && (int)kind < damageByKind.Length)
                damageByKind[(int)kind] += damage;
        }

        public override Fix ModifyIncomingDamage(Fix damage, DamageKind kind) =>
            Adapted && kind == AdaptedKind ? damage * Fix.Ratio(7, 10) : damage;

        public override void OnActiveTick()
        {
            var echoTick = Sim.ActiveTick - BossTuning.MimicEchoFirstTick;
            if (echoTick >= 0)
            {
                var phase = echoTick % BossTuning.MimicEchoCycleTicks;
                if (phase == 0)
                {
                    var target = Target;
                    if (target != null)
                    {
                        EchoMark = target.Pos;
                        EchoWarning = true;
                        Sim.EmitBoss(BossEvent.MimicEchoWarn, Self.Index, target.Index, BossTuning.MimicEchoRadius, EchoMark);
                    }
                }
                if (phase == BossTuning.MimicEchoWarnTicks && EchoWarning)
                {
                    EchoWarning = false;
                    DamageNear(EchoMark, BossTuning.MimicEchoRadius, BossTuning.MimicEchoDamage);
                    Sim.EmitBoss(BossEvent.MimicEchoStrike, Self.Index, Target?.Index ?? -1, BossTuning.MimicEchoRadius, EchoMark);
                }
            }
            if (Sim.ActiveTick >= 360 && Sim.ActiveTick % 360 == 0) Sim.Tap(Self.Index);
            if (!Announced)
            {
                Announced = true;
                var target = Target;
                Sim.EmitBoss(BossEvent.MimicCopied, Self.Index, target != null ? target.Index : -1, Fix.Zero, Self.Pos);
            }
            if (!Adapted && Sim.ActiveTick >= 900)
            {
                var best = Fix.Zero;
                for (var i = 0; i < damageByKind.Length; i++)
                    if (damageByKind[i] > best) { best = damageByKind[i]; AdaptedKind = (DamageKind)i; }
                Adapted = true;
                Sim.EmitBoss(BossEvent.MimicAdapted, Self.Index, Target?.Index ?? -1, best, Self.Pos);
            }
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Announced);
            h = SimHash.Mix(h, Adapted);
            h = SimHash.Mix(h, (int)AdaptedKind);
            h = SimHash.Mix(h, EchoWarning);
            h = SimHash.Mix(h, EchoMark);
            h = SimHash.Mix(h, BossTuning.MimicEchoFirstTick);
            h = SimHash.Mix(h, BossTuning.MimicEchoCycleTicks);
            h = SimHash.Mix(h, BossTuning.MimicEchoWarnTicks);
            h = SimHash.Mix(h, BossTuning.MimicEchoRadius);
            h = SimHash.Mix(h, BossTuning.MimicEchoDamage);
            foreach (var amount in damageByKind) h = SimHash.Mix(h, amount);
            return h;
        }
    }

    /// <summary>
    /// WARDEN: at WardenFirstStepTick and every WardenStepEveryTicks after, the walls step in by WardenStepPct of the
    /// ORIGINAL width and height, down to WardenMinPct, each step announced WardenWarnTicks before. The arena is the smaller
    /// of this and the normal shrink per axis. On a step MatchSim pushes balls caught outside back in (no damage, no bounce),
    /// separates them with the normal ball-ball resolution and disables obstacles whose centre is now outside.
    /// BB-6f: only sides/ceiling actually moved by a WARDEN step spike the player, for SpikeWindowTicks ActiveTicks.
    /// Normal shrink movement never opens a window. Spikes follow the active smaller bound while that window lasts.
    /// </summary>
    public sealed class WardenRule : BossRule
    {
        public override string Id => BossIds.Warden;

        public int Steps { get; private set; }
        public bool Warning { get; private set; }
        public bool CrossWarning { get; private set; }
        public FixVec2 CrossMark { get; private set; }

        byte movedWalls;
        int spikeUntilActiveTick;

        /// <summary>ActiveTick of step number <paramref name="step"/> (1-based).</summary>
        public static int StepTick(int step) => BossTuning.WardenFirstStepTick + (step - 1) * BossTuning.WardenStepEveryTicks;

        /// <summary>Warden size after <paramref name="steps"/> steps: original × (100 − steps × StepPct) / 100, rounded toward zero.</summary>
        public static Fix SizeAfter(Fix original, int steps) => FixMath.MulDiv(original, 100 - steps * BossTuning.WardenStepPct, 100);

        public override void OnActiveTick()
        {
            var crossTick = Sim.ActiveTick - BossTuning.WardenCrossFirstTick;
            if (crossTick >= 0)
            {
                var phase = crossTick % BossTuning.WardenCrossCycleTicks;
                if (phase == 0)
                {
                    var target = Target;
                    if (target != null)
                    {
                        CrossMark = target.Pos;
                        CrossWarning = true;
                        Sim.EmitBoss(BossEvent.WardenCrossWarn, Self.Index, target.Index, BossTuning.WardenCrossHalfWidth, CrossMark);
                    }
                }
                if (phase == BossTuning.WardenCrossWarnTicks && CrossWarning)
                {
                    CrossWarning = false;
                    var target = Target;
                    if (target != null &&
                        (FixMath.Abs(target.Pos.X - CrossMark.X) <= BossTuning.WardenCrossHalfWidth ||
                         FixMath.Abs(target.Pos.Y - CrossMark.Y) <= BossTuning.WardenCrossHalfWidth))
                        Sim.DealDamage(Self, target, BossTuning.WardenCrossDamage, DamageKind.Ability);
                    Sim.EmitBoss(BossEvent.WardenCrossFire, Self.Index, target?.Index ?? -1, BossTuning.WardenCrossHalfWidth, CrossMark);
                }
            }
            if (Steps >= BossTuning.WardenMaxSteps) { Warning = false; return; }
            var next = StepTick(Steps + 1);
            if (Sim.ActiveTick == next - BossTuning.WardenWarnTicks)
            {
                Warning = true;
                Sim.EmitBoss(BossEvent.WardenWarn, Self.Index, -1, SizeAfter(Sim.Config.ArenaWidth, Steps + 1), Self.Pos);
            }
            if (Sim.ActiveTick == next)
            {
                // Compare limits at THIS tick on both sides of our step: normal per-tick shrink is excluded.
                var before = Sim.BossLimitedArena;
                Steps++;
                var after = Sim.BossLimitedArena;
                movedWalls = 0;
                if (after.Left > before.Left) movedWalls |= (byte)(1 << (int)ArenaWall.Left);
                if (after.Right < before.Right) movedWalls |= (byte)(1 << (int)ArenaWall.Right);
                if (after.Top < before.Top) movedWalls |= (byte)(1 << (int)ArenaWall.Top);
                spikeUntilActiveTick = movedWalls != 0 ? Sim.ActiveTick + BossTuning.SpikeWindowTicks : 0;
                Warning = false;
                Sim.RequestPushIn();
                // The wall step pushes the player toward the edge; the warning shows when to turn inward.
                var target = Target;
                if (target != null)
                {
                    if (FixMath.Abs(target.Pos.X) > Sim.Arena.Width * Fix.Ratio(1, 4) ||
                        target.Pos.Y > Sim.Arena.Height * Fix.Ratio(1, 4))
                        Sim.DealDamage(Self, target, 18, DamageKind.Ability);
                    target.Vel += target.Pos.NormalizedOr(FixVec2.Right) * 4;
                }
                Sim.EmitBoss(BossEvent.WardenStep, Self.Index, -1, SizeAfter(Sim.Config.ArenaWidth, Steps), Self.Pos);
            }
        }

        /// <summary>Window includes the step tick, excludes step tick + SpikeWindowTicks, and pauses during hit-stop.</summary>
        public bool WallsSpiked => movedWalls != 0 && Sim.ActiveTick < spikeUntilActiveTick;

        /// <summary>Bit per ArenaWall for the currently spiked walls (always excludes Bottom); View reads the same mask.</summary>
        public byte SpikedWallMask => WallsSpiked ? movedWalls : (byte)0;

        public bool IsWallSpiked(ArenaWall wall) => (SpikedWallMask & (1 << (int)wall)) != 0;

        public override Fix WallSpikeDamage(BallState ball, ArenaWall wall) =>
            ball != Self && IsWallSpiked(wall) ? BossTuning.WardenSpikeDamage : Fix.Zero;

        public override bool ArenaLimit(out Fix width, out Fix height)
        {
            width = SizeAfter(Sim.Config.ArenaWidth, Steps);
            height = SizeAfter(Sim.Config.ArenaHeight, Steps);
            return Steps > 0;
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(h, Steps);
            h = SimHash.Mix(h, Warning);
            h = SimHash.Mix(h, CrossWarning);
            h = SimHash.Mix(h, CrossMark);
            h = SimHash.Mix(h, BossTuning.WardenCrossFirstTick);
            h = SimHash.Mix(h, BossTuning.WardenCrossCycleTicks);
            h = SimHash.Mix(h, BossTuning.WardenCrossWarnTicks);
            h = SimHash.Mix(h, BossTuning.WardenCrossHalfWidth);
            h = SimHash.Mix(h, BossTuning.WardenCrossDamage);
            h = SimHash.Mix(h, (int)movedWalls);
            h = SimHash.Mix(h, spikeUntilActiveTick);
            h = SimHash.Mix(h, BossTuning.SpikeWindowTicks);
            h = SimHash.Mix(h, BossTuning.WardenFirstStepTick);
            h = SimHash.Mix(h, BossTuning.WardenStepEveryTicks);
            h = SimHash.Mix(h, BossTuning.WardenWarnTicks);
            h = SimHash.Mix(h, BossTuning.WardenStepPct);
            h = SimHash.Mix(h, BossTuning.WardenSpikeDamage);
            return SimHash.Mix(h, BossTuning.WardenMinPct);
        }
    }
}
