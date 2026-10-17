using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Skills
{
    /// <summary>One independently hashed skill slot per real ball. Bind at fight start; never shared between balls.</summary>
    public sealed class SkillState
    {
        readonly MatchSim sim;
        readonly BallState self;
        public readonly SkillDefinition Definition;
        public readonly int Level;
        public readonly SkillBranch Branch;
        public bool Active { get; private set; }
        public int ReadyAt { get; private set; }
        public int EndAt { get; private set; }
        public int Charges { get; private set; }
        public int FireCount { get; private set; }
        internal int Rank => Level - 1;
        /// <summary>BB-19b: the Run player's own skill for this weapon (null for enemies, bosses and weapons without one).</summary>
        public ActiveSkill Act { get; private set; }
        internal bool A => Level > 1 && Branch == SkillBranch.A;
        internal bool B => Level > 1 && Branch == SkillBranch.B;
        /// <summary>BB-19: the Run player's skill is pressed by a tap (TapState) instead of firing by itself; enemies and bosses keep the automatic rule.</summary>
        public bool Manual => self.Tap != null;
        /// <summary>Manual skills recharge in TapTuning.ManualCooldownPercent of the automatic time.</summary>
        public int CooldownTicks => Act != null ? Act.CooldownTicks : Manual ? AutoCooldownTicks * Tap.TapTuning.ManualCooldownPercent / 100 : AutoCooldownTicks;
        public bool IsReady => sim.ActiveTick >= ReadyAt;
        public int FiredAt { get; private set; } = -1;
        int AutoCooldownTicks => Definition.WeaponId == "pike" ? 540 : Definition.WeaponId == "fang" || Definition.WeaponId == "aegis" || Definition.WeaponId == "rig" ? 720 :
            Definition.WeaponId == "volley" ? (A ? (Level == 2 ? 600 : 480) : 720) : 600;
        // BB-BAL1 (Run-only, levels 2-3): fang A per rank 90 -> 103, pike B 60 -> 69, brawler B 60 -> 69, aegis A 40 -> 46 (+15%).
        public int DurationTicks => Definition.WeaponId == "fang" ? (A ? 180 + Rank * 103 : 180) :
            Definition.WeaponId == "pike" ? (B ? 120 + Rank * 69 : 120) :
            Definition.WeaponId == "brawler" ? (B ? 90 + Rank * 69 : 90) :
            Definition.WeaponId == "venom" ? 240 : Definition.WeaponId == "aegis" ? (A ? 120 + Rank * 46 : 120) :
            Definition.WeaponId == "serpent" ? 90 : 0;   // BB-14 STRIKE 1.5 s; HYMN is instant
        public int HitCharges => A && Definition.WeaponId == "blade" ? 3 + Rank * 2 : 3;
        // BB-BAL1 (Run-only, levels 2-3, +15%): blade B Level/2 -> Level*23/40; fang B 1.5/2 -> 1.725/2.3; pike A (Level+1)/6 -> (Level+1)*23/120.
        public Fix HitBonus => B && Definition.WeaponId == "blade" ? Fix.Ratio(Level * 23, 40) :
            A && Definition.WeaponId == "blade" && self.Tree != null ? (Level == 3 ? Fix.Ratio(3, 4) : Fix.Ratio(5, 8)) : Fix.Half;   // BB-12g: Run EMBER A
        public Fix SpinGainMultiplier => B ? (Level == 2 ? Fix.Ratio(69, 40) : Fix.Ratio(23, 10)) : Fix.One;
        public Fix LengthBonus => A ? Fix.Ratio((Level + 1) * 23, 120) : Fix.Ratio(1, 3);
        public Fix BodyMultiplier => A ? (Level == 2 ? Fix.Ratio(161, 80) : Fix.Ratio(23, 10)) : Fix.Ratio(27, 20);   // BB-BAL1: base/B 1.5 -> 1.35 (-10%); A L2/L3 1.75/2 -> 2.0125/2.3 (+15%, Run only)
        public Fix ArrowBonus => B ? Fix.Ratio(Rank, 4) : Fix.Zero;
        // BB-BAL1 (Run-only, levels 2-3, +15%): venom A 2/3, 3/4 -> 23/30, 69/80; venom B 60 -> 69 per rank; aegis B 2/3, 3/4 -> 23/30, 69/80.
        public Fix DrainFraction => A ? (Level == 2 ? Fix.Ratio(23, 30) : Fix.Ratio(69, 80)) : Fix.Half;
        public int PoisonExtraTicks => B ? Rank * 69 : 0;
        public Fix ParryReflect => B ? (Level == 2 ? Fix.Ratio(23, 30) : Fix.Ratio(69, 80)) : Fix.Ratio(3, 5);
        public int VolleyCharges => A ? 2 + Rank * (self.Tree != null ? 2 : 1) : 2;   // BB-12g: Run A rank counts twice
        public int DeployCount => B ? Rank : 0;
        /// <summary>Cooldown progress 0..1.</summary>
        public Fix CooldownFraction => sim.ActiveTick >= ReadyAt ? Fix.One : FixMath.Clamp01(Fix.Ratio(CooldownTicks - (ReadyAt - sim.ActiveTick), CooldownTicks));
        /// <summary>The tap bar: the cooldown, or the skill's own gauge (wind-up, lit orbs).</summary>
        public Fix ReadyFraction => Act != null ? Act.Gauge() : CooldownFraction;
        /// <summary>A tap now would do something.</summary>
        public bool CanTap => Act != null ? Act.CanTap : IsReady;
        /// <summary>BB-19b: the skill is mid-action (dash, wind-up, parry window).</summary>
        public bool Running => Act != null && Act.Running;

        internal SkillState(MatchSim match, BallState ball, int level, SkillBranch branch)
        {
            _ = new SkillSpec(level, branch);
            sim = match; self = ball; Definition = SkillDefinition.ForWeapon(ball.Weapon.Id); Level = level; Branch = branch;
            ReadyAt = CooldownTicks;   // the Run player's TapState is attached right after (AttachTap)
        }
        /// <summary>Called by MatchSim after the ball's TapState exists: a manual skill starts half charged.</summary>
        internal void AttachTap()
        {
            if (!Manual) return;
            Act = ActiveSkill.For(sim, self, this);
            ReadyAt = CooldownTicks / 2;
        }
        /// <summary>BB-19b: an active skill was released: start its cooldown.</summary>
        internal void Spend(int cooldownTicks)
        {
            FiredAt = sim.ActiveTick; FireCount++; ReadyAt = sim.ActiveTick + cooldownTicks;
            sim.EmitSkill(SimEventType.SkillFired, self, this);
        }
        internal void Delay(int ticks) => ReadyAt += ticks;

        bool Condition()
        {
            if (Manual) return true;   // the player chose the moment
            if (self.Weapon is FangRule fang) return fang.SpinDegPerTick >= WeaponTuning.FangMaxSpin * Fix.Ratio(3, 4);
            if (self.Weapon is RigRule rig) return rig.TurretCount > 0;
            if (self.Weapon is VenomRule)
            {
                foreach (var ball in sim.Balls) if (ball != self && ball.Alive && ball.Status.PoisonStackCount >= 3) return true;
                return false;
            }
            return true;
        }

        internal void Tick()
        {
            Act?.Tick();
            if (Active && EndAt > 0 && sim.ActiveTick >= EndAt) End();
            if (!Manual && sim.ActiveTick >= ReadyAt && Condition()) Fire();
            if (self.Weapon is PikeRule pike) pike.RefreshLength();
            if (Active && self.Weapon is BrawlerRule) self.Vel = self.Vel.NormalizedOr(FixVec2.Right) * sim.SkillSpeedCap(self);
        }

        /// <summary>BB-19: the player's tap. True when the skill was ready and fired.</summary>
        internal bool TryFire()
        {
            if (!Manual) return false;
            if (Act != null) return Act.Tap();
            if (sim.ActiveTick < ReadyAt) return false;
            Fire();
            return true;
        }

        void Fire()
        {
            {
                if (Active) End();
                FiredAt = sim.ActiveTick;
                Active = true; FireCount++; ReadyAt = sim.ActiveTick + CooldownTicks;
                EndAt = DurationTicks > 0 ? sim.ActiveTick + DurationTicks : 0;
                Charges = self.Weapon is BladeRule ? HitCharges : self.Weapon is RigRule ? VolleyCharges : self.Weapon is BrawlerRule || self.Weapon is SerpentRule ? 1 : 0;
                sim.EmitSkill(SimEventType.SkillFired, self, this);
                if (self.Weapon is VolleyRule volley) { volley.FireVolley(ArrowBonus); End(); }
                if (self.Weapon is ChorusRule chorus) { chorus.Hymn(); End(); }   // BB-14 HYMN
                if (Manual) ManualEffect();
                if (self.Weapon is RigRule rig)
                {
                    if (Manual && rig.TurretCount == 0) rig.PlaceTurret();   // OVERCLOCK on tap always has a turret to speed up
                    rig.BeginOverclock();
                    for (var i = 0; i < DeployCount; i++) rig.PlaceTurret();
                }
            }
        }

        /// <summary>BB-19: what a tap adds to the old condition-gated skills so they work at any moment.</summary>
        void ManualEffect()
        {
            switch (self.Weapon)
            {
                case FangRule fang:   // FRENZY: spin up at once, then no decay
                    var top = WeaponTuning.FangMaxSpin * Fix.Ratio(3, 4);
                    if (fang.SpinDegPerTick < top) fang.SpinDegPerTick = top;
                    break;
                case BrawlerRule _:   // CHARGE: aim the rush at the nearest foe
                {
                    var foe = WeaponAim.NearestFoe(sim, self);
                    if (foe != null) self.Vel = (foe.Pos - self.Pos).NormalizedOr(self.Vel.NormalizedOr(FixVec2.Right)) * self.Vel.Length;
                    break;
                }
                case VenomRule venom:   // DRAIN: three stacks on the nearest foe, so the drain has something to feed on
                {
                    var foe = WeaponAim.NearestFoe(sim, self);
                    if (foe == null) break;
                    var dps = WeaponTuning.VenomDps * (Fix.One + self.Bonus.DamagePct);
                    for (var k = 0; k < 3 && foe.Status.PoisonStackCount < venom.MaxStacks; k++) sim.ApplyPoison(foe, self, dps, 3 * MatchConfig.TicksPerSecond);
                    break;
                }
            }
        }
        public Fix ModifyHit(Fix damage, DamageKind kind)
        {
            if (Act != null) return Act.ModifyHit(damage, kind);
            if (!Active || Charges <= 0 || kind != DamageKind.Weapon) return damage;
            if (self.Weapon is BladeRule) return damage + HitBonus;
            if (self.Weapon is SerpentRule) return damage * TributeTuning.SerpentStrikeBite;   // BB-14 STRIKE bite
            return self.Weapon is BrawlerRule ? damage * BodyMultiplier : damage;
        }
        internal void LandedHit(DamageKind kind)
        {
            if (!Active || Charges <= 0 || kind != DamageKind.Weapon) return;
            if (self.Weapon is BladeRule) { if (--Charges == 0) End(); }
            else if (self.Weapon is BrawlerRule || self.Weapon is SerpentRule) Charges = 0; // CHARGE speed / STRIKE hunt persist until the duration ends
        }
        internal void TurretVolley()
        {
            if (Active && self.Weapon is RigRule && Charges > 0 && --Charges == 0) End();
        }
        void End()
        {
            Active = false; EndAt = 0; Charges = 0;
            sim.EmitSkill(SimEventType.SkillEnded, self, this);
        }
        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Definition.Id); h = SimHash.Mix(h, Level); h = SimHash.Mix(h, (int)Branch);
            h = SimHash.Mix(h, Active); h = SimHash.Mix(h, ReadyAt); h = SimHash.Mix(h, EndAt);
            h = SimHash.Mix(SimHash.Mix(h, Charges), FireCount);
            return Act != null ? Act.HashInto(h) : h;
        }
    }
}
