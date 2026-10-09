using System;

namespace BallBattle.Sim
{
    /// <summary>
    /// The single damage pipeline plus the public API traits and weapons use to affect balls.
    /// Hit (Weapon/Projectile): base × (1 + DamagePct) → attacker outgoing traits → target shield → target
    /// incoming traits → apply HP → attacker weapon/traits OnHitDealt → target traits OnHitTaken.
    /// Other kinds (Status, Reflect, Hazard): target incoming traits → apply HP.
    /// Deaths are resolved by the calling step (ResolveDeaths), never in the middle of a hook; until then a
    /// ball at HP &lt;= 0 is "pending dead": it cannot act, be healed, damaged further or hit again.
    /// </summary>
    public sealed partial class MatchSim
    {
        /// <summary>Alive and not pending death.</summary>
        static bool Active(BallState b) => b != null && b.Alive && b.Hp > 0f;

        /// <summary>Damage a Weapon/Projectile hit will deal. No HP change yet; may use up a target shield charge.</summary>
        float ComputeHitDamage(BallState attacker, BallState target, float baseDamage, DamageKind kind, out bool blocked)
        {
            var d = baseDamage * (1f + attacker.Bonus.DamagePct);
            foreach (var t in attacker.Traits) d = t.ModifyOutgoingDamage(target, d, kind);
            blocked = d > 0f && target.Status.TryConsumeShield();
            if (blocked) return 0f;
            foreach (var t in target.Traits) d = t.ModifyIncomingDamage(attacker, d, kind);
            return d > 0f ? d : 0f;
        }

        /// <summary>HP part of an unblocked hit. Hooks run separately (RunHitHooks) so a trade can apply both HP changes first.</summary>
        static void ApplyHitHp(BallState attacker, BallState target, float damage)
        {
            target.Hp -= damage;
            target.LastAttacker = attacker.Index;
        }

        static void RunHitHooks(BallState attacker, BallState target, float damage, DamageKind kind)
        {
            attacker.Weapon.OnHitDealt(target, damage, kind);
            foreach (var t in attacker.Traits) t.OnHitDealt(target, damage, kind);
            foreach (var t in target.Traits) t.OnHitTaken(attacker, damage, kind);
        }

        /// <summary>
        /// Non-hit damage (Status, Reflect, Hazard): target's incoming traits, then HP. Source may be null.
        /// Returns the damage actually dealt and emits Damage (Variant = kind).
        /// </summary>
        public float DealDamage(BallState source, BallState target, float amount, DamageKind kind)
            => DealDamage(source, target, amount, kind, true);

        /// <summary>Internal callers that report the damage in their own event (poison pulse, walls, obstacles) pass emit = false.</summary>
        float DealDamage(BallState source, BallState target, float amount, DamageKind kind, bool emit)
        {
            if (kind == DamageKind.Weapon || kind == DamageKind.Projectile)
                throw new ArgumentException("Weapon/Projectile damage goes through hits, not DealDamage", nameof(kind));
            if (!Ongoing || !Active(target) || amount <= 0f) return 0f;

            var d = amount;
            foreach (var t in target.Traits) d = t.ModifyIncomingDamage(source, d, kind);
            if (d <= 0f) return 0f;

            target.Hp -= d;
            if (source != null) target.LastAttacker = source.Index;
            if (emit)
                Emit(SimEventType.Damage, source != null ? source.Index : -1, target.Index, d, target.Pos, (byte)kind);
            return d;
        }

        /// <summary>Restores HP up to MaxHp. Returns the amount actually restored (emits Heal when &gt; 0). Never revives a pending-dead ball.</summary>
        public float Heal(BallState ball, float amount)
        {
            if (!Ongoing || !Active(ball) || amount <= 0f || ball.Hp >= ball.MaxHp) return 0f;
            var before = ball.Hp;
            ball.Hp = MathF.Min(ball.MaxHp, ball.Hp + amount);
            var restored = ball.Hp - before;
            Emit(SimEventType.Heal, ball.Index, -1, restored, ball.Pos);
            return restored;
        }

        /// <summary>Adds a poison stack: <paramref name="dps"/> damage once per second; <paramref name="ticks"/> is rounded up to whole seconds.</summary>
        public void ApplyPoison(BallState target, BallState source, float dps, int ticks)
        {
            if (!Ongoing || !Active(target) || dps <= 0f || ticks <= 0) return;
            var src = source != null ? source.Index : -1;
            target.Status.AddPoison(dps, WholeSeconds(ticks), src);
            Emit(SimEventType.StatusApplied, target.Index, src, dps, target.Pos, (byte)StatusKind.Poison);
        }

        /// <summary>Adds shield charges; each blocks one Weapon/Projectile hit completely.</summary>
        public void AddShield(BallState target, int charges)
        {
            if (!Ongoing || !Active(target) || charges <= 0) return;
            target.Status.ShieldCharges += charges;
            Emit(SimEventType.StatusApplied, target.Index, -1, charges, target.Pos, (byte)StatusKind.Shield);
        }

        /// <summary>Heal <paramref name="perSecond"/> once per second; <paramref name="ticks"/> rounded up to whole seconds. Replaces any running regen.</summary>
        public void ApplyRegen(BallState target, float perSecond, int ticks)
        {
            if (!Ongoing || !Active(target) || perSecond <= 0f || ticks <= 0) return;
            target.Status.SetRegen(perSecond, WholeSeconds(ticks));
            Emit(SimEventType.StatusApplied, target.Index, -1, perSecond, target.Pos, (byte)StatusKind.Regen);
        }

        /// <summary>Effects pulse once per second, so durations are whole seconds (a 0.5 s poison still pulses once).</summary>
        static int WholeSeconds(int ticks)
        {
            const int p = StatusEffects.PulseTicks;
            return ticks >= int.MaxValue - p ? ticks : (ticks + p - 1) / p * p;
        }

        /// <summary>Once per active tick: advance status timers, apply poison pulses, then regen pulses.</summary>
        void TickStatusEffects()
        {
            if (!Ongoing) return;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var s = b.Status;
                var poison = s.TickPoison(out var mainSource);
                if (poison > 0f)
                {
                    var source = mainSource >= 0 ? balls[mainSource] : null;
                    var dealt = DealDamage(source, b, poison, DamageKind.Status, false);
                    if (dealt > 0f) Emit(SimEventType.StatusTick, b.Index, mainSource, dealt, b.Pos, (byte)StatusKind.Poison);
                }

                var regen = s.TickRegen();
                if (regen > 0f) Heal(b, regen);
            }
            ResolveDeaths();
        }
    }
}
