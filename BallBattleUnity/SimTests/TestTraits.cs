using System.Collections.Generic;
using BallBattle.Sim;

namespace BallBattle.SimTests
{
    /// <summary>Builds zero-gravity matches with balls parked at exact spots, for staged contact tests.</summary>
    static class Stage
    {
        public static MatchConfig StillConfig() => new MatchConfig
        {
            Gravity = 0f,
            MinHorizontalSpeed = 0f,
            ShrinkStartTick = int.MaxValue,
            CapTicks = int.MaxValue
        };

        public static MatchSim Build(MatchConfig cfg, params (BallLoadout loadout, Vec2 pos, float angle)[] setup)
        {
            var loadouts = new BallLoadout[setup.Length];
            for (var i = 0; i < setup.Length; i++) loadouts[i] = setup[i].loadout;
            var m = new MatchSim(cfg ?? StillConfig(), 1, loadouts);
            for (var i = 0; i < setup.Length; i++)
            {
                var b = m.Balls[i];
                b.Pos = setup[i].pos;
                b.Vel = Vec2.Zero;
                b.WeaponAngleDeg = setup[i].angle;
                b.SpinDir = 1;
            }
            return m;
        }

        public static BallLoadout L(WeaponRule w, params TraitRule[] traits) => new BallLoadout(w).With(traits);

        /// <summary>Keeps every ball parked (call after each Step) so a non-spinning blade stays in contact.</summary>
        public static void Park(MatchSim m, params Vec2[] positions)
        {
            for (var i = 0; i < positions.Length; i++)
            {
                m.Balls[i].Pos = positions[i];
                m.Balls[i].Vel = Vec2.Zero;
            }
        }
    }

    /// <summary>Logs every hook call as "tag:hook" into a shared list.</summary>
    sealed class RecordingTrait : TraitRule
    {
        readonly string tag;
        readonly List<string> log;
        public int Ticks;

        public RecordingTrait(string tag, List<string> log) { this.tag = tag; this.log = log; }

        public override string Id => "test-recording";
        public override void OnSpawn() => log.Add(tag + ":spawn");
        public override float ModifyOutgoingDamage(BallState target, float damage, DamageKind kind) { log.Add(tag + ":out"); return damage; }
        public override float ModifyIncomingDamage(BallState attacker, float damage, DamageKind kind) { log.Add(tag + ":in"); return damage; }
        public override void OnHitDealt(BallState target, float damage, DamageKind kind) => log.Add(tag + ":dealt");
        public override void OnHitTaken(BallState attacker, float damage, DamageKind kind) => log.Add(tag + ":taken");
        public override void OnParry(BallState other) => log.Add(tag + ":parry");
        public override void OnWall() => log.Add(tag + ":wall");
        public override void OnTick() => Ticks++;
        protected override ulong HashState(ulong h) => SimHash.Mix(h, Ticks);
    }

    /// <summary>Multiplies outgoing and incoming hit damage.</summary>
    sealed class ScaleTrait : TraitRule
    {
        readonly float outgoing, incoming;
        public ScaleTrait(float outgoing, float incoming) { this.outgoing = outgoing; this.incoming = incoming; }
        public override string Id => "test-scale";
        public override float ModifyOutgoingDamage(BallState target, float damage, DamageKind kind) => damage * outgoing;
        public override float ModifyIncomingDamage(BallState attacker, float damage, DamageKind kind) => damage * incoming;
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Sends a fraction of every hit taken back to the attacker as Reflect damage.</summary>
    sealed class ReflectTrait : TraitRule
    {
        readonly float ratio;
        public ReflectTrait(float ratio) => this.ratio = ratio;
        public override string Id => "test-reflect";
        public override void OnHitTaken(BallState attacker, float damage, DamageKind kind) => Sim.DealDamage(Self, attacker, damage * ratio, DamageKind.Reflect);
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Poisons whatever it hits.</summary>
    sealed class PoisonOnHitTrait : TraitRule
    {
        public override string Id => "test-poison";
        public override void OnHitDealt(BallState target, float damage, DamageKind kind) => Sim.ApplyPoison(target, Self, 1f, 180);
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Heals itself by the damage it deals (vampire-like).</summary>
    sealed class LifestealTrait : TraitRule
    {
        public override string Id => "test-lifesteal";
        public override void OnHitDealt(BallState target, float damage, DamageKind kind) => Sim.Heal(Self, damage);
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Once per match: survive a lethal blow at 25 HP.</summary>
    sealed class LastStandTrait : TraitRule
    {
        bool used;
        public override string Id => "test-last-stand";
        public override bool TryPreventDeath()
        {
            if (used) return false;
            used = true;
            Self.Hp = 25f;
            return true;
        }
        protected override ulong HashState(ulong h) => SimHash.Mix(h, used);
    }

    /// <summary>Starts every match with one shield charge (events must reach the first Step).</summary>
    sealed class StartShieldTrait : TraitRule
    {
        public override string Id => "test-start-shield";
        public override void OnSpawn() => Sim.AddShield(Self, 1);
        protected override ulong HashState(ulong h) => h;
    }

    /// <summary>Fires a shot at the nearest other ball every <c>interval</c> active ticks.</summary>
    sealed class ShooterRule : WeaponRule
    {
        readonly int interval;
        readonly ProjectileSpec spec;
        int timer;

        public ShooterRule(int interval = 30, float damage = 2f, int bounces = 1)
        {
            this.interval = interval;
            spec = new ProjectileSpec { Radius = 2f, Damage = damage, Bounces = bounces, LifetimeTicks = 240 };
        }

        public override string Id => "test-shooter";
        public override bool HasBlade => false;
        public override float Damage(in HitContext ctx) => 0f;

        public override void OnTick()
        {
            if (++timer < interval) return;
            timer = 0;
            for (var i = 0; i < Sim.Balls.Count; i++) // index loop: foreach over IReadOnlyList boxes an enumerator
            {
                var other = Sim.Balls[i];
                if (other == Self || !other.Alive) continue;
                var dir = (other.Pos - Self.Pos).NormalizedOr(new Vec2(1f, 0f));
                Sim.FireProjectile(Self, Self.Pos + dir * (Self.Radius + 3f), dir * 6f, spec);
                return;
            }
        }

        protected override ulong HashState(ulong h) => SimHash.Mix(h, timer);
    }
}
