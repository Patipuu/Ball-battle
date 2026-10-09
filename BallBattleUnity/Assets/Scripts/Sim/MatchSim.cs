using System;
using System.Collections.Generic;

namespace BallBattle.Sim
{
    /// <summary>
    /// Deterministic match simulation. One Step() = one tick (1/60 s).
    /// First Step only: traits' OnSpawn. Per active tick: arena update → N substeps of (integrate balls +
    /// projectiles → weapon contacts → projectile contacts → ball-ball → obstacles → walls) → speed limits →
    /// status pulses → weapon/trait OnTick → projectile lifetimes → cooldowns → time cap.
    /// During hitstop the whole world is frozen.
    /// Same config + seed + loadouts → identical state hash on the same build.
    /// Partials: Motion (substeps, integration), Collisions (blades, bodies, ball-ball), Damage (pipeline,
    /// heal, status), Rules (limits, deaths, end, hash), Arena/MatchSim.Obstacles, Projectiles/MatchSim.Projectiles.
    /// </summary>
    public sealed partial class MatchSim
    {
        public readonly MatchConfig Config;
        public readonly uint Seed;

        readonly SimRandom rng;
        readonly BallState[] balls;
        readonly int[,] parryCooldown;
        readonly ProjectilePool projectiles = new ProjectilePool();
        /// <summary>Captured at construction, so changing Config.Layout mid-match has no effect.</summary>
        readonly ArenaLayout layout;
        /// <summary>Weapon + trait tuning at match start (tuning is static; this pins it into the hash).</summary>
        readonly ulong tuningFingerprint;
        bool spawnHooksDone;

        public IReadOnlyList<BallState> Balls => balls;
        public ProjectilePool Projectiles => projectiles;
        public ArenaLayout Layout => layout;
        /// <summary>Events produced by the last Step() only.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>(512);

        /// <summary>All ticks stepped, including hitstop.</summary>
        public int Tick { get; private set; }
        /// <summary>Ticks where the world actually moved. Arena shrink and time cap use this.</summary>
        public int ActiveTick { get; private set; }
        public int HitstopRemaining { get; private set; }
        public ArenaRect Arena { get; private set; }

        public MatchOutcome Outcome { get; private set; }
        public MatchEndReason EndReason { get; private set; }
        public int WinnerIndex { get; private set; } = -1;

        bool Ongoing => Outcome == MatchOutcome.Ongoing;

        /// <summary>Plain Versus match: each ball gets only a weapon, default HP and size.</summary>
        public MatchSim(MatchConfig config, uint seed, IReadOnlyList<WeaponRule> weapons)
            : this(config, seed, Wrap(weapons)) { }

        public MatchSim(MatchConfig config, uint seed, IReadOnlyList<BallLoadout> loadouts)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            Validate(loadouts);

            Config = config;
            Seed = seed;
            rng = new SimRandom(seed);
            layout = config.Layout ?? ArenaLayout.Empty;
            balls = new BallState[loadouts.Count];
            parryCooldown = new int[loadouts.Count, loadouts.Count];
            Arena = config.ArenaAt(0);
            tuningFingerprint = unchecked(Weapons.WeaponTuning.Fingerprint() * 1099511628211UL ^ Traits.TraitTuning.Fingerprint());

            for (var i = 0; i < loadouts.Count; i++)
            {
                loadouts[i].Weapon.Bind(config);
                balls[i] = Spawn(i, loadouts.Count, loadouts[i]);
            }
        }

        /// <summary>Checks everything before binding anything, so a rejected set of loadouts can be fixed and reused.</summary>
        static void Validate(IReadOnlyList<BallLoadout> loadouts)
        {
            if (loadouts == null || loadouts.Count < 2) throw new ArgumentException("Need at least 2 balls", nameof(loadouts));
            if (loadouts.Count > ProjectilePool.MaxBalls) throw new ArgumentException($"At most {ProjectilePool.MaxBalls} balls", nameof(loadouts));

            var seenWeapons = new HashSet<WeaponRule>();
            var seenTraits = new HashSet<TraitRule>();
            for (var i = 0; i < loadouts.Count; i++)
            {
                var l = loadouts[i];
                if (l == null || l.Weapon == null) throw new ArgumentException($"Loadout {i} has no weapon", nameof(loadouts));
                if (l.Weapon.IsBound || !seenWeapons.Add(l.Weapon))
                    throw new InvalidOperationException($"Weapon '{l.Weapon.Id}' instance is already used by a ball; create a new instance per ball and per match.");
                if (l.Hp == 0f) throw new ArgumentException($"Loadout {i} starts with 0 HP", nameof(loadouts));
                for (var t = 0; t < l.Traits.Count; t++)
                {
                    var trait = l.Traits[t];
                    if (trait == null) throw new ArgumentException($"Loadout {i} trait {t} is null", nameof(loadouts));
                    if (trait.IsBound || !seenTraits.Add(trait))
                        throw new InvalidOperationException($"Trait '{trait.Id}' instance is already used by a ball; create a new instance per ball and per match.");
                }
            }
        }

