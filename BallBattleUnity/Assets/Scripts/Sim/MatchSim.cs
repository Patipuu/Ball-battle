using System;
using System.Collections.Generic;

namespace BallBattle.Sim
{
    /// <summary>
    /// Deterministic match simulation. One Step() = one tick (1/60 s).
    /// Order per active tick: arena update → N substeps of (integrate → weapon contacts → ball-ball → walls)
    /// → speed limits → cooldowns → time cap. During hitstop the whole world is frozen.
    /// Same config + seed + weapon set → identical state hash on the same build.
    /// Collision handling lives in MatchSim.Collisions.cs.
    /// </summary>
    public sealed partial class MatchSim
    {
        public readonly MatchConfig Config;
        public readonly uint Seed;

        readonly SimRandom rng;
        readonly BallState[] balls;
        readonly int[,] parryCooldown;
        /// <summary>WeaponTuning values at match start (tuning is static; this pins it into the hash).</summary>
        readonly ulong tuningFingerprint;

        public IReadOnlyList<BallState> Balls => balls;
        /// <summary>Events produced by the last Step() only.</summary>
        public readonly List<SimEvent> Events = new List<SimEvent>(32);

        /// <summary>All ticks stepped, including hitstop.</summary>
        public int Tick { get; private set; }
        /// <summary>Ticks where the world actually moved. Arena shrink and time cap use this.</summary>
        public int ActiveTick { get; private set; }
        public int HitstopRemaining { get; private set; }
        public ArenaRect Arena { get; private set; }

        public MatchOutcome Outcome { get; private set; }
        public MatchEndReason EndReason { get; private set; }
        public int WinnerIndex { get; private set; } = -1;

        public MatchSim(MatchConfig config, uint seed, IReadOnlyList<WeaponRule> weapons)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (weapons == null || weapons.Count < 2) throw new ArgumentException("Need at least 2 weapons", nameof(weapons));

            Config = config;
            Seed = seed;
            rng = new SimRandom(seed);
            balls = new BallState[weapons.Count];
            parryCooldown = new int[weapons.Count, weapons.Count];
            Arena = config.ArenaAt(0);
            tuningFingerprint = Weapons.WeaponTuning.Fingerprint();

            for (var i = 0; i < weapons.Count; i++)
            {
                if (weapons[i] == null) throw new ArgumentException($"Weapon {i} is null", nameof(weapons));
                weapons[i].Bind(config);
                balls[i] = Spawn(i, weapons.Count, weapons[i]);
            }
        }

        BallState Spawn(int index, int count, WeaponRule weapon)
        {
            var b = new BallState(index, weapon, count)
            {
                Radius = Config.BallRadius,
                MaxHp = Config.StartHp,
                Hp = Config.StartHp
            };

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
            if (Outcome != MatchOutcome.Ongoing) return;

            Tick++;
            if (HitstopRemaining > 0)
            {
                HitstopRemaining--;
                return;
            }

            ActiveTick++;
            Arena = Config.ArenaAt(ActiveTick);

            var substeps = SubstepCount();
            var dt = 1f / substeps;
            for (var s = 0; s < substeps && Outcome == MatchOutcome.Ongoing; s++)
            {
                Integrate(dt);
                ResolveWeaponContacts();
                ResolveBallCollisions();
                ResolveWalls();
            }

            ApplySpeedLimits();
            TickCooldowns();
            CheckTimeCap();
        }

        /// <summary>
        /// Enough substeps that per substep: no blade turns more than MaxSpinPerSubstepDeg, no ball moves
        /// more than half a radius, and two blade tips cannot close faster than the parry reach
        /// (otherwise fast/long blades could pass through each other without a parry).
        /// </summary>
        int SubstepCount()
        {
            var maxSpin = 0f;
            var maxSpeed = 0f;
            var maxTipSpeed = 0f;
            var minParryReach = float.MaxValue;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var speed = b.Vel.Length;
                if (speed > maxSpeed) maxSpeed = speed;
                var w = b.Weapon;
                if (!w.HasBlade) continue;
                if (w.SpinDegPerTick > maxSpin) maxSpin = w.SpinDegPerTick;
                var tip = w.SpinDegPerTick * (MathF.PI / 180f) * (w.BladeInner + w.BladeLength);
                if (tip > maxTipSpeed) maxTipSpeed = tip;
                if (w.BladeThickness < minParryReach) minParryReach = w.BladeThickness;
            }

