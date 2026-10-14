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
        /// <summary>A = boss, Value = width the walls will have after the coming step.</summary>
        WardenWarn,
        /// <summary>A = boss, Value = new Warden width (the arena is the smaller of this and the normal shrink).</summary>
        WardenStep
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

        /// <summary>Upper bound on the arena size at the current ActiveTick, or false for none.</summary>
        public virtual bool ArenaLimit(out Fix width, out Fix height)
        {
            width = Fix.Zero;
            height = Fix.Zero;
            return false;
        }

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
