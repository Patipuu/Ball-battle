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
        static bool Active(BallState b) => b != null && b.Alive && b.Hp > Fix.Zero;

        /// <summary>
        /// Damage a Weapon/Projectile hit will deal. No HP change yet; may use up a target shield charge. A boss shell (BB-04) is
        /// checked first: then the hit deals 0, counts as blocked (no growth, no hooks), keeps the shield charge, and
        /// <paramref name="shelled"/> is true.
        /// </summary>
        Fix ComputeHitDamage(BallState attacker, BallState target, Fix baseDamage, DamageKind kind, FixVec2 contact, out bool blocked, out bool shelled,
            out int arcAngleTenths)
        {
            shelled = BossBlocks(attacker, target, contact, out arcAngleTenths);
            if (shelled)
            {
                blocked = true;
                return Fix.Zero;
            }
            var d = (attacker.Skill?.ModifyHit(baseDamage, kind) ?? baseDamage) * (Fix.One + attacker.Bonus.DamagePct);
            if (attacker.Tap != null) d = attacker.Tap.ApplyToHit(d, target);   // TAP-1 PERFECT
            foreach (var t in attacker.Traits) d = t.ModifyOutgoingDamage(target, d, kind);
            if (d > Fix.Zero && target.Skill?.Act != null && target.Skill.Act.Guard(attacker, d, kind))   // BB-19b: parry / dodge swallow the blow
            {
                blocked = true;
                return Fix.Zero;
            }
            blocked = d > Fix.Zero && target.Status.TryConsumeShield();
            if (blocked) return Fix.Zero;
            foreach (var t in target.Traits) d = t.ModifyIncomingDamage(attacker, d, kind);
            d = BossIncoming(target, d, kind);
            if (IsPlainVersus && target.Weapon is Weapons.ChorusRule)
            {
                if (attacker.Weapon.BodyAttacks) d *= Weapons.TributeTuning.ChorusVersusBodyDamageScale;
                else if (kind == DamageKind.Projectile) d *= Weapons.TributeTuning.ChorusVersusProjectileDamageScale;
            }
            return d > Fix.Zero ? d : Fix.Zero;
        }

        /// <summary>HP part of an unblocked hit. Hooks run separately (RunHitHooks) so a trade can apply both HP changes first.</summary>
        void ApplyHitHp(BallState attacker, BallState target, Fix damage, DamageKind kind)
        {
            target.Hp -= damage;
            target.LastAttacker = attacker.Index;
            BossTookDamage(target, kind, damage);
        }

        static void RunHitHooks(BallState attacker, BallState target, Fix damage, DamageKind kind)
        {
            attacker.Skill?.LandedHit(kind);
            attacker.Weapon.OnHitDealt(target, damage, kind);
            foreach (var t in attacker.Traits) t.OnHitDealt(target, damage, kind);
            target.Weapon.OnHitTaken(attacker, damage, kind);
            foreach (var t in target.Traits) t.OnHitTaken(attacker, damage, kind);
        }

        /// <summary>
        /// Non-hit damage (Status, Reflect, Hazard): target's incoming traits, then HP. Source may be null.
        /// Returns the damage actually dealt and emits Damage (Variant = kind).
        /// </summary>
        public Fix DealDamage(BallState source, BallState target, Fix amount, DamageKind kind)
            => DealDamage(source, target, amount, kind, true);

        /// <summary>Internal callers that report the damage in their own event (poison pulse, walls, obstacles) pass emit = false.</summary>
        Fix DealDamage(BallState source, BallState target, Fix amount, DamageKind kind, bool emit)
        {
            if (kind == DamageKind.Weapon || kind == DamageKind.Projectile)
                throw new ArgumentException("Weapon/Projectile damage goes through hits, not DealDamage", nameof(kind));
            if (!Ongoing || !Active(target) || amount <= Fix.Zero) return Fix.Zero;
            if (kind == DamageKind.Reflect && source?.Tap != null) amount = source.Tap.ApplyToReflect(amount, target);   // TAP-1 MIRROR guard

            var d = amount;
            foreach (var t in target.Traits) d = t.ModifyIncomingDamage(source, d, kind);
            d = BossIncoming(target, d, kind);
            if (IsPlainVersus && source != null && target.Weapon is Weapons.ChorusRule && kind == DamageKind.Ability)
                d *= Weapons.TributeTuning.ChorusVersusAbilityDamageScale;
            if (d <= Fix.Zero) return Fix.Zero;

            target.Hp -= d;
            BossTookDamage(target, kind, d);
            if (source != null) target.LastAttacker = source.Index;
            if (emit)
                Emit(SimEventType.Damage, source != null ? source.Index : -1, target.Index, d, target.Pos, (byte)kind);
            return d;
        }

        /// <summary>Restores HP up to MaxHp. Returns the amount actually restored (emits Heal when &gt; 0). Never revives a pending-dead ball.</summary>
        public Fix Heal(BallState ball, Fix amount)
        {
            if (!Ongoing || !Active(ball) || amount <= Fix.Zero || ball.Hp >= ball.MaxHp) return Fix.Zero;
            var before = ball.Hp;
            ball.Hp = FixMath.Min(ball.MaxHp, ball.Hp + amount);
            var restored = ball.Hp - before;
            Emit(SimEventType.Heal, ball.Index, -1, restored, ball.Pos);
            return restored;
        }

        /// <summary>Adds a poison stack: <paramref name="dps"/> damage once per second; <paramref name="ticks"/> is rounded up to whole seconds.</summary>
        public void ApplyPoison(BallState target, BallState source, Fix dps, int ticks)
        {
            if (!Ongoing || !Active(target) || dps <= Fix.Zero || ticks <= 0) return;
            var src = source != null ? source.Index : -1;
            if (source?.Skill != null && source.Skill.Active && source.Weapon is Weapons.VenomRule) ticks += source.Skill.PoisonExtraTicks;
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
        public void ApplyRegen(BallState target, Fix perSecond, int ticks)
        {
            if (!Ongoing || !Active(target) || perSecond <= Fix.Zero || ticks <= 0) return;
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
        Fix[] poisonSourceDamage;

        void TickStatusEffects()
        {
            if (!Ongoing) return;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var s = b.Status;
                if (poisonSourceDamage == null) poisonSourceDamage = new Fix[balls.Length];
                for (var i = 0; i < poisonSourceDamage.Length; i++) poisonSourceDamage[i] = Fix.Zero;
                var poison = s.TickPoison(out var mainSource, poisonSourceDamage);
                if (poison > Fix.Zero)
                {
                    var source = mainSource >= 0 ? balls[mainSource] : null;
                    var dealt = DealDamage(source, b, poison, DamageKind.Status, false);
                    if (dealt > Fix.Zero) Emit(SimEventType.StatusTick, b.Index, mainSource, dealt, b.Pos, (byte)StatusKind.Poison);
                    if (dealt > Fix.Zero)
                        for (var i = 0; i < balls.Length; i++)
                        {
                            var owner = balls[i];
                            if (poisonSourceDamage[i] <= Fix.Zero || owner.Skill == null || !owner.Skill.Active || !(owner.Weapon is Weapons.VenomRule)) continue;
                            var share = FixMath.MulDiv(dealt, poisonSourceDamage[i].Raw, poison.Raw);
                            Heal(owner, share * owner.Skill.DrainFraction);
                        }
                }

                var regen = s.TickRegen();
                if (regen > Fix.Zero) Heal(b, regen);
            }
            ResolveDeaths();
        }
    }
}
