namespace BallBattle.Sim
{
    /// <summary>Mutable state of one ball. Public fields so tests can stage exact situations.</summary>
    public sealed class BallState
    {
        public readonly int Index;
        public readonly WeaponRule Weapon;
        /// <summary>Passive traits, hooks called in array order. Empty for a plain Versus ball.</summary>
        public readonly TraitRule[] Traits;
        public readonly StatusEffects Status = new StatusEffects();
        public StatBonus Bonus;
        /// <summary>0..1: fraction of hit knockback ignored (Heavy). 0 = normal.</summary>
        public float KnockbackResist;

        public Vec2 Pos;
        public Vec2 Vel;
        public float Hp;
        public float MaxHp;
        public float Radius;
        /// <summary>Added to the weapon's BladeInner so blades start at the same distance from the edge on bigger balls (Radius - default radius).</summary>
        public float BladeShift;
        public float WeaponAngleDeg;
        /// <summary>Second blade opposite the first, as a fraction of the weapon's BladeLength (Twin Blade). 0 = none.</summary>
        public float TwinBladeScale;
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

        public float HpFraction => MaxHp > 0f ? Hp / MaxHp : 0f;

        public Vec2 BladeStart => Pos + Vec2.FromAngleDeg(WeaponAngleDeg) * (Weapon.BladeInner + BladeShift);
        public Vec2 BladeEnd => Pos + Vec2.FromAngleDeg(WeaponAngleDeg) * (Weapon.BladeInner + BladeShift + Weapon.BladeLength);

        public bool HasTwinBlade => TwinBladeScale > 0f && Weapon.HasBlade;
        public Vec2 TwinBladeStart => Pos + Vec2.FromAngleDeg(WeaponAngleDeg + 180f) * (Weapon.BladeInner + BladeShift);
        public Vec2 TwinBladeEnd => Pos + Vec2.FromAngleDeg(WeaponAngleDeg + 180f) * (Weapon.BladeInner + BladeShift + Weapon.BladeLength * TwinBladeScale);

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
            return Weapon.HashInto(h);
        }
    }
}
