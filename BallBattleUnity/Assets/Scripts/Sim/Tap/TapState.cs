using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Tap
{
    /// <summary>
    /// TAP-1 "tap to strike": the Run player's only input during a fight. MatchSim.Tap(ball) queues a tap; at the start of the next active
    /// tick, if the cooldown (1.2 s) is over, the ball's weapon makes one short strike of its own kind (EMBER slash, MANTIS whirl, ANGLER
    /// cast, WRECKER ram, SPARKLER volley, SPORE spore puff, MIRROR guard, HIVE all turrets fire). The first damaging hit inside the strike's
    /// window is PERFECT: x1.5 damage and a TapPerfect event. A ball whose TapState was never touched behaves and hashes exactly as before.
    /// </summary>
    /// <summary>TAP-3 tuning knobs (mutable only for the TapLab sweeps; never changed by the game).</summary>
    public static class TapTuning
    {
        public static int CooldownTicks = 120;
        /// <summary>BB-19: a tapped skill recharges in this share of the skill's automatic cooldown (10-12 s at 100).</summary>
        public static int ManualCooldownPercent = 200;
        /// <summary>A strike that lands nothing (no PERFECT) adds this much to the cooldown: spam is punished.</summary>
        public static int MissPenaltyTicks = 60;
        public static Fix PerfectMultiplier = Fix.Ratio(5, 4);
        public static Fix BladeSpin = 2;
        public static int BladeTicks = 8;
        public static Fix FangSpin = Fix.Ratio(5, 4);
        public static int FangTicks = 30;
        public static Fix PikeLength = Fix.Ratio(1, 5);
        public static int PikeTicks = 30;
        public static Fix BrawlerSpeed = Fix.Ratio(6, 5);
        public static int BrawlerTicks = 30;
        public static Fix AegisReflect = Fix.Ratio(5, 4);
        public static int AegisTicks = 30;
        public static int VenomStacks = 1;
        /// <summary>WRECKER ram speed as a share of the speed cap; 0 = keep the current speed and only turn toward the foe.</summary>
        public static Fix BrawlerDash = Fix.Zero;
        /// <summary>WRECKER: how far the ram turns the ball toward the foe (1 = straight at it, 1/2 = halfway).</summary>
        public static Fix BrawlerTurn = Fix.Ratio(1, 4);
        /// <summary>SPORE puff poison: share of SPORE's poison DPS, and how long it lasts.</summary>
        public static Fix VenomPuffDps = Fix.Ratio(1, 4);
        public static int VenomPuffSeconds = 1;
        /// <summary>SPARKLER/HIVE: a tap needs this many thirds of a charge.</summary>
        public static int ChargeThirds = 2;
        /// <summary>SPARKLER: an aimed (tapped) volley hits this much harder than its charge alone.</summary>
        public static Fix VolleyTapDamage = Fix.Ratio(6, 5);
        /// <summary>SPORE puff reach beyond both radii (px).</summary>
        public static Fix SporeReach = 20;
        public static int Set(string kv)
        {
            var n = 0;
            foreach (var part in kv.Split(','))
            {
                var t = part.Split('='); if (t.Length != 2) continue;
                var f = typeof(TapTuning).GetField(t[0]);
                if (f == null) throw new System.ArgumentException(t[0]);
                if (f.FieldType == typeof(int)) f.SetValue(null, int.Parse(t[1]));
                else { var r = t[1].Split('/'); f.SetValue(null, r.Length == 2 ? Fix.Ratio(int.Parse(r[0]), int.Parse(r[1])) : Fix.FromInt(int.Parse(r[0]))); }
                n++;
            }
            return n;
        }
    }

    public sealed class TapState
    {
        public static int CooldownTicks => TapTuning.CooldownTicks;
        public static Fix PerfectMultiplier => TapTuning.PerfectMultiplier;
        public static Fix SporeReach => TapTuning.SporeReach;

        readonly MatchSim sim;
        readonly BallState self;
        bool pending;

        public int ReadyAt { get; private set; }
        public int ActiveUntil { get; private set; }
        public int PerfectUntil { get; private set; } = -1;
        public int Taps { get; private set; }
        public int Perfects { get; private set; }
        public int Misses { get; private set; }
        /// <summary>Tick after which the current strike is judged (-1: none pending); landed = it made a PERFECT.</summary>
        int judgeAt = -1;
        bool landed;

        internal TapState(MatchSim match, BallState ball) { sim = match; self = ball; }

        /// <summary>A tap was requested or made in this match (only then does the state enter the hash).</summary>
        internal bool Touched => pending || Taps > 0;
        bool CanStrike => self.Weapon is RigRule rig ? rig.Charged : self.Weapon is VolleyRule v ? v.Charged : true;
        /// <summary>Cooldown over and the weapon has something to do (HIVE needs a turret).</summary>
        public bool Ready => SkillMode ? self.Skill.CanTap : sim.ActiveTick >= ReadyAt && CanStrike;
        /// <summary>BB-19: the Run player's tap presses the ball's skill (the old strike is gone for balls that have one).</summary>
        bool SkillMode => self.Skill != null && self.Skill.Manual;
        /// <summary>0..1 progress of the current cooldown (a missed strike's penalty stretches it), for the ring around the ball.</summary>
        public Fix ReadyFraction => SkillMode ? self.Skill.ReadyFraction : sim.ActiveTick >= ReadyAt ? Fix.One : Fix.Ratio(sim.ActiveTick - StruckAt, ReadyAt - StruckAt);
        public int StruckAt { get; private set; }
        /// <summary>The last strike missed (no PERFECT): the View can show the longer cooldown.</summary>
        public bool LastMissed { get; private set; }
        public bool Active => sim.ActiveTick < ActiveUntil;

        public Fix SpinMultiplier => !Active ? Fix.One : self.Weapon is BladeRule ? TapTuning.BladeSpin : self.Weapon is FangRule ? TapTuning.FangSpin : Fix.One;
        public Fix LengthBonus => Active && self.Weapon is PikeRule ? TapTuning.PikeLength : Fix.Zero;
        public Fix SpeedCapMultiplier => Active && self.Weapon is BrawlerRule ? TapTuning.BrawlerSpeed : Fix.One;

        internal void Request() => pending = true;

        internal void Tick()
        {
            if (judgeAt >= 0 && sim.ActiveTick > judgeAt)
            {
                LastMissed = !landed;
                if (!landed) { Misses++; ReadyAt += TapTuning.MissPenaltyTicks; sim.EmitTap(SimEventType.TapMiss, self, -1, Fix.Zero, self.Pos); }
                judgeAt = -1;
            }
            if (!pending) return;
            pending = false;
            if (SkillMode)
            {
                if (!self.Skill.TryFire()) return;
                Taps++; StruckAt = sim.ActiveTick; LastMissed = false;
                sim.EmitTap(SimEventType.TapStrike, self, -1, Fix.Zero, self.Pos);
                return;
            }
            if (Ready) Strike();
        }

        static int ActiveTicksFor(WeaponRule w) => w is BladeRule ? TapTuning.BladeTicks : w is FangRule ? TapTuning.FangTicks : w is PikeRule ? TapTuning.PikeTicks
            : w is BrawlerRule ? TapTuning.BrawlerTicks : w is AegisRule ? TapTuning.AegisTicks : 0;
        static int PerfectWindowFor(WeaponRule w) => w is BladeRule ? 14 : w is VolleyRule || w is RigRule ? 45 : w is VenomRule ? 0 : 30;

        void Strike()
        {
            Taps++;
            StruckAt = sim.ActiveTick;
            LastMissed = false;
            ReadyAt = sim.ActiveTick + CooldownTicks;
            var w = self.Weapon;
            ActiveUntil = sim.ActiveTick + ActiveTicksFor(w);
            var window = PerfectWindowFor(w);
            PerfectUntil = w is VenomRule ? -1 : sim.ActiveTick + window;
            judgeAt = sim.ActiveTick + window;
            landed = false;
            sim.EmitTap(SimEventType.TapStrike, self, -1, Fix.Zero, self.Pos);
            switch (w)
            {
                case BrawlerRule _:
                    var foe = WeaponAim.NearestFoe(sim, self);
                    if (foe != null)
                    {
                        var speed = self.Vel.Length;
                        var dash = sim.SpeedCap(self) * TapTuning.BrawlerDash;
                        if (dash > speed) speed = dash;
                        var now = self.Vel.NormalizedOr(FixVec2.Right);
                        var at = (foe.Pos - self.Pos).NormalizedOr(now);
                        self.Vel = (now + (at - now) * TapTuning.BrawlerTurn).NormalizedOr(at) * speed;
                    }
                    break;
                case VolleyRule v: v.FireNow(); break;
                case RigRule r: r.FireAllNow(); break;
                case VenomRule _: SporePuff(); break;
                case PikeRule p: p.RefreshLength(); break;
            }
        }

        /// <summary>SPORE: one poison stack on every foe within reach; a puff that reaches someone is PERFECT and adds a second stack.</summary>
        void SporePuff()
        {
            var hit = false;
            foreach (var b in sim.Balls)
            {
                if (b == self || !b.Alive || b.Hp <= Fix.Zero) continue;
                var reach = self.Radius + b.Radius + SporeReach;
                if ((b.Pos - self.Pos).LengthSq > FixMath.Sq(reach)) continue;
                var cap = self.Weapon is VenomRule venom ? venom.MaxStacks : 1;   // the puff obeys the same stack cap as SPORE's hits
                var dps = WeaponTuning.VenomDps * TapTuning.VenomPuffDps * (Fix.One + self.Bonus.DamagePct);
                var ticks = TapTuning.VenomPuffSeconds * MatchConfig.TicksPerSecond;
                for (var k = 0; k < TapTuning.VenomStacks && b.Status.PoisonStackCount < cap; k++) sim.ApplyPoison(b, self, dps, ticks);
                if (!hit) { hit = true; landed = true; Perfects++; sim.EmitTap(SimEventType.TapPerfect, self, b.Index, dps, b.Pos); }
            }
        }

        /// <summary>Weapon/projectile hit by this ball: the first one inside the strike window is PERFECT (x1.5).</summary>
        internal Fix ApplyToHit(Fix damage, BallState target) => Perfect(damage, target);

        /// <summary>Reflect by this ball (MIRROR): x1.5 while the guard is up, and PERFECT inside the window.</summary>
        internal Fix ApplyToReflect(Fix damage, BallState target)
        {
            if (Active && self.Weapon is AegisRule) damage = damage * TapTuning.AegisReflect;
            return Perfect(damage, target);
        }

        Fix Perfect(Fix damage, BallState target)
        {
            if (PerfectUntil < 0 || sim.ActiveTick > PerfectUntil || damage <= Fix.Zero) return damage;
            PerfectUntil = -1;
            landed = true;
            Perfects++;
            var boosted = damage * PerfectMultiplier;
            sim.EmitTap(SimEventType.TapPerfect, self, target.Index, boosted - damage, target.Pos);
            return boosted;
        }

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, pending); h = SimHash.Mix(h, ReadyAt); h = SimHash.Mix(h, ActiveUntil);
            h = SimHash.Mix(h, PerfectUntil); h = SimHash.Mix(h, Taps);
            h = SimHash.Mix(h, judgeAt); h = SimHash.Mix(h, landed); h = SimHash.Mix(h, Misses);
            h = SimHash.Mix(h, StruckAt); h = SimHash.Mix(h, LastMissed);
            return SimHash.Mix(h, Perfects);
        }
    }
}
