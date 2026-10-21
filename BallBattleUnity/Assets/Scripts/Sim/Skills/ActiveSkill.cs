using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Skills
{
    /// <summary>BB-19b tuning for the Run player's tapped skills (not balanced yet). Tick counts are active ticks (60 per second).</summary>
    public static class SkillTuning
    {
        public static int Cooldown = 480;                         // 8 s between releases unless a skill says otherwise
        // EMBER FIRE TRAIL
        public static int EmberDashTicks = 22;
        public static int EmberTrailLife = 240;
        public static int EmberTrailPulse = 24;
        public static Fix EmberTrailRadius = 11;
        public static Fix EmberTrailBase = Fix.Ratio(3, 5);       // + 0.4 x blade damage per pulse
        // MANTIS PHANTOM
        public static int MantisDodgeTicks = 20;
        public static Fix MantisSlash = Fix.Ratio(5, 2);          // x fang damage
        public static Fix MantisCounter = Fix.Ratio(3, 4);        // of the slash, on a dodged hit
        // ANGLER HOOK
        public static Fix HookSpeed = 4;
        public static int HookLife = 45;
        public static Fix HookRadius = 5;
        public static Fix HookDamage = 2;                         // x pike damage
        public static Fix HookPull = 7;
        public static int HookStun = 60;
        // WRECKER CHARGE
        public static int WreckFull = 180;
        public static int WreckBackfire = 240;
        public static Fix WreckMultiplier = Fix.Ratio(12, 5);     // extra x at full charge
        public static int WreckBackfireStun = 45;
        // SPARKLER RICOCHET
        public static Fix BoltSpeed = 5;
        public static Fix BoltRadius = 4;
        public static Fix BoltDamage = Fix.Ratio(16, 5);          // x arrow damage
        public static int BoltBounces = 4;
        // SPORE CLOUD
        public static Fix CloudRadius = 38;
        public static int CloudLife = 300;
        public static Fix CloudPoison = Fix.Ratio(3, 2);          // x venom dps
        public static Fix CloudHeal = 3;
        public static int CloudSlowPercent = 60;
        // MIRROR PARRY
        public static int ParryTicks = 24;
        public static Fix ParryReflect = 3;
        public static int ParryStun = 60;
        public static int ParryMissPenalty = 90;
        // HIVE SWARM
        public static int Bees = 6;
        public static Fix BeeDamage = Fix.Ratio(9, 10);
        public static Fix BeeSpeed = Fix.Ratio(16, 5);
        public static int BeeLife = 300;
        public static int BeeStingGap = 20;
        // BGLGXLQ UNITY
        public static int UnityCooldown = 120;
        public static Fix UnityFullBonus = Fix.Ratio(3, 2);
        public static int UnityFadeTicks = 15 * MatchConfig.TicksPerSecond;
        // HUNTER COIL
        public static int CoilLunge = 36;
        public static int CoilLife = 90;
        public static int CoilBites = 3;
        public static Fix CoilBite = Fix.Ratio(11, 5);
    }

    /// <summary>
    /// BB-19b: the Run player's own skill for one weapon. A tap calls Tap(); nothing here fires by itself. Enemies and bosses keep the
    /// automatic SkillState rules, so Versus and enemy balance do not change. Subclasses keep their own state and hash it.
    /// </summary>
    public abstract class ActiveSkill
    {
        protected readonly MatchSim Sim;
        protected readonly BallState Self;
        protected readonly SkillState Skill;

        protected ActiveSkill(MatchSim sim, BallState self, SkillState skill) { Sim = sim; Self = self; Skill = skill; }

        internal static ActiveSkill For(MatchSim sim, BallState self, SkillState skill)
        {
            switch (self.Weapon)
            {
                case BladeRule _: return new EmberTrail(sim, self, skill);
                case FangRule _: return new MantisPhantom(sim, self, skill);
                case PikeRule _: return new AnglerHook(sim, self, skill);
                case BrawlerRule _: return new WreckerCharge(sim, self, skill);
                case VolleyRule _: return new SparklerRicochet(sim, self, skill);
                case VenomRule _: return new SporeCloud(sim, self, skill);
                case AegisRule _: return new MirrorParry(sim, self, skill);
                case RigRule _: return new HiveSwarm(sim, self, skill);
                case ChorusRule _: return new ChorusUnityHand(sim, self, skill);
                case SerpentRule _: return new HunterCoil(sim, self, skill);
            }
            return null;
        }

        protected static bool Live(BallState b) => b != null && b.Alive && b.Hp > Fix.Zero;
        protected bool A => Skill.A;
        protected bool B => Skill.B;
        /// <summary>BB-19c: skill level 3 (the branch effect is stronger than at level 2).</summary>
        protected bool L3 => Skill.Level >= 3;
        /// <summary>BB-19c: the tree's FINALE for this ball ('A', 'B' or '-'); the finales live in the skills now.</summary>
        protected char Fin => Self.Tree != null ? Self.Tree.Finale : '-';
        protected int Growth => Self.Tree != null ? Self.Tree.Growth : 0;   // INSTINCT rank
        protected bool FinA => Fin == 'A';
        protected bool FinB => Fin == 'B';
        protected void Proc(Fix value) => Sim.EmitFx(SkillFxEvent.FinaleProc, Self.Index, -1, value, Self.Pos);
        protected Fix Mult => Fix.One + Self.Bonus.DamagePct;
        protected BallState Foe => Sim.NearestEnemy(Self);
        protected FixVec2 Heading => Self.Vel.NormalizedOr(FixVec2.Right);

        protected FixVec2 ClampToArena(FixVec2 p)
        {
            var a = Sim.Arena;
            var m = Self.Radius;
            return new FixVec2(FixMath.Clamp(p.X, a.Left + m, a.Right - m), FixMath.Clamp(p.Y, a.Bottom + m, a.Top - m));
        }

        public virtual int CooldownTicks => SkillTuning.Cooldown;
        /// <summary>False while a tap would do nothing (cooldown, nothing to release).</summary>
        public virtual bool CanTap => Skill.IsReady;
        /// <summary>Bot turn (Rules 23): first cast at BallState.BotFirstTick, then BotRepeatTicks after the skill is ready again (RunTuning per stage).</summary>
        protected bool BotTurn => Skill.FireCount == 0 ? Sim.ActiveTick >= Self.BotFirstTick : Sim.ActiveTick - Skill.ReadyAt >= Self.BotRepeatTicks;
        /// <summary>Run enemies (BallLoadout.AutoSkill): press now? Default: the skill can be used and it is the bot's turn.</summary>
        internal virtual bool BotWantsTap => CanTap && BotTurn;
        /// <summary>The tap bar: 0..1. Default is the cooldown; a skill with a build-up or a wind-up shows that instead.</summary>
        public virtual Fix Gauge() => Skill.CooldownFraction;
        /// <summary>True while the skill is doing something the player should see (dash, wind-up, parry window).</summary>
        public virtual bool Running => false;

        /// <summary>The player's tap. True when it did something (the tap bar flashes).</summary>
        internal virtual bool Tap()
        {
            if (!Skill.IsReady || !Release()) return false;
            Skill.Spend(CooldownTicks);
            return true;
        }

        protected virtual bool Release() => false;
        internal virtual void Tick() { }
        internal virtual Fix ModifyHit(Fix damage, DamageKind kind) => damage;
        /// <summary>A Weapon/Projectile hit of <paramref name="damage"/> is about to land on this ball: true = swallowed (parry, dodge).</summary>
        internal virtual bool Guard(BallState attacker, Fix damage, DamageKind kind) => false;
        internal virtual ulong HashInto(ulong h) => h;
    }

    /// <summary>EMBER: dash along the current heading and leave a trail of fire that burns the foe for four seconds.</summary>
    public sealed class EmberTrail : ActiveSkill
    {
        int dashUntil = -1;
        // FINALE state: who burns, when last lit, when the burn ends, how hard it bites
        int burnTarget = -1, burnLast, burnUntil = -1;
        Fix burnPower;
        internal EmberTrail(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }
        public override bool Running => Sim.ActiveTick < dashUntil;

        protected override bool Release()
        {
            var dir = Heading;
            Self.Vel = dir * Sim.SkillSpeedCap(Self);
            dashUntil = Sim.ActiveTick + SkillTuning.EmberDashTicks * (A ? 5 : 4) / 4;
            Sim.EmitFx(SkillFxEvent.EmberDash, Self.Index, -1, Fix.Zero, Self.Pos);
            return true;
        }

        /// <summary>The trail just burned <paramref name="b"/> (called by the Fx rules).</summary>
        internal void Ignite(BallState b, Fix power)
        {
            burnTarget = b.Index; burnLast = Sim.ActiveTick; burnPower = power;
            burnUntil = Sim.ActiveTick + (FinA ? 150 : 36);   // SMOLDER: 2 s more after leaving the trail
        }

        bool Burning(BallState b) => b != null && b.Index == burnTarget && Sim.ActiveTick < burnUntil;

        internal override Fix ModifyHit(Fix damage, DamageKind kind)
        {
            if (!FinB || kind != DamageKind.Weapon || !Burning(Foe)) return damage;
            Proc(Fix.One);
            return damage * Fix.Ratio(5, 4);   // BACKDRAFT
        }

        internal override void Tick()
        {
            if (FinA && Sim.ActiveTick < burnUntil && Sim.ActiveTick - burnLast >= SkillTuning.EmberTrailPulse && Sim.ActiveTick % SkillTuning.EmberTrailPulse == 0)
            {
                var f = Foe;
                if (f != null && f.Index == burnTarget)
                {
                    Sim.DealDamage(Self, f, burnPower * Fix.Half, DamageKind.Ability);   // SMOLDER
                    Sim.EmitFx(SkillFxEvent.TrailBurn, Self.Index, f.Index, burnPower * Fix.Half, f.Pos);
                }
            }
            if (Sim.ActiveTick >= dashUntil) return;
            Self.Vel = Self.Vel.NormalizedOr(FixVec2.Right) * Sim.SkillSpeedCap(Self);
            if ((dashUntil - Sim.ActiveTick) % 2 != 0) return;
            var blade = Self.Weapon as BladeRule;
            var dmg = (SkillTuning.EmberTrailBase + (blade != null ? blade.CurrentDamage * Fix.Ratio(2, 5) : Fix.Zero)) * Mult * (B ? (L3 ? Fix.Ratio(2, 1) : Fix.Ratio(17, 10)) : Fix.One);
            Sim.SpawnFx(new FxItem
            {
                Kind = FxKind.FireTrail, Owner = Self.Index, Target = -1, Pos = Self.Pos,
                Radius = SkillTuning.EmberTrailRadius * (B ? Fix.Ratio(4, 5) : Fix.One), Power = dmg,
                Life = SkillTuning.EmberTrailLife * (A ? (L3 ? 7 : 6) : B ? 4 : 5) / (A ? 4 : 5) * (FinB ? 17 : 20) / 20
            });
        }

        internal override ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, dashUntil);
            if (burnUntil < 0) return h;
            return SimHash.Mix(SimHash.Mix(SimHash.Mix(SimHash.Mix(h, burnTarget), burnLast), burnUntil), burnPower);
        }
    }

    /// <summary>MANTIS: blink behind the foe and slash at once. For a moment the blink also dodges: a blow that lands then is swallowed and answered.</summary>
    public sealed class MantisPhantom : ActiveSkill
    {
        int dodgeUntil = -1;
        int afterspinReadyAt;
        internal MantisPhantom(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }
        public override bool Running => Sim.ActiveTick < dodgeUntil;
        public override int CooldownTicks => FinB ? base.CooldownTicks * 11 / 10 : base.CooldownTicks;   // BLOODLUST: +10%

        Fix SlashDamage(BallState foe)
        {
            var fang = Self.Weapon as FangRule;
            var d = (fang != null ? fang.CurrentDamage : Fix.One) * SkillTuning.MantisSlash * Mult;
            if (B && foe.HpFraction < (L3 ? Fix.Ratio(9, 20) : Fix.Ratio(3, 10))) d *= 2;
            if (FinB) d *= Fix.Ratio(6, 5);   // BLOODLUST: +20%
            return d;
        }

        protected override bool Release()
        {
            var foe = Foe;
            if (foe == null) return false;
            var from = Self.Pos;
            var back = (-foe.Vel).NormalizedOr((from - foe.Pos).NormalizedOr(FixVec2.Right));
            Self.Pos = ClampToArena(foe.Pos + back * (foe.Radius + Self.Radius + 2));
            var face = (foe.Pos - Self.Pos).NormalizedOr(FixVec2.Right);
            var speed = Self.Vel.Length;
            Self.Vel = face * (speed > 5 ? speed : 5);
            Sim.SpawnFx(new FxItem
            {
                Kind = FxKind.Ghost, Owner = Self.Index, Target = -1, Pos = from, Radius = Self.Radius + 6,
                Power = A ? SlashDamage(foe) * (L3 ? Fix.Ratio(9, 10) : Fix.Ratio(3, 5)) : Fix.Zero, Life = 30
            });
            Sim.EmitFx(SkillFxEvent.MantisBlink, Self.Index, foe.Index, Fix.Zero, from);
            var dealt = Sim.DealDamage(Self, foe, SlashDamage(foe), DamageKind.Ability);
            Sim.EmitFx(SkillFxEvent.MantisSlash, Self.Index, foe.Index, dealt, foe.Pos);
            dodgeUntil = Sim.ActiveTick + SkillTuning.MantisDodgeTicks * (A ? (L3 ? 7 : 6) : 4) / 4;
            if (FinA && Sim.ActiveTick >= afterspinReadyAt && Self.Weapon is FangRule fr)
            {
                fr.AfterspinHits = 3;   // AFTERSPIN: the next three hits spin up 50% faster
                afterspinReadyAt = Sim.ActiveTick + 12 * MatchConfig.TicksPerSecond;
                Proc(Fix.One);
            }
            return true;
        }

        internal override bool Guard(BallState attacker, Fix damage, DamageKind kind)
        {
            if (Sim.ActiveTick >= dodgeUntil) return false;
            dodgeUntil = -1;
            var counter = Sim.DealDamage(Self, attacker, SlashDamage(attacker) * SkillTuning.MantisCounter, DamageKind.Ability);
            Sim.EmitFx(SkillFxEvent.MantisDodge, Self.Index, attacker.Index, counter, Self.Pos);
            return true;
        }

        internal override ulong HashInto(ulong h) => FinA ? SimHash.Mix(SimHash.Mix(h, dodgeUntil), afterspinReadyAt) : SimHash.Mix(h, dodgeUntil);
    }

    /// <summary>ANGLER: throw a slow hook. If it catches the foe, the line reels the foe in and holds it.</summary>
    public sealed class AnglerHook : ActiveSkill
    {
        internal AnglerHook(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }

        protected override bool Release()
        {
            var foe = Foe;
            if (foe == null) return false;
            var dir = (foe.Pos - Self.Pos).NormalizedOr(FixVec2.Right);
            var speed = SkillTuning.HookSpeed * (A ? (L3 ? Fix.Ratio(29, 20) : Fix.Ratio(13, 10)) : Fix.One);
            var pike = Self.Weapon as PikeRule;
            var power = (pike != null ? pike.CurrentDamage : Fix.One) * SkillTuning.HookDamage * Mult * (B ? (L3 ? Fix.Ratio(7, 4) : Fix.Ratio(3, 2)) : Fix.One) * (FinB ? Fix.Ratio(5, 4) : Fix.One);   // YANK: +25% damage
            var slot = Sim.SpawnFx(new FxItem
            {
                Kind = FxKind.Hook, Owner = Self.Index, Target = -1, Pos = Self.Pos + dir * (Self.Radius + 2), Vel = dir * speed,
                Radius = SkillTuning.HookRadius * (A ? (L3 ? Fix.Ratio(13, 10) : Fix.Ratio(6, 5)) : B ? Fix.Ratio(17, 20) : Fix.One), Power = power,
                Extra = (SkillTuning.HookPull + (B ? (L3 ? 3 : 2) : 0)) * (FinB ? Fix.Ratio(6, 5) : Fix.One),   // YANK: +20% pull
                Life = SkillTuning.HookLife * (A ? (L3 ? 29 : 26) : 20) / 20 * (FinA ? 112 : FinB ? 90 : 100) / 100,   // LONG CAST +12% reach / YANK -10%
                Aux2 = (B ? SkillTuning.HookStun * (L3 ? 19 : 16) / 10 : SkillTuning.HookStun) * (FinA ? 9 : 10) / 10   // LONG CAST: -10% stun
            });
            if (slot < 0) return false;
            Sim.EmitFx(SkillFxEvent.HookThrown, Self.Index, foe.Index, Fix.Zero, Self.Pos);
            return true;
        }
    }

    /// <summary>WRECKER: first tap winds up (stands still, charges), second tap rams the foe; the longer the charge, the harder. Too long and it backfires.</summary>
    public sealed class WreckerCharge : ActiveSkill
    {
        bool charging;
        int chargeStart;
        int rushUntil = -1;
        Fix impact = Fix.One;
        internal WreckerCharge(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }

        int Full => B ? SkillTuning.WreckFull / 2 : SkillTuning.WreckFull;
        int Backfire => B ? SkillTuning.WreckBackfire * 5 / 8 : A ? SkillTuning.WreckBackfire * (L3 ? 6 : 5) / 4 : SkillTuning.WreckBackfire;
        Fix Cap => B ? (L3 ? Fix.Ratio(7, 8) : Fix.Ratio(3, 4)) : Fix.One;
        public bool Charging => charging;
        /// <summary>0..1 of the way to full charge (only meaningful while charging).</summary>
        public Fix Charge => charging ? FixMath.Min(Fix.One, Fix.Ratio(Sim.ActiveTick - chargeStart, Full)) : Fix.Zero;
        public override bool Running => charging || Sim.ActiveTick < rushUntil;
        public override Fix Gauge() => charging ? Charge : Skill.CooldownFraction;
        public override bool CanTap => charging || Skill.IsReady;
        /// <summary>A bot winds up on its turn and rams at full charge (never into the backfire).</summary>
        internal override bool BotWantsTap => charging ? Charge >= Fix.One : Skill.IsReady && BotTurn;

        internal override bool Tap()
        {
            if (charging) { Launch(Charge * Cap, true); return true; }
            if (!Skill.IsReady) return false;
            charging = true;
            chargeStart = Sim.ActiveTick;
            Self.Vel = FixVec2.Zero;
            Self.StunTicks = FxPool.Max(Self.StunTicks, 2);
            Sim.EmitFx(SkillFxEvent.WreckCharge, Self.Index, -1, Fix.Zero, Self.Pos);
            return true;
        }

        void Launch(Fix power, bool clean)
        {
            charging = false;
            Self.StunTicks = clean ? 0 : FinA ? SkillTuning.WreckBackfireStun / 2 : FinB ? SkillTuning.WreckBackfireStun * 3 / 2 : SkillTuning.WreckBackfireStun;   // STEADY / OVERCHARGE
            var foe = Foe;
            var dir = foe != null ? (foe.Pos - Self.Pos).NormalizedOr(FixVec2.Right) : Heading;
            Self.Vel = dir * (Sim.SkillSpeedCap(Self) * (Fix.Ratio(3, 5) + Fix.Ratio(2, 5) * power));
            rushUntil = Sim.ActiveTick + 30 + (int)(40 * power.Raw / Fix.OneRaw);
            impact = clean ? Fix.One + SkillTuning.WreckMultiplier * power : Fix.One;
            if (clean && FinB && Sim.ActiveTick - chargeStart >= Full) impact *= Fix.Ratio(13, 10);   // OVERCHARGE: held past full = +30%
            Skill.Spend(CooldownTicks);
            Sim.EmitFx(clean ? SkillFxEvent.WreckLaunch : SkillFxEvent.WreckBackfire, Self.Index, foe != null ? foe.Index : -1, power, Self.Pos);
        }

        internal override void Tick()
        {
            if (charging)
            {
                Self.Vel = FixVec2.Zero;
                Self.StunTicks = FxPool.Max(Self.StunTicks, 2);
                if (Sim.ActiveTick - chargeStart >= Backfire) Launch(Fix.Ratio(1, 5), false);
                return;
            }
            if (Sim.ActiveTick < rushUntil && Self.StunTicks == 0)
                Self.Vel = Self.Vel.NormalizedOr(FixVec2.Right) * Sim.SkillSpeedCap(Self);
        }

        internal override Fix ModifyHit(Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon || impact <= Fix.One || Sim.ActiveTick >= rushUntil) return damage;
            var d = damage * impact;
            Sim.EmitFx(SkillFxEvent.WreckImpact, Self.Index, -1, impact, Self.Pos);
            impact = Fix.One;
            return d;
        }

        internal override ulong HashInto(ulong h) => SimHash.Mix(SimHash.Mix(SimHash.Mix(SimHash.Mix(h, charging), chargeStart), rushUntil), impact);
    }

    /// <summary>SPARKLER: fire one big bolt along the blade, bouncing off the walls. The blade turns, so the tap picks the angle.</summary>
    public sealed class SparklerRicochet : ActiveSkill
    {
        internal SparklerRicochet(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }

        protected override bool Release()
        {
            var dir = FixVec2.FromAngleDeg(Self.WeaponAngleDeg);
            var volley = Self.Weapon as VolleyRule;
            var spec = new ProjectileSpec
            {
                Radius = SkillTuning.BoltRadius * (B ? (L3 ? Fix.Ratio(7, 4) : Fix.Ratio(3, 2)) : Fix.One),
                Damage = (volley != null ? volley.ArrowDamage : Fix.One) * SkillTuning.BoltDamage * (B ? (L3 ? Fix.Ratio(9, 4) : Fix.Ratio(19, 10)) : Fix.One),
                Bounces = A ? SkillTuning.BoltBounces + (L3 ? 3 : 2) : B ? 2 : SkillTuning.BoltBounces,
                Hits = FinB ? 4 : 1, LifetimeTicks = 360, GravityScale = Fix.Zero,
                Tag = (byte)(FinA ? 1 : FinB ? 2 : 0)   // SPLIT (1) / CHAIN (2)
            };
            var slot = Sim.FireProjectile(Self, Self.Pos + dir * (Self.Radius + 6), dir * SkillTuning.BoltSpeed * (FinB ? Fix.Ratio(4, 5) : Fix.One), spec);
            if (slot < 0) return false;
            Sim.EmitFx(SkillFxEvent.BoltFired, Self.Index, slot, spec.Damage, Self.Pos);
            return true;
        }
    }

    /// <summary>SPORE: breathe out a spore cloud that grows, then fades. The foe inside is poisoned and slowed; the owner inside heals.</summary>
    public sealed class SporeCloud : ActiveSkill
    {
        internal SporeCloud(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }

        protected override bool Release()
        {
            var slot = Sim.SpawnFx(new FxItem
            {
                Kind = FxKind.Cloud, Owner = Self.Index, Target = -1, Pos = Self.Pos,
                Radius = SkillTuning.CloudRadius * (A ? (L3 ? Fix.Ratio(31, 20) : Fix.Ratio(27, 20)) : B ? Fix.Ratio(4, 5) : Fix.One),
                Power = WeaponTuning.VenomDps * SkillTuning.CloudPoison * Mult * (B ? (L3 ? Fix.Ratio(11, 5) : Fix.Ratio(9, 5)) : Fix.One) * Fix.Ratio(100 + 3 * Growth, 100),   // SEEP also feeds the cloud
                Extra = SkillTuning.CloudHeal * (B ? Fix.Ratio(2, 5) : Fix.One),
                Life = SkillTuning.CloudLife * (A ? (L3 ? 15 : 13) : 10) / 10, Aux = B ? 40 : SkillTuning.CloudSlowPercent,
                Aux2 = FinA ? 1 : FinB ? 2 : 0   // OUTBREAK (1): a small cloud is left behind / LINGER (2): longer poison
            });
            if (slot < 0) return false;
            Sim.EmitFx(SkillFxEvent.CloudOpen, Self.Index, -1, Fix.Zero, Self.Pos);
            return true;
        }
    }

    /// <summary>MIRROR: open a short parry window. A blow that lands inside it is swallowed and thrown back harder, and the foe is stunned. A window that catches nothing costs extra cooldown.</summary>
    public sealed class MirrorParry : ActiveSkill
    {
        int parryUntil = -1;
        bool caught;
        internal MirrorParry(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }
        public override bool Running => Sim.ActiveTick < parryUntil;

        protected override bool Release()
        {
            parryUntil = Sim.ActiveTick + (A ? (L3 ? SkillTuning.ParryTicks * 2 : SkillTuning.ParryTicks * 5 / 3) : B ? SkillTuning.ParryTicks * 7 / 12 : SkillTuning.ParryTicks);
            caught = false;
            Sim.EmitFx(SkillFxEvent.ParryOpen, Self.Index, -1, Fix.Zero, Self.Pos);
            return true;
        }

        internal override bool Guard(BallState attacker, Fix damage, DamageKind kind)
        {
            if (Sim.ActiveTick >= parryUntil) return false;
            caught = true;
            parryUntil = -1;
            var back = Sim.DealDamage(Self, attacker, damage * SkillTuning.ParryReflect * (B ? (L3 ? Fix.Ratio(7, 4) : Fix.Ratio(3, 2)) : Fix.One) * (FinA ? Fix.Ratio(3, 2) : Fix.One), DamageKind.Reflect);   // RIPOSTE: +50%
            if (FinB) Skill.Delay(-(CooldownTicks * 6 / 10));   // FLOW: 60% of the cooldown back
            attacker.StunTicks = FxPool.Max(attacker.StunTicks, B ? (L3 ? SkillTuning.ParryStun * 3 / 2 : SkillTuning.ParryStun * 4 / 3) : SkillTuning.ParryStun);
            Sim.EmitFx(SkillFxEvent.ParryHit, Self.Index, attacker.Index, back, Self.Pos);
            return true;
        }

        internal override void Tick()
        {
            if (parryUntil < 0 || Sim.ActiveTick < parryUntil) return;
            parryUntil = -1;
            if (caught) return;
            Skill.Delay((B ? SkillTuning.ParryMissPenalty * 2 / 3 : SkillTuning.ParryMissPenalty) * (FinB ? 5 : 4) / 4);   // FLOW: a miss costs 25% more
            Sim.EmitFx(SkillFxEvent.ParryMiss, Self.Index, -1, Fix.Zero, Self.Pos);
        }

        internal override ulong HashInto(ulong h) => SimHash.Mix(SimHash.Mix(h, parryUntil), caught);
    }

    /// <summary>HIVE: release a swarm of drones that hunt the foe and sting it for a few seconds.</summary>
    public sealed class HiveSwarm : ActiveSkill
    {
        int queenUntil = -1;
        internal HiveSwarm(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }

        /// <summary>QUEEN: one drone circles the hive and swallows one blow while the swarm is out.</summary>
        internal override bool Guard(BallState attacker, Fix damage, DamageKind kind)
        {
            if (!FinA || queenUntil < 0 || Sim.ActiveTick >= queenUntil) return false;
            queenUntil = -1;
            Proc(damage);
            return true;
        }

        internal override ulong HashInto(ulong h) => queenUntil >= 0 ? SimHash.Mix(h, queenUntil) : h;

        protected override bool Release()
        {
            var n = (A ? SkillTuning.Bees * (L3 ? 7 : 6) / 4 : B ? SkillTuning.Bees * 2 / 3 : SkillTuning.Bees) + (FinB ? 2 : 0);   // STING STORM: +2 drones
            var dmg = SkillTuning.BeeDamage * Mult * (A ? Fix.Ratio(7, 10) : B ? (L3 ? Fix.Ratio(9, 4) : Fix.Ratio(19, 10)) : Fix.One) * (FinB ? Fix.Ratio(3, 4) : Fix.One);   // ... each 25% weaker
            var spawned = 0;
            for (var i = 0; i < n; i++)
            {
                var dir = FixVec2.FromAngleDeg(FixMath.Wrap(Fix.Ratio(360 * i, n), 360));
                var slot = Sim.SpawnFx(new FxItem
                {
                    Kind = FxKind.Bee, Owner = Self.Index, Target = -1, Pos = Self.Pos + dir * Self.Radius, Vel = dir * 3,
                    Radius = 3, Power = dmg, Life = B ? SkillTuning.BeeLife * 13 / 10 : SkillTuning.BeeLife, Aux = i, Aux2 = 10
                });
                if (slot >= 0) spawned++;
            }
            if (spawned == 0) return false;
            if (FinA) queenUntil = Sim.ActiveTick + SkillTuning.BeeLife;
            Sim.EmitFx(SkillFxEvent.BeesReleased, Self.Index, -1, spawned, Self.Pos);
            return true;
        }
    }

    /// <summary>BGLGXLQ: UNITY by hand. Lit orbs stay bright for 15 seconds after their last touch; the tap bursts what is lit (all five: bigger).</summary>
    public sealed class ChorusUnityHand : ActiveSkill
    {
        readonly ChorusRule chorus;
        internal ChorusUnityHand(MatchSim s, BallState b, SkillState k) : base(s, b, k) { chorus = (ChorusRule)b.Weapon; chorus.ManualUnity = true; }

        public override int CooldownTicks => SkillTuning.UnityCooldown;
        public override bool CanTap => Skill.IsReady && chorus.Lit > 0;
        public override Fix Gauge() => Skill.IsReady ? Fix.Ratio(chorus.Lit, ChorusRule.Colors) : Skill.CooldownFraction;

        protected override bool Release()
        {
            if (chorus.Lit <= 0) return false;
            var full = chorus.Lit >= ChorusRule.Colors;
            var scale = Fix.Ratio(chorus.Lit, ChorusRule.Colors) * (full ? SkillTuning.UnityFullBonus : Fix.One);
            Sim.EmitFx(SkillFxEvent.UnityRelease, Self.Index, -1, scale, Self.Pos);
            chorus.ReleaseUnity(scale);
            return true;
        }
    }

    /// <summary>HUNTER: lunge at the foe; on contact the serpent coils it, holds it and bites three times.</summary>
    public sealed class HunterCoil : ActiveSkill
    {
        int lungeUntil = -1;
        int shedUntil = -1;
        internal HunterCoil(MatchSim s, BallState b, SkillState k) : base(s, b, k) { }
        public override bool Running => Sim.ActiveTick < lungeUntil;

        protected override bool Release()
        {
            if (Foe == null) return false;
            lungeUntil = Sim.ActiveTick + SkillTuning.CoilLunge;
            Sim.EmitFx(SkillFxEvent.CoilLunge, Self.Index, Foe.Index, Fix.Zero, Self.Pos);
            return true;
        }

        internal override void Tick()
        {
            if (Sim.ActiveTick >= lungeUntil) return;
            var foe = Foe;
            if (foe == null) { lungeUntil = -1; return; }
            var to = foe.Pos - Self.Pos;
            if (to.Length <= Self.Radius + foe.Radius + 8)
            {
                lungeUntil = -1;
                var bites = B ? SkillTuning.CoilBites + (L3 ? 2 : 1) : SkillTuning.CoilBites;
                var slot = Sim.SpawnFx(new FxItem
                {
                    Kind = FxKind.Coil, Owner = Self.Index, Target = foe.Index, Pos = foe.Pos, Radius = foe.Radius + 4,
                    Power = SkillTuning.CoilBite * Mult, Extra = B ? (L3 ? Fix.Ratio(9, 20) : Fix.Ratio(3, 10)) : Fix.Zero, Life = A ? (L3 ? SkillTuning.CoilLife * 9 / 5 : SkillTuning.CoilLife * 3 / 2) : SkillTuning.CoilLife,
                    Aux = bites, Aux2 = A ? 90 : 0
                });
                if (slot >= 0) Sim.EmitFx(SkillFxEvent.CoilCatch, Self.Index, foe.Index, Fix.Zero, foe.Pos);
                return;
            }
            Self.Vel = to.NormalizedOr(FixVec2.Right) * Sim.SkillSpeedCap(Self);
        }

        /// <summary>The coil is over (called by the Fx rules): SHED SKIN gives one second of cover.</summary>
        internal void CoilEnded()
        {
            if (FinB) shedUntil = Sim.ActiveTick + MatchConfig.TicksPerSecond;
        }

        internal override bool Guard(BallState attacker, Fix damage, DamageKind kind)
        {
            if (!FinB || Sim.ActiveTick >= shedUntil) return false;
            shedUntil = -1;
            Proc(damage);
            return true;
        }

        internal override ulong HashInto(ulong h) => shedUntil >= 0 ? SimHash.Mix(SimHash.Mix(h, lungeUntil), shedUntil) : SimHash.Mix(h, lungeUntil);
    }
}
