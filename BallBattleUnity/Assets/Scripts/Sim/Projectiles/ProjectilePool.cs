namespace BallBattle.Sim
{
    /// <summary>One projectile slot. Inactive slots are free for reuse.</summary>
    public struct Projectile
    {
        public bool Active;
        /// <summary>Ball index that owns it (cannot hit or be deflected by its owner). Changes when deflected.</summary>
        public int Owner;
        public Vec2 Pos;
        public Vec2 Vel;
        public float Radius;
        public float Damage;
        /// <summary>Wall/obstacle bounces left; at 0 the next wall contact removes it.</summary>
        public int BouncesLeft;
        /// <summary>Ball hits left before it is removed (1 = stops at first ball).</summary>
        public int HitsLeft;
        /// <summary>Removed when this reaches 0 (counts active ticks).</summary>
        public int TicksLeft;
        /// <summary>Multiplier on MatchConfig.Gravity (0 = flies straight).</summary>
        public float GravityScale;
        /// <summary>Bit i set = already hit ball i (piercing shots hit each ball once).</summary>
        public int HitMask;

        public ulong HashInto(ulong h)
        {
            h = SimHash.Mix(h, Active);
            if (!Active) return h;
            h = SimHash.Mix(h, Owner);
            h = SimHash.Mix(h, Pos);
            h = SimHash.Mix(h, Vel);
            h = SimHash.Mix(h, Radius);
            h = SimHash.Mix(h, Damage);
            h = SimHash.Mix(h, BouncesLeft);
            h = SimHash.Mix(h, HitsLeft);
            h = SimHash.Mix(h, TicksLeft);
            h = SimHash.Mix(h, GravityScale);
            return SimHash.Mix(h, HitMask);
        }
    }

    /// <summary>What a weapon or trait asks for when firing. Pos/Vel are filled by the caller.</summary>
    public struct ProjectileSpec
    {
        public float Radius;
        public float Damage;
        public int Bounces;
        /// <summary>Balls it can hit before it is removed; &lt;= 0 = 1.</summary>
        public int Hits;
        /// <summary>&lt;= 0 = ProjectilePool.DefaultLifetimeTicks.</summary>
        public int LifetimeTicks;
        public float GravityScale;
    }

    /// <summary>
    /// Fixed-capacity projectile storage. Spawning takes the lowest free slot, so reuse is deterministic.
    /// Read-only from outside the Sim (fire through MatchSim.FireProjectile so rules and events apply).
    /// </summary>
    public sealed class ProjectilePool
    {
        public const int Capacity = 128;
        /// <summary>Ball-hit bitmask is an int, so at most 32 balls may own projectiles.</summary>
        public const int MaxBalls = 32;
        /// <summary>Lifetime used when a spec gives none.</summary>
        public const int DefaultLifetimeTicks = 10 * MatchConfig.TicksPerSecond;

        readonly Projectile[] items = new Projectile[Capacity];
        public int ActiveCount { get; private set; }

        public ref readonly Projectile Get(int index) => ref items[index];

        internal ref Projectile At(int index) => ref items[index];

        /// <summary>Returns the slot index, or -1 when the pool is full (the shot is dropped).</summary>
        internal int Spawn(int owner, Vec2 pos, Vec2 vel, in ProjectileSpec spec)
        {
            for (var i = 0; i < Capacity; i++)
            {
                if (items[i].Active) continue;
                items[i] = new Projectile
                {
                    Active = true,
                    Owner = owner,
                    Pos = pos,
                    Vel = vel,
                    Radius = spec.Radius,
                    Damage = spec.Damage,
                    BouncesLeft = spec.Bounces,
                    HitsLeft = spec.Hits > 0 ? spec.Hits : 1,
                    TicksLeft = spec.LifetimeTicks > 0 ? spec.LifetimeTicks : DefaultLifetimeTicks,
                    GravityScale = spec.GravityScale
                };
                ActiveCount++;
                return i;
            }
            return -1;
        }

        internal void Remove(int index)
        {
            if (!items[index].Active) return;
            items[index].Active = false;
            ActiveCount--;
        }

        /// <summary>Shots of a ball that died vanish with it.</summary>
        internal void RemoveOwnedBy(int owner)
        {
            if (ActiveCount == 0) return;
            for (var i = 0; i < Capacity; i++)
                if (items[i].Active && items[i].Owner == owner) Remove(i);
        }

        public ulong HashInto(ulong h)
        {
            for (var i = 0; i < Capacity; i++) h = items[i].HashInto(h);
            return h;
        }
    }
}
