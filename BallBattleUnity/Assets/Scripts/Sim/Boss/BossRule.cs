using System;

namespace BallBattle.Sim.Boss
{
    /// <summary>What a boss event (SimEventType.Boss) announces; carried in SimEvent.Variant.</summary>
    public enum BossEvent : byte
    {
        /// <summary>A = boss, B = target ball.</summary>
        ShellWarn,
        /// <summary>A = boss, B = target ball, Point = centre of the shell arc on the boss's edge (it faces this way until ShellOff).</summary>
        ShellOn,
        ShellOff,
        /// <summary>A = boss, B = pulled ball.</summary>
        MagnetWarn,
        MagnetPull,
        MagnetEnd,
        /// <summary>A = boss (the copy), B = the ball it copied.</summary>
        MimicCopied,
        MimicAdapted,
        /// <summary>A = boss, Value = width the walls will have after the coming step.</summary>
        WardenWarn,
        /// <summary>A = boss, Value = new Warden width (the arena is the smaller of this and the normal shrink).</summary>
        WardenStep,
        HunterMark, HunterCharge, BellWarn, BellWave, EmberMark, EmberIgnite, PrismWarn, PrismBeam,
        MimicEchoWarn, MimicEchoStrike, WardenCrossWarn, WardenCrossFire
    }

    /// <summary>
    /// A boss mechanic (BB-04): attached to the boss ball by the Run's enemy generator (BallLoadout.Boss), never in Versus.
    /// One instance per boss ball per match (MatchSim enforces it). Timers count ActiveTick, so hit-stop never advances them.
    /// Hooks, in step order: OnActiveTick once per active tick right after the arena update and before any movement (phase
    /// changes, events, pull); ArenaLimit when the arena rect is built; BlocksHit inside the hit pipeline (pure: no side effects).
    /// A new boss = one subclass + HashState covering every mutable field and every tuning value it reads.
    /// </summary>
    public abstract class BossRule
    {
        public abstract string Id { get; }

        protected MatchSim Sim { get; private set; }
        protected BallState Self { get; private set; }

        internal bool IsBound => Sim != null;

        internal void Bind(MatchSim sim, BallState self)
        {
            if (Sim != null) throw new InvalidOperationException($"Boss rule '{Id}' instance is already used by a ball; create a new instance per ball and per match.");
            Sim = sim;
            Self = self;
        }

        /// <summary>Once per active tick, before movement. ActiveTick is already the new tick.</summary>
        public virtual void OnActiveTick() { }

        /// <summary>A Weapon/Projectile hit on Self at <paramref name="contact"/>: true = it deals 0 and counts as no hit (no growth, no on-hit hooks). Pure.</summary>
        public virtual bool BlocksHit(BallState attacker, FixVec2 contact) => false;

        /// <summary>Boss-specific resistance applied after traits to real damage, before HP changes.</summary>
        public virtual Fix ModifyIncomingDamage(Fix damage, DamageKind kind) => damage;

        /// <summary>Observe real HP loss so a boss can react to the player's attack mix.</summary>
        public virtual void OnDamageTaken(DamageKind kind, Fix damage) { }

        protected void DamageNear(FixVec2 point, Fix radius, Fix damage)
        {
            var target = Target;
            if (target != null && (target.Pos - point).LengthSq <= FixMath.Sq(radius))
                Sim.DealDamage(Self, target, damage, DamageKind.Ability);
        }

        /// <summary>
        /// BB-FIX2: the angle (tenths of a degree, 0..1800) this rule's BlocksHit judged for a hit by <paramref name="attacker"/> at
        /// <paramref name="contact"/>, or -1 when it judged none. The Sim puts it on the Hit/ProjectileHit event so a trace logs the
        /// exact number the block used. Pure.
        /// </summary>
        public virtual int HitArcAngleTenths(BallState attacker, FixVec2 contact) => -1;

        /// <summary>Upper bound on the arena size at the current ActiveTick, or false for none.</summary>
        public virtual bool ArenaLimit(out Fix width, out Fix height)
        {
            width = Fix.Zero;
            height = Fix.Zero;
            return false;
        }

        /// <summary>
        /// BB-6d: spike damage a wall contact deals to <paramref name="ball"/> from this boss (WARDEN's spiked walls), 0 for none. It
        /// shares the normal spike rules: one hit per contact, the higher of this and a spike zone, the same 10-ActiveTick immunity.
        /// </summary>
        public virtual Fix WallSpikeDamage(BallState ball, ArenaWall wall) => Fix.Zero;

        /// <summary>The ball this boss acts on: the first other ball still alive (Run mode: the player, ball 0). Null if none.</summary>
        protected BallState Target
        {
            get
            {
                foreach (var b in Sim.Balls)
                    if (b != Self && b.Alive) return b;
                return null;
            }
        }

        protected abstract ulong HashState(ulong h);

        public ulong HashInto(ulong h) => HashState(SimHash.Mix(h, Id));
    }
}
