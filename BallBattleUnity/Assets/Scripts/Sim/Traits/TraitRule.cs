using System;

namespace BallBattle.Sim
{
    /// <summary>What caused a piece of damage. Traits use it to decide what they react to.</summary>
    public enum DamageKind : byte
    {
        /// <summary>Blade or body hit. Goes through outgoing traits, shield, incoming traits, hit hooks.</summary>
        Weapon,
        /// <summary>Projectile hit. Same pipeline as Weapon.</summary>
        Projectile,
        /// <summary>Damage over time (poison). Incoming traits only; never blocked by shield.</summary>
        Status,
        /// <summary>Damage sent back by a trait (thorns). Incoming traits only; never triggers hit hooks (no loops).</summary>
        Reflect,
        /// <summary>Arena hazard (spikes). Incoming traits only.</summary>
        Hazard
    }

    /// <summary>
    /// A passive modifier on one ball. One instance per ball per match (MatchSim enforces it), since hooks
    /// may change its fields. Order is fixed so matches stay deterministic: per-ball hooks run by ball index,
    /// then trait index; hit hooks run attacker's traits, then target's (trait index within each ball).
    /// Modify* hooks must be pure (no side effects): they also run for hits a shield then blocks, and both
    /// directions of a trade are computed before either is applied. React in the On* hooks instead.
    /// A new trait = one subclass + HashState covering every mutable field it adds.
    /// </summary>
    public abstract class TraitRule
    {
        public abstract string Id { get; }

        /// <summary>1 or 2 (upgraded).</summary>
        public readonly int Level;

        /// <summary>Match this trait is bound to (null before Bind). Use it to heal, poison, deal damage, fire.</summary>
        protected MatchSim Sim { get; private set; }

        /// <summary>The ball that owns this trait.</summary>
        protected BallState Self { get; private set; }

        protected TraitRule(int level = 1)
        {
            if (level < 1 || level > 2) throw new ArgumentOutOfRangeException(nameof(level), "Trait level must be 1 or 2");
            Level = level;
        }

        internal bool IsBound => Sim != null;

        internal void Bind(MatchSim sim, BallState self)
        {
            if (Sim != null) throw new InvalidOperationException($"Trait '{Id}' instance is already used by a ball; create a new instance per ball and per match.");
            Sim = sim;
            Self = self;
        }

        /// <summary>After every ball has spawned, before the first tick.</summary>
        public virtual void OnSpawn() { }

        /// <summary>Self is attacking. Return the new damage (Weapon/Projectile only).</summary>
        public virtual float ModifyOutgoingDamage(BallState target, float damage, DamageKind kind) => damage;

        /// <summary>Self is being damaged (any kind). Return the new damage. Attacker may be null (hazard).</summary>
        public virtual float ModifyIncomingDamage(BallState attacker, float damage, DamageKind kind) => damage;

        /// <summary>Self landed a Weapon/Projectile hit that was not shield-blocked.</summary>
        public virtual void OnHitDealt(BallState target, float damage, DamageKind kind) { }

        /// <summary>Self took a Weapon/Projectile hit that was not shield-blocked.</summary>
        public virtual void OnHitTaken(BallState attacker, float damage, DamageKind kind) { }

        public virtual void OnParry(BallState other) { }
        public virtual void OnWall() { }

        /// <summary>Self's blade knocked back projectile <paramref name="slot"/>.</summary>
        public virtual void OnDeflect(int slot) { }

        /// <summary>
        /// Self is about to die: HP &lt;= 0 at a death check (after each weapon pair, projectile pass, obstacles,
        /// walls, status pulses, tick hooks — so possibly several times per tick). To save it, set Self.Hp &gt; 0
        /// and return true (e.g. Second Wind; keep a once-flag). The first trait that returns true wins; MatchSim
        /// emits Heal. Returning true with HP still &lt;= 0 is a bug in the trait and throws.
        /// </summary>
        public virtual bool TryPreventDeath() => false;

        /// <summary>Once per active (non-hitstop) tick, after movement and status effects.</summary>
        public virtual void OnTick() { }

        /// <summary>Mixes the subclass's own mutable fields into the hash. Return <paramref name="h"/> unchanged if there are none.</summary>
        protected abstract ulong HashState(ulong h);

        public ulong HashInto(ulong h) => HashState(SimHash.Mix(SimHash.Mix(h, Id), Level));
    }
}
