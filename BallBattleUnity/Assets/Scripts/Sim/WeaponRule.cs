using System;
using System.Globalization;

namespace BallBattle.Sim
{
    /// <summary>Facts about one landed hit, passed to the attacker's weapon rule.</summary>
    public readonly struct HitContext
    {
        public readonly float AttackerSpeed;
        public readonly float RelativeSpeed;

        public HitContext(float attackerSpeed, float relativeSpeed)
        {
            AttackerSpeed = attackerSpeed;
            RelativeSpeed = relativeSpeed;
        }
    }

    /// <summary>
    /// One weapon's behaviour and its per-ball mutable stats.
    /// Each instance belongs to exactly one ball in one match (MatchSim enforces this), because
    /// OnHit/OnParry/OnWall change its fields. A new weapon = one subclass: shape fields + Damage
    /// + how stats scale + HashState covering every field the subclass adds.
    /// </summary>
    public abstract class WeaponRule
    {
        public abstract string Id { get; }

        /// <summary>False for bodies with no blade (they cannot parry and are never parried).</summary>
        public virtual bool HasBlade => true;

        /// <summary>True when the ball's own body deals damage on contact.</summary>
        public virtual bool BodyAttacks => false;

        /// <summary>Blade = segment from center+dir*BladeInner to center+dir*(BladeInner+BladeLength).</summary>
        public float BladeInner = 12f;
        public float BladeLength = 20f;
        public float BladeThickness = 3f;
        public float SpinDegPerTick = 6f;

        /// <summary>Added to MatchConfig.MaxSpeed for this ball.</summary>
        public float MaxSpeedBonus;

        /// <summary>Ticks before this weapon may hit the same target again; -1 = MatchConfig.HitCooldownTicks.</summary>
        public int HitCooldownTicks = -1;

        /// <summary>Fraction of speed gained on every real wall bounce (0 = none). The speed cap still applies.</summary>
        public float WallSpeedBoost;

        /// <summary>Multiplier on MatchConfig.HitKnockback for this weapon's hits (low = target stays close for combos).</summary>
        public float KnockbackScale = 1f;

        /// <summary>HUD label of the stat that grows (e.g. "DMG"). Real weapons must override.</summary>
        public virtual string StatLabel => "";

        /// <summary>Current value of that stat.</summary>
        public virtual float StatValue => 0f;

        /// <summary>Stat formatted for the HUD (weapon decides precision), so the HUD needs no per-weapon code.</summary>
        public virtual string StatText => StatValue.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>Config of the match this weapon is bound to (null before Bind).</summary>
        protected MatchConfig Config { get; private set; }

        /// <summary>Match this weapon is in (null before the ball spawns). Use it to fire, poison, heal.</summary>
        protected MatchSim Sim { get; private set; }

        /// <summary>The ball holding this weapon.</summary>
        protected BallState Self { get; private set; }

        bool bound;
        internal bool IsBound => bound;

        /// <summary>Called by MatchSim. Throws if this instance already belongs to a ball.</summary>
        internal void Bind(MatchConfig config)
        {
            if (bound) throw new InvalidOperationException($"Weapon '{Id}' instance is already used by a ball; create a new instance per ball and per match.");
            bound = true;
            Config = config;
        }

        internal void Attach(MatchSim sim, BallState self)
        {
            Sim = sim;
            Self = self;
        }

        public abstract float Damage(in HitContext ctx);

        /// <summary>This weapon's own blade/body hit landed (not shield-blocked). Growth goes here.</summary>
        public virtual void OnHit(in HitContext ctx) { }

        /// <summary>Any Weapon or Projectile hit by this ball landed, after HP changed (poison on hit, +1 arrow per hit).</summary>
        public virtual void OnHitDealt(BallState target, float damage, DamageKind kind) { }

        public virtual void OnParry(BallState other) { }
        public virtual void OnWall() { }

        /// <summary>This ball's blade knocked back projectile <paramref name="slot"/> (it now belongs to this ball).</summary>
        public virtual void OnDeflect(int slot) { }

        /// <summary>Once per active (non-hitstop) tick, after movement. Ranged weapons fire here via Sim.FireProjectile.</summary>
        public virtual void OnTick() { }

        /// <summary>Mixes the subclass's own mutable fields into the hash. Return <paramref name="h"/> unchanged if there are none.</summary>
        protected abstract ulong HashState(ulong h);

        /// <summary>Hash of base shape fields + subclass state, for determinism checks.</summary>
        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Id);
            h = SimHash.Mix(h, BladeInner);
            h = SimHash.Mix(h, BladeLength);
            h = SimHash.Mix(h, BladeThickness);
            h = SimHash.Mix(h, SpinDegPerTick);
            h = SimHash.Mix(h, MaxSpeedBonus);
            h = SimHash.Mix(h, HitCooldownTicks);
            h = SimHash.Mix(h, WallSpeedBoost);
            h = SimHash.Mix(h, KnockbackScale);
            return HashState(h);
        }
    }
}
