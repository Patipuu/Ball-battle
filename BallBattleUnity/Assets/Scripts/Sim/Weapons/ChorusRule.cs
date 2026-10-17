namespace BallBattle.Sim.Weapons
{
    /// <summary>
    /// BB-14 BGLGXLQ, reworked in BB-18 (Phu, 16/10): five colour orbs circle the ball - PINK, GREEN, BLUE, YELLOW, RED.
    /// An unlit orb that touches an enemy lights its colour (any order; a lit orb touching again does nothing), nicks the
    /// enemy and turns on that colour's gift: PINK heals on each melee hit, GREEN raises top speed, BLUE gives one shield
    /// charge, YELLOW adds melee damage, RED burns the enemy it touched. When all five are lit, UNITY fires: every enemy within
    /// the radius takes base + per-ring damage, then the ring goes dark and starts again; each completed ring adds melee
    /// damage for good. The short halo blade still deals the melee hits.
    /// </summary>
    public sealed class ChorusRule : WeaponRule
    {
        public const string WeaponId = "chorus";
        public const int Colors = 5;
        public const int Pink = 1, Green = 2, Blue = 3, Yellow = 4, Red = 5;
        const int AllLit = (1 << Colors) - 1;

        int litMask;   // bit (colour - 1) set = lit
        /// <summary>How many colours are lit now, 0..4 (5 only for the instant of UNITY).</summary>
        public int Lit { get; private set; }
        /// <summary>Completed rings (UNITY bursts) so far: the growth stat.</summary>
        public int Rings { get; private set; }
        /// <summary>Angle of the PINK orb in degrees; the others follow 72 degrees apart.</summary>
        public Fix OrbAngleDeg { get; private set; }
        int nextLightTick;              // an orb may light only from this active tick on (one colour per touch, dark pause after UNITY)
        int pendingSecondUnity = -1;   // FINALE B: tick of the second, weaker burst (-1 none)
        Fix pendingSecondDamage;

        /// <summary>BB-19b: the Run player releases UNITY by hand (no burst at five lit), and a lit orb fades after SkillTuning.UnityFadeTicks.</summary>
        public bool ManualUnity { get; internal set; }
        readonly int[] litAt = new int[Colors];

        public ChorusRule()
        {
            BladeLength = TributeTuning.ChorusLength;
            SpinDegPerTick = TributeTuning.ChorusSpin;
        }

        public override string Id => WeaponId;
        public override string StatLabel => "RINGS";
        public override Fix StatValue => Rings;
        public override string StatText => Rings + " +" + Lit + "/5";

        char Fin => Self?.Tree != null ? Self.Tree.Finale : '-';
        int Growth => Self?.Tree != null ? Self.Tree.Growth : 0;
        /// <summary>INSTINCT DEVOTION: colour effects +6% per rank.</summary>
        Fix Devotion => Self?.Tree != null ? Fix.Ratio(100 + 6 * Growth, 100) : Fix.One;

        public bool IsLit(int color) => (litMask & (1 << (color - 1))) != 0;

        /// <summary>Distance from the ball centre to an orb centre.</summary>
        public Fix OrbDistance => (Self != null ? Self.Radius : (Fix)12) + TributeTuning.ChorusOrbGap;

        /// <summary>Where the orb of <paramref name="color"/> (1..5) is now.</summary>
        public FixVec2 Orb(int color) => Self.Pos + FixVec2.FromAngleDeg(OrbAngleDeg + 72 * (color - 1)) * OrbDistance;

        public override Fix Damage(in HitContext ctx)
        {
            var d = TributeTuning.ChorusStartDamage + TributeTuning.ChorusDamagePerRing * Rings;
            if (IsLit(Yellow)) d += TributeTuning.ChorusYellowDamage * Devotion;
            if (Self?.Tree != null) d += TributeTuning.ChorusRunDamage;   // Run baseline
            if (Sim != null && Sim.IsPlainVersus) d *= TributeTuning.ChorusVersusMeleeScale;
            return d;
        }

        /// <summary>OPENER MORNING LIGHT: the fight starts with PINK lit.</summary>
        internal void ApplyTreeStart() => Light(Pink, null, Self.Pos);

        public override void OnHitDealt(BallState target, Fix damage, DamageKind kind)
        {
            if (kind != DamageKind.Weapon) return;
            if (IsLit(Pink)) Sim.Heal(Self, TributeTuning.ChorusPinkHeal * Devotion);
        }

        /// <summary>Skill HYMN: lights the first unlit colours at once (it may fire UNITY).</summary>
        internal void Hymn()
        {
            var left = TributeTuning.ChorusHymnColors;
            for (var c = Pink; c <= Red && left > 0; c++)
            {
                if (IsLit(c)) continue;
                left--;
                if (Light(c, null, Orb(c))) break;   // UNITY fired: the new ring starts dark
            }
        }

        /// <summary>Lights <paramref name="color"/> and applies its gift. True when this completed the ring (UNITY fired).</summary>
        bool Light(int color, BallState target, FixVec2 at)
        {
            litMask |= 1 << (color - 1);
            Lit++;
            litAt[color - 1] = Sim.ActiveTick;
            Sim.EmitTribute(SimEventType.ChorusLit, Self, target != null ? target.Index : -1, color, at, (byte)Lit);
            switch (color)
            {
                case Green: MaxSpeedBonus = TributeTuning.ChorusGreenSpeed * Devotion; break;
                case Blue: Sim.AddShield(Self, TributeTuning.ChorusBlueShield); break;
                case Red:
                    if (target != null) Sim.ApplyPoison(target, Self, TributeTuning.ChorusRedDps * Devotion *
                        (Sim.IsPlainVersus ? TributeTuning.ChorusVersusAbilityScale : Fix.One), TributeTuning.ChorusRedTicks);
                    break;
            }
            Sim.EmitStat(Self);
            if (litMask != AllLit || ManualUnity) return false;
            Unity();
            return true;
        }

        Fix UnityDamage()
        {
            var d = TributeTuning.ChorusUnityBase + TributeTuning.ChorusUnityPerRing * Rings;
            if (Sim.IsPlainVersus) d *= TributeTuning.ChorusVersusAbilityScale;
            var s = Self.Skill;
            if (s != null && s.Level > 1 && s.Branch == Skills.SkillBranch.B) d *= s.Level == 2 ? TributeTuning.ChorusRadianceL2 : TributeTuning.ChorusRadianceL3;   // RADIANCE
            return d;
        }

        Fix UnityRadius()
        {
            var s = Self.Skill;
            return TributeTuning.ChorusUnityRadius + (s != null && s.Level > 1 && s.Branch == Skills.SkillBranch.B ? TributeTuning.ChorusRadianceRadius : Fix.Zero);
        }

        void Unity() => Unity(Fix.One);

        /// <summary>BB-19b: the tap. Bursts what is lit; <paramref name="scale"/> is the share of full UNITY damage.</summary>
        internal void ReleaseUnity(Fix scale)
        {
            if (Lit > 0) Unity(scale);
        }

        void Fade()
        {
            for (var c = Pink; c <= Red; c++)
            {
                if (!IsLit(c) || Sim.ActiveTick - litAt[c - 1] < Skills.SkillTuning.UnityFadeTicks) continue;
                litMask &= ~(1 << (c - 1));
                Lit--;
                if (c == Green) MaxSpeedBonus = Fix.Zero;
                Sim.EmitStat(Self);
            }
        }

        void Unity(Fix scale)
        {
            var damage = UnityDamage() * scale;
            if (Fin == 'B')
            {
                // DOUBLE RAINBOW: two bursts at ChorusDoubleRainbow each, 0.3 s apart.
                damage *= TributeTuning.ChorusDoubleRainbow;
                pendingSecondUnity = Sim.ActiveTick + 18;
                pendingSecondDamage = damage;
            }
            Burst(damage);
            var s = Self.Skill;
            if (s != null && s.Level > 1 && s.Branch == Skills.SkillBranch.A)   // CHORUS: UNITY heals; Run uses its own balance values
                Sim.Heal(Self, Self.MaxHp * (Self.Tree != null
                    ? s.Level == 2 ? TributeTuning.ChorusRunHealL2 : TributeTuning.ChorusRunHealL3
                    : s.Level == 2 ? TributeTuning.ChorusHealL2 : TributeTuning.ChorusHealL3));
            Rings++;
            litMask = 0;
            Lit = 0;
            MaxSpeedBonus = Fix.Zero;
            nextLightTick = Sim.ActiveTick + TributeTuning.ChorusDarkTicks;
            if (Fin == 'A')   // EVERLIGHT: the first colours stay lit
                for (var c = Pink; c < Pink + TributeTuning.ChorusEverlightKeep; c++) Light(c, null, Self.Pos);
            Sim.EmitStat(Self);
        }

        void Burst(Fix damage)
        {
            var r = UnityRadius();
            var hits = 0;
            foreach (var other in Sim.Balls)
            {
                if (other == Self || !other.Alive || other.Hp <= Fix.Zero) continue;
                if ((other.Pos - Self.Pos).LengthSq > FixMath.Sq(r + other.Radius)) continue;
                Sim.DealDamage(Self, other, damage, DamageKind.Ability);
                hits++;
            }
            Sim.EmitTribute(SimEventType.ChorusUnity, Self, hits, damage, Self.Pos, (byte)(Rings & 0xFF));
        }

        public override void OnTick()
        {
            OrbAngleDeg += TributeTuning.ChorusOrbSpin;
            if (OrbAngleDeg >= 360) OrbAngleDeg -= 360;
            if (pendingSecondUnity >= 0 && Sim.ActiveTick >= pendingSecondUnity)
            {
                pendingSecondUnity = -1;
                Burst(pendingSecondDamage);
            }
            if (ManualUnity) Fade();
            Touch();
        }

        /// <summary>An unlit orb touching an enemy body lights its colour (at most one colour per ChorusLightGapTicks).</summary>
        void Touch()
        {
            if (Sim.ActiveTick < nextLightTick || !Self.Alive) return;
            var reach = TributeTuning.ChorusOrbRadius;
            for (var c = Pink; c <= Red; c++)
            {
                if (IsLit(c)) continue;
                var at = Orb(c);
                foreach (var other in Sim.Balls)
                {
                    if (other == Self || !other.Alive || other.Hp <= Fix.Zero) continue;
                    if ((other.Pos - at).LengthSq > FixMath.Sq(reach + other.Radius)) continue;
                    nextLightTick = Sim.ActiveTick + TributeTuning.ChorusLightGapTicks;
                    Sim.DealDamage(Self, other, TributeTuning.ChorusOrbDamage *
                        (Sim.IsPlainVersus ? TributeTuning.ChorusVersusAbilityScale : Fix.One), DamageKind.Ability);
                    Light(c, other, at);
                    return;
                }
            }
        }

        protected override ulong HashState(ulong h)
        {
            h = SimHash.Mix(SimHash.Mix(SimHash.Mix(h, litMask), Lit), Rings);
            h = SimHash.Mix(SimHash.Mix(h, OrbAngleDeg), nextLightTick);
            h = SimHash.Mix(SimHash.Mix(h, pendingSecondUnity), pendingSecondDamage);
            if (ManualUnity) for (var i = 0; i < Colors; i++) h = SimHash.Mix(h, litAt[i]);
            return h;
        }
    }
}
