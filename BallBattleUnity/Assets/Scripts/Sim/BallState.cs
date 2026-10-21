namespace BallBattle.Sim
{
    /// <summary>Mutable state of one ball. Public fields so tests can stage exact situations.</summary>
    public sealed class BallState
    {
        public readonly int Index;
        public readonly WeaponRule Weapon;
        public Skills.SkillState Skill { get; internal set; }
        /// <summary>TAP-1: tap-to-strike input state (Run player only); null = no taps possible.</summary>
        public Tap.TapState Tap { get; internal set; }
        /// <summary>BB-12g: the Run player's tree; null for everyone else (the rules read it, the hash only mixes it when set).</summary>
        public TreeLoadout Tree { get; internal set; }
        /// <summary>The sim presses this ball's active skill when it wants to (Run enemies). See BallLoadout.AutoSkill.</summary>
        public bool AutoSkill { get; internal set; }
        /// <summary>AutoSkill timing (see BallLoadout).</summary>
        public int BotFirstTick { get; internal set; }
        public int BotRepeatTicks { get; internal set; }
        /// <summary>Spin this tick: the weapon's, times the tap whirl while it lasts (identical when there is no tap).</summary>
        internal Fix SpinNow => Tap != null ? Weapon.SpinDegPerTick * Tap.SpinMultiplier : Weapon.SpinDegPerTick;
        /// <summary>Passive traits, hooks called in array order. Empty for a plain Versus ball.</summary>
        public readonly TraitRule[] Traits;
        public readonly StatusEffects Status = new StatusEffects();
        public StatBonus Bonus;
        /// <summary>0..1: fraction of hit knockback ignored (Heavy). 0 = normal.</summary>
        public Fix KnockbackResist;
        /// <summary>BB-19b: ticks the ball is held in place (hook, coil, wind-up, parry stun).</summary>
        public int StunTicks;
        /// <summary>BB-19b: ticks the ball moves at SlowFactor of its speed (spore cloud, coil).</summary>
        public int SlowTicks;
        public Fix SlowFactor = Fix.One;

        public FixVec2 Pos;
        public FixVec2 Vel;
        public Fix Hp;
        public Fix MaxHp;
        public Fix Radius;
        /// <summary>Added to the weapon's BladeInner so blades start at the same distance from the edge on bigger balls (Radius - default radius).</summary>
        public Fix BladeShift;
        public Fix WeaponAngleDeg;
        /// <summary>Second blade opposite the first, as a fraction of the weapon's BladeLength (Twin Blade). 0 = none.</summary>
        public Fix TwinBladeScale;
        /// <summary>+1 counter-clockwise, -1 clockwise. Flips on parry.</summary>
        public int SpinDir = 1;
        public bool Alive = true;
        public int HitCount;
        /// <summary>Index of the ball that damaged this one last (-1 = none); credited with the kill.</summary>
        public int LastAttacker = -1;

        /// <summary>Ticks until this ball may hit ball [i] again.</summary>
        public readonly int[] HitCooldown;

        public BallState(int index, WeaponRule weapon, int ballCount, TraitRule[] traits = null)
        {
            Index = index;
            Weapon = weapon;
            Traits = traits ?? System.Array.Empty<TraitRule>();
            HitCooldown = new int[ballCount];
        }

        public Fix HpFraction => MaxHp > Fix.Zero ? Hp / MaxHp : Fix.Zero;

        public FixVec2 BladeStart => Pos + FixVec2.FromAngleDeg(WeaponAngleDeg) * (Weapon.BladeInner + BladeShift);
        public FixVec2 BladeEnd => Pos + FixVec2.FromAngleDeg(WeaponAngleDeg) * (Weapon.BladeInner + BladeShift + Weapon.BladeLength);

        public bool HasTwinBlade => TwinBladeScale > Fix.Zero && Weapon.HasBlade;
        public FixVec2 TwinBladeStart => Pos + FixVec2.FromAngleDeg(WeaponAngleDeg + 180) * (Weapon.BladeInner + BladeShift);
        public FixVec2 TwinBladeEnd => Pos + FixVec2.FromAngleDeg(WeaponAngleDeg + 180) * (Weapon.BladeInner + BladeShift + Weapon.BladeLength * TwinBladeScale);

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, TwinBladeScale);
            h = SimHash.Mix(h, Pos);
            h = SimHash.Mix(h, Vel);
            h = SimHash.Mix(h, Hp);
            h = SimHash.Mix(h, MaxHp);
            h = SimHash.Mix(h, Radius);
            h = SimHash.Mix(h, BladeShift);
            h = SimHash.Mix(h, WeaponAngleDeg);
            h = SimHash.Mix(h, SpinDir);
            h = SimHash.Mix(h, Alive);
            h = SimHash.Mix(h, HitCount);
            h = SimHash.Mix(h, LastAttacker);
            for (var i = 0; i < HitCooldown.Length; i++) h = SimHash.Mix(h, HitCooldown[i]);
            h = Bonus.HashInto(h);
            h = SimHash.Mix(h, KnockbackResist);
            h = Status.HashInto(h);
            for (var i = 0; i < Traits.Length; i++) h = Traits[i].HashInto(h);
            h = SimHash.Mix(h, Skill != null);
            if (Skill != null) h = Skill.HashInto(h);
            if (Tap != null && Tap.Touched) h = Tap.HashInto(h);   // untouched = same hash as before TAP-1 (goldens)
            if (StunTicks > 0 || SlowTicks > 0) h = SimHash.Mix(SimHash.Mix(SimHash.Mix(h, StunTicks), SlowTicks), SlowFactor);   // BB-19b: untouched = same hash as before
            if (Tree != null) h = Tree.HashInto(h);   // BB-12g: no tree = same hash as before
            return Weapon.HashInto(h);
        }
    }
}
