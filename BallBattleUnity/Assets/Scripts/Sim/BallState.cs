namespace BallBattle.Sim
{
    /// <summary>Mutable state of one ball. Public fields so tests can stage exact situations.</summary>
    public sealed class BallState
    {
        public readonly int Index;
        public readonly WeaponRule Weapon;

        public Vec2 Pos;
        public Vec2 Vel;
        public float Hp;
        public float MaxHp;
        public float Radius;
        public float WeaponAngleDeg;
        /// <summary>+1 counter-clockwise, -1 clockwise. Flips on parry.</summary>
        public int SpinDir = 1;
        public bool Alive = true;
        public int HitCount;

        /// <summary>Ticks until this ball may hit ball [i] again.</summary>
        public readonly int[] HitCooldown;

        public BallState(int index, WeaponRule weapon, int ballCount)
        {
            Index = index;
            Weapon = weapon;
            HitCooldown = new int[ballCount];
        }

        public float HpFraction => MaxHp > 0f ? Hp / MaxHp : 0f;

        public Vec2 BladeStart => Pos + Vec2.FromAngleDeg(WeaponAngleDeg) * Weapon.BladeInner;
        public Vec2 BladeEnd => Pos + Vec2.FromAngleDeg(WeaponAngleDeg) * (Weapon.BladeInner + Weapon.BladeLength);

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Pos);
            h = SimHash.Mix(h, Vel);
            h = SimHash.Mix(h, Hp);
            h = SimHash.Mix(h, MaxHp);
            h = SimHash.Mix(h, Radius);
            h = SimHash.Mix(h, WeaponAngleDeg);
            h = SimHash.Mix(h, SpinDir);
            h = SimHash.Mix(h, Alive);
            h = SimHash.Mix(h, HitCount);
            for (var i = 0; i < HitCooldown.Length; i++) h = SimHash.Mix(h, HitCooldown[i]);
            return Weapon.HashInto(h);
        }
    }
}
