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
        int Rank => Level - 1;
        bool A => Level > 1 && Branch == SkillBranch.A;
        bool B => Level > 1 && Branch == SkillBranch.B;
        public int CooldownTicks => Definition.WeaponId == "pike" ? 540 : Definition.WeaponId == "fang" || Definition.WeaponId == "aegis" || Definition.WeaponId == "rig" ? 720 :
            Definition.WeaponId == "volley" ? (A ? (Level == 2 ? 600 : 480) : 720) : 600;
        public int DurationTicks => Definition.WeaponId == "fang" ? (A ? 180 + Rank * 90 : 180) :
            Definition.WeaponId == "pike" ? (B ? 120 + Rank * 60 : 120) :
            Definition.WeaponId == "brawler" ? (B ? 90 + Rank * 60 : 90) :
            Definition.WeaponId == "venom" ? 240 : Definition.WeaponId == "aegis" ? (A ? 120 + Rank * 40 : 120) : 0;
        public int HitCharges => A && Definition.WeaponId == "blade" ? 3 + Rank * 2 : 3;
        public Fix HitBonus => B && Definition.WeaponId == "blade" ? Fix.Ratio(Level, 2) : Fix.Half;
        public Fix SpinGainMultiplier => B ? (Level == 2 ? Fix.Ratio(3, 2) : (Fix)2) : Fix.One;
        public Fix LengthBonus => A ? Fix.Ratio(Level + 1, 6) : Fix.Ratio(1, 3);
        public Fix BodyMultiplier => A ? (Level == 2 ? Fix.Ratio(7, 4) : (Fix)2) : Fix.Ratio(3, 2);
        public Fix ArrowBonus => B ? Fix.Ratio(Rank, 4) : Fix.Zero;
        public Fix DrainFraction => A ? (Level == 2 ? Fix.Ratio(2, 3) : Fix.Ratio(3, 4)) : Fix.Half;
        public int PoisonExtraTicks => B ? Rank * 60 : 0;
        public Fix ParryReflect => B ? (Level == 2 ? Fix.Ratio(2, 3) : Fix.Ratio(3, 4)) : Fix.Ratio(3, 5);
        public int VolleyCharges => A ? 2 + Rank : 2;
        public int DeployCount => B ? Rank : 0;
        public Fix ReadyFraction => sim.ActiveTick >= ReadyAt ? Fix.One : Fix.Ratio(CooldownTicks - (ReadyAt - sim.ActiveTick), CooldownTicks);

        internal SkillState(MatchSim match, BallState ball, int level, SkillBranch branch)
        {
            _ = new SkillSpec(level, branch);
            sim = match; self = ball; Definition = SkillDefinition.ForWeapon(ball.Weapon.Id); Level = level; Branch = branch;
            ReadyAt = CooldownTicks;
        }

        bool Condition()
        {
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
            if (Active && EndAt > 0 && sim.ActiveTick >= EndAt) End();
            if (sim.ActiveTick >= ReadyAt && Condition())
            {
                if (Active) End();
                Active = true; FireCount++; ReadyAt = sim.ActiveTick + CooldownTicks;
                EndAt = DurationTicks > 0 ? sim.ActiveTick + DurationTicks : 0;
                Charges = self.Weapon is BladeRule ? HitCharges : self.Weapon is RigRule ? VolleyCharges : self.Weapon is BrawlerRule ? 1 : 0;
                sim.EmitSkill(SimEventType.SkillFired, self, this);
                if (self.Weapon is VolleyRule volley) { volley.FireVolley(ArrowBonus); End(); }
                if (self.Weapon is RigRule rig)
                {
                    rig.BeginOverclock();
                    for (var i = 0; i < DeployCount; i++) rig.PlaceTurret();
                }
            }
            if (self.Weapon is PikeRule pike) pike.RefreshLength();
            if (Active && self.Weapon is BrawlerRule) self.Vel = self.Vel.NormalizedOr(FixVec2.Right) * sim.SkillSpeedCap(self);
        }
        public Fix ModifyHit(Fix damage, DamageKind kind)
        {
            if (!Active || Charges <= 0 || kind != DamageKind.Weapon) return damage;
            if (self.Weapon is BladeRule) return damage + HitBonus;
            return self.Weapon is BrawlerRule ? damage * BodyMultiplier : damage;
        }
        internal void LandedHit(DamageKind kind)
        {
            if (!Active || Charges <= 0 || kind != DamageKind.Weapon) return;
            if (self.Weapon is BladeRule) { if (--Charges == 0) End(); }
            else if (self.Weapon is BrawlerRule) Charges = 0; // CHARGE speed persists until its duration ends
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
            return SimHash.Mix(SimHash.Mix(h, Charges), FireCount);
        }
    }
}