        static BallLoadout[] Wrap(IReadOnlyList<WeaponRule> weapons)
        {
            if (weapons == null) throw new ArgumentNullException(nameof(weapons));
            var result = new BallLoadout[weapons.Count];
            for (var i = 0; i < weapons.Count; i++) result[i] = new BallLoadout(weapons[i]);
            return result;
        }

        BallState Spawn(int index, int count, BallLoadout l)
        {
            var traits = l.Traits.ToArray();
            var maxHp = l.MaxHp > 0f ? l.MaxHp : Config.StartHp;
            var radius = l.Radius > 0f ? l.Radius : Config.BallRadius;
            var b = new BallState(index, l.Weapon, count, traits)
            {
                Radius = radius,
                BladeShift = radius - Config.BallRadius,
                MaxHp = maxHp,
                Hp = l.Hp >= 0f ? MathF.Min(l.Hp, maxHp) : maxHp,
                Bonus = l.Bonus
            };
            l.Weapon.Attach(this, b);
            foreach (var t in traits) t.Bind(this, b);

            // Spread evenly across the width, upper half, with a seeded jitter.
            var slot = (index + 0.5f) / count;
            var x = Arena.Left + Arena.Width * slot + rng.Range(-15f, 15f);
            var y = rng.Range(Arena.Bottom * 0.3f, Arena.Top * 0.6f);
            b.Pos = new Vec2(x, y);

            var dir = Vec2.FromAngleDeg(rng.Range(0f, 360f));
            b.Vel = dir * rng.Range(Config.StartSpeedMin, Config.StartSpeedMax);
            b.WeaponAngleDeg = rng.Range(0f, 360f);
            b.SpinDir = rng.NextBool() ? 1 : -1;
            return b;
        }

        public void Step()
        {
            Events.Clear();
            if (!Ongoing) return;

            Tick++;
            if (!spawnHooksDone)
            {
                RunSpawnHooks();
                if (!Ongoing) return;
            }

            if (HitstopRemaining > 0)
            {
                HitstopRemaining--;
                return;
            }

            ActiveTick++;
            Arena = Config.ArenaAt(ActiveTick);

            var substeps = SubstepCount();
            var dt = 1f / substeps;
            for (var s = 0; s < substeps && Ongoing; s++)
            {
                Integrate(dt);
                IntegrateProjectiles(dt);
                ResolveWeaponContacts();
                ResolveProjectileContacts();
                ResolveBallCollisions();
                ResolveObstacles();
                ResolveWalls();
            }

            ApplySpeedLimits();
            TickStatusEffects();
            RunTickHooks();
            TickProjectileLifetimes();
            TickCooldowns();
            CheckTimeCap();
        }

        /// <summary>
        /// Runs traits' OnSpawn now instead of on the first Step, so a preview/countdown already shows the final
        /// sizes and HP (Heavy). Idempotent. Its events stay in Events only until the first Step.
        /// </summary>
        public void ApplySpawnHooks()
        {
            if (spawnHooksDone || !Ongoing) return;
            RunSpawnHooks();
        }

        /// <summary>On the first Step unless ApplySpawnHooks ran first.</summary>
        void RunSpawnHooks()
        {
            spawnHooksDone = true;
            foreach (var b in balls)
                foreach (var t in b.Traits) t.OnSpawn();
            ResolveDeaths();
        }

        /// <summary>Weapon then trait OnTick, ball by ball. A ball that drops to 0 HP stops acting at once.</summary>
        void RunTickHooks()
        {
            foreach (var b in balls)
            {
                if (!Ongoing) return;
                if (!Active(b)) continue;
                b.Weapon.OnTick();
                foreach (var t in b.Traits)
                {
                    if (!Active(b)) break;
                    t.OnTick();
                }
                ResolveDeaths();
            }
        }
    }
}
