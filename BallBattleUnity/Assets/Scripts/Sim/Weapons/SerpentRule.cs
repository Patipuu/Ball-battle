namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// BB-14 snake (tribute ball): the arena drops prey for it; it steers gently toward the nearest prey, eats it and grows a
    /// tail (segments that follow the path the head took). Tail segments touching an enemy whip it; the bite (a short fang)
    /// grows with the tail. An enemy blade touching a segment cuts the tail there; the lost segments drop back as prey.
    /// Prey exist only in matches with this ball, so other matches are unchanged.
    /// </summary>
    public sealed class SerpentRule : WeaponRule
    {
        public const string WeaponId = "serpent";

        public int Segments { get; private set; }
        /// <summary>Prey eaten so far (each adds permanent bite damage).</summary>
        public int Meals { get; private set; }
        int cutImmuneUntil;
        public int PreyCount { get; private set; }
        readonly FixVec2[] prey = new FixVec2[TributeTuning.SerpentPreyMax];
        int nextPreyAt = -1;

        // Path of the head, newest first (index 0 = this tick). Segment k (1-based) sits at history[k * spacing].
        readonly FixVec2[] history = new FixVec2[TributeTuning.SerpentMaxSegments * TributeTuning.SerpentSegmentSpacing + 1];
        int histHead;
        bool histReady;
        readonly int[] tailCooldown = new int[TributeTuning.SerpentMaxSegments];

        public SerpentRule()
        {
            BladeLength = TributeTuning.SerpentLength;
            SpinDegPerTick = TributeTuning.SerpentSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "LENGTH";
        public override Fix StatValue => Segments;
        public override string StatText => Segments.ToString();

        char Fin => Self?.Tree != null ? Self.Tree.Finale : '-';
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        bool Striking => Self.Skill != null && Self.Skill.Active;
        bool SkillA => Self.Skill != null && Self.Skill.Level > 1 && Self.Skill.Branch == Skills.SkillBranch.A;
        bool SkillB => Self.Skill != null && Self.Skill.Level > 1 && Self.Skill.Branch == Skills.SkillBranch.B;

        public FixVec2 Prey(int i) => prey[i];

        /// <summary>Position of tail segment <paramref name="k"/> (1 = right behind the head).</summary>
        public FixVec2 Segment(int k) => Trail(k * TributeTuning.SerpentSegmentSpacing);

        /// <summary>Where the head was <paramref name="ticksBack"/> ticks ago (0..MaxSegments * spacing); the View draws the body along it.</summary>
        public FixVec2 Trail(int ticksBack)
        {
            if (!histReady) return Self.Pos;
            var idx = histHead - ticksBack;
            while (idx < 0) idx += history.Length;
            return history[idx];
        }

        public override Fix Damage(in HitContext ctx) =>
            TributeTuning.SerpentBiteBase + TributeTuning.SerpentBitePerMeal * Meals + TributeTuning.SerpentBitePerSegment * Segments +
            (Self?.Tree != null ? TributeTuning.SerpentRunBite : Fix.Zero);   // Run baseline

        /// <summary>OPENER FIRST MEAL: start with 2 segments.</summary>
        internal void ApplyTreeStart() => Segments = 2;

        public override void OnHitDealt(BallState target, Fix damage, DamageKind kind)
        {
            if (kind == DamageKind.Weapon) HydraHeal();
        }

        /// <summary>FINALE A (HYDRA): a bite heals 0.2 per tail segment. COIL bites call this too (BB-19c).</summary>
        internal void HydraHeal()
        {
            if (Fin == 'A' && Segments > 0) Sim.Heal(Self, Fix.Ratio(1, 5) * Segments);
        }

        public override void OnTick()
        {
            Record();
            SpawnPrey();
            Hunt();
            Eat();
            Tail();
        }

        void Record()
        {
            if (!histReady)
            {
                for (var i = 0; i < history.Length; i++) history[i] = Self.Pos;
                histReady = true;
                histHead = 0;
                return;
            }
            histHead = (histHead + 1) % history.Length;
            history[histHead] = Self.Pos;
        }

        void SpawnPrey()
        {
            if (nextPreyAt < 0) nextPreyAt = TributeTuning.SerpentFirstPreyTicks;
            if (Sim.ActiveTick < nextPreyAt) return;
            nextPreyAt = Sim.ActiveTick + TributeTuning.SerpentPreyEveryTicks;
            if (PreyCount >= TributeTuning.SerpentPreyOnField) return;
            var a = Sim.Arena;
            var m = TributeTuning.SerpentPreyMargin;
            for (var attempt = 0; attempt < 4; attempt++)
            {
                var p = new FixVec2(Sim.RandomFix(a.Left + m, a.Right - m), Sim.RandomFix(a.Bottom + m, a.Top - m));
                var clear = true;
                foreach (var b in Sim.Balls)
                    if (b.Alive && (b.Pos - p).LengthSq < FixMath.Sq(b.Radius + m)) { clear = false; break; }
                if (!clear && attempt < 3) continue;
                AddPrey(p);
                return;
            }
        }

        void AddPrey(FixVec2 p)
        {
            if (PreyCount >= prey.Length) return;
            prey[PreyCount++] = p;
            Sim.EmitTribute(SimEventType.PreySpawned, Self, PreyCount - 1, Fix.Zero, p);
        }

        void Hunt()
        {
            FixVec2 target;
            Fix deg;
            if (Striking)
            {
                var enemy = Sim.NearestEnemy(Self);
                if (enemy == null) return;
                target = enemy.Pos;
                deg = TributeTuning.SerpentStrikeDeg;
            }
            else
            {
                if (PreyCount == 0) return;
                var best = 0;
                var bestD = (prey[0] - Self.Pos).LengthSq;
                for (var i = 1; i < PreyCount; i++)
                {
                    var d = (prey[i] - Self.Pos).LengthSq;
                    if (d < bestD) { bestD = d; best = i; }
                }
                target = prey[best];
                deg = TributeTuning.SerpentHuntDeg + Fix.Ratio(1, 5) * Growth;   // INSTINCT SCENT
            }
            Self.Vel = SteerToward(Self.Vel, target - Self.Pos, deg);
        }

        /// <summary>Turns <paramref name="v"/> toward <paramref name="dir"/> by at most <paramref name="maxDeg"/> degrees, keeping its speed.</summary>
        internal static FixVec2 SteerToward(FixVec2 v, FixVec2 dir, Fix maxDeg)
        {
            var speed = v.Length;
            if (speed <= Fix.Zero || dir.LengthSq <= 0) return v;
            var vn = v.NormalizedOr(FixVec2.Right);
            var dn = dir.NormalizedOr(FixVec2.Right);
            var turn = FixVec2.FromAngleDeg(maxDeg);   // (cos, sin)
            if (FixVec2.DotFix(vn, dn) >= turn.X) return dn * speed;
            var cross = vn.X * dn.Y - vn.Y * dn.X;
            var sin = cross >= Fix.Zero ? turn.Y : -turn.Y;
            var r = new FixVec2(vn.X * turn.X - vn.Y * sin, vn.X * sin + vn.Y * turn.X);
            return r * speed;
        }

        void Eat()
        {
            var reach = Self.Radius + TributeTuning.SerpentPreyRadius + (Striking && SkillB ? (Fix)8 : Fix.Zero);   // SWALLOW
            for (var i = 0; i < PreyCount; i++)
            {
                if ((prey[i] - Self.Pos).LengthSq > FixMath.Sq(reach)) continue;
                var at = prey[i];
                prey[i] = prey[--PreyCount];
                i--;
                if (Segments < TributeTuning.SerpentMaxSegments) Segments++;
                Meals++;
                Sim.Heal(Self, TributeTuning.SerpentEatHeal);
                Sim.EmitTribute(SimEventType.PreyEaten, Self, Segments, Fix.Zero, at);
                Sim.EmitStat(Self);
            }
        }

        void Tail()
        {
            for (var k = 0; k < TributeTuning.SerpentMaxSegments; k++) if (tailCooldown[k] > 0) tailCooldown[k]--;
            if (Segments == 0) return;
            var segR = TributeTuning.SerpentSegmentRadius;
            foreach (var other in Sim.Balls)
            {
                if (other == Self || !other.Alive || other.Hp <= Fix.Zero) continue;
                // Cutting: the enemy blade touching a segment cuts the tail there (the nearest cut point to the head wins).
                if (other.Weapon.HasBlade && Sim.ActiveTick >= cutImmuneUntil)
                {
                    var reach = segR + other.Weapon.BladeThickness / 2;
                    for (var k = 1; k <= Segments; k++)
                    {
                        var s = Segment(k);
                        var c = Geometry.ClosestPointOnSegment(other.BladeStart, other.BladeEnd, s);
                        if ((c - s).LengthSq > FixMath.Sq(reach)) continue;
                        Cut(k, other, s);
                        break;
                    }
                }
                // Whip: a segment touching the enemy's body.
                for (var k = 1; k <= Segments; k++)
                {
                    if (tailCooldown[k - 1] > 0) continue;
                    var s = Segment(k);
                    if ((other.Pos - s).LengthSq > FixMath.Sq(segR + other.Radius)) continue;
                    tailCooldown[k - 1] = TributeTuning.SerpentTailCooldownTicks;
                    var constrict = SkillA && Segments >= TributeTuning.SerpentConstrictSegments;
                    Sim.DealDamage(Self, other, TributeTuning.SerpentTailDamage * (constrict ? TributeTuning.SerpentConstrictTail : Fix.One), DamageKind.Ability);
                    if (constrict) other.Vel = other.Vel * TributeTuning.SerpentConstrictSlow;   // CONSTRICT
                    if (!other.Alive || other.Hp <= Fix.Zero) break;
                }
            }
        }

        void Cut(int k, BallState cutter, FixVec2 at)
        {
            var lost = Segments - k + 1;
            var keep = 0;   // BB-19c: SHED SKIN is now the post-coil cover in HunterCoil
            var drop = lost - keep;
            for (var i = 0; i < drop; i++) AddPrey(Segment(Segments - i));
            Segments -= drop;
            cutImmuneUntil = Sim.ActiveTick + TributeTuning.SerpentCutImmuneTicks;
            Sim.EmitTribute(SimEventType.TailCut, Self, cutter.Index, drop, at);
            Sim.EmitStat(Self);
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(SimHash.Mix(SimHash.Mix(h, Segments), PreyCount), nextPreyAt);
            h = SimHash.Mix(SimHash.Mix(h, Meals), cutImmuneUntil);
            for (var i = 0; i < PreyCount; i++) h = SimHash.Mix(SimHash.Mix(h, prey[i].X), prey[i].Y);
            h = SimHash.Mix(SimHash.Mix(h, histHead), histReady);
            for (var i = 0; i < history.Length; i++) h = SimHash.Mix(SimHash.Mix(h, history[i].X), history[i].Y);
            for (var i = 0; i < tailCooldown.Length; i++) h = SimHash.Mix(h, tailCooldown[i]);
            return h;
        }
    }
}