            var bySpin = (int)MathF.Ceiling(maxSpin / Config.MaxSpinPerSubstepDeg);
            var bySpeed = (int)MathF.Ceiling(maxSpeed / (Config.BallRadius * 0.5f));
            var bySweep = maxTipSpeed > 0f ? (int)MathF.Ceiling((2f * maxTipSpeed + 2f * maxSpeed) / minParryReach) : 0;
            bySpin = Math.Max(bySpin, bySweep);
            var n = Math.Max(Config.MinSubsteps, Math.Max(bySpin, bySpeed));
            return Math.Min(n, Config.MaxSubsteps);
        }

        void Integrate(float dt)
        {
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                b.Vel.Y -= Config.Gravity * dt;
                b.Pos += b.Vel * dt;
                if (b.Weapon.HasBlade)
                {
                    var a = b.WeaponAngleDeg + b.SpinDir * b.Weapon.SpinDegPerTick * dt;
                    a %= 360f;
                    if (a < 0f) a += 360f;
                    b.WeaponAngleDeg = a;
                }
            }
        }

        void ApplySpeedLimits()
        {
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                // Horizontal floor first, then the cap, so the cap always holds.
                if (MathF.Abs(b.Vel.X) < Config.MinHorizontalSpeed)
                {
                    float sign = b.Vel.X > 0f ? 1f : (b.Vel.X < 0f ? -1f : (b.Index % 2 == 0 ? 1f : -1f));
                    b.Vel.X = sign * Config.MinHorizontalSpeed;
                }

                var max = Config.MaxSpeed + b.Weapon.MaxSpeedBonus;
                var speedSq = b.Vel.LengthSq;
                if (speedSq > max * max) b.Vel = b.Vel * (max / MathF.Sqrt(speedSq));
            }
        }

        void TickCooldowns()
        {
            var n = balls.Length;
            for (var i = 0; i < n; i++)
            {
                var cd = balls[i].HitCooldown;
                for (var j = 0; j < n; j++)
                {
                    if (cd[j] > 0) cd[j]--;
                    if (parryCooldown[i, j] > 0) parryCooldown[i, j]--;
                }
            }
        }

        void CheckTimeCap()
        {
            if (Outcome != MatchOutcome.Ongoing || ActiveTick < Config.CapTicks) return;

            var best = -1;
            var bestFrac = -1f;
            var tie = false;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var f = b.HpFraction;
                if (f > bestFrac) { bestFrac = f; best = b.Index; tie = false; }
                else if (f == bestFrac) tie = true;
            }

            End(tie ? -1 : best, MatchEndReason.TimeCap);
        }

        void EndIfDecided()
        {
            var alive = 0;
            var last = -1;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                alive++;
                last = b.Index;
            }

            if (alive == 1) End(last, MatchEndReason.Knockout);
            else if (alive == 0) End(-1, MatchEndReason.Knockout);
        }

        void End(int winner, MatchEndReason reason)
        {
            Outcome = winner >= 0 ? MatchOutcome.Win : MatchOutcome.Draw;
            WinnerIndex = winner;
            EndReason = reason;
            Emit(SimEventType.MatchEnd, winner, -1, 0f, Vec2.Zero);
        }

        void Emit(SimEventType type, int a, int b, float value, Vec2 point)
        {
            Events.Add(new SimEvent { Type = type, Tick = Tick, A = a, B = b, Value = value, Point = point });
        }

        /// <summary>Hash of the whole match state. Equal hashes ⇔ (practically) identical matches.</summary>
        public ulong ComputeHash()
        {
            var h = SimHash.Seed;
            h = SimHash.Mix(h, SimVersion.Rules);
            h = SimHash.Mix(SimHash.Mix(h, (uint)tuningFingerprint), (uint)(tuningFingerprint >> 32));
            h = SimHash.Mix(h, Tick);
            h = SimHash.Mix(h, ActiveTick);
            h = SimHash.Mix(h, HitstopRemaining);
            h = SimHash.Mix(h, rng.State);
            h = SimHash.Mix(h, (int)Outcome);
            h = SimHash.Mix(h, WinnerIndex);
            foreach (var b in balls) h = b.HashInto(h);
            var n = balls.Length;
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                    h = SimHash.Mix(h, parryCooldown[i, j]);
            return h;
        }
    }
}
