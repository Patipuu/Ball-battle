using System.Collections.Generic;
using BallBattle.Sim.Boss;

namespace BallBattle.Sim
{
    /// <summary>
    /// BB-04 boss hooks. A match without a boss rule (every Versus match, every normal Run fight) never enters this code: no extra
    /// state, no extra hash input, identical results. With a boss: BossPreTick runs once per active tick right after the arena update
    /// (boss phases, events, pull), then the arena rect is limited by the bosses (Warden) and, on a Warden step, balls caught outside
    /// are pushed back in and obstacles whose centre is outside are disabled for the rest of the match.
    /// </summary>
    public sealed partial class MatchSim
    {
        static readonly BossRule[] NoBosses = new BossRule[0];

        BossRule[] bossRules = NoBosses;
        /// <summary>Obstacles switched off by a Warden step (null when no boss can switch them off).</summary>
        bool[] obstacleOff;
        bool pushInRequested;

        /// <summary>Boss rules in ball order (empty in Versus).</summary>
        public IReadOnlyList<BossRule> Bosses => bossRules;

        /// <summary>The first boss rule, or null.</summary>
        public BossRule Boss => bossRules.Length > 0 ? bossRules[0] : null;

        /// <summary>Ball index that owns <see cref="Boss"/>, or -1.</summary>
        public int BossBallIndex => bossOwner.Length > 0 ? bossOwner[0] : -1;

        /// <summary>False once a Warden step disabled obstacle <paramref name="index"/> (View: hide it).</summary>
        public bool ObstacleActive(int index) => obstacleOff == null || !obstacleOff[index];

        void BindBosses(IReadOnlyList<BallLoadout> loadouts)
        {
            var rules = new List<BossRule>();
            var owners = new List<int>();
            for (var i = 0; i < loadouts.Count; i++)
            {
                var boss = loadouts[i].Boss;
                if (boss == null) continue;
                boss.Bind(this, balls[i]);
                rules.Add(boss);
                owners.Add(i);
            }
            if (rules.Count == 0) return;
            bossRules = rules.ToArray();
            bossOwner = owners.ToArray();
            obstacleOff = new bool[layout.ObstacleCount];
        }

        /// <summary>Ball index of each boss rule.</summary>
        int[] bossOwner = new int[0];

        void BossPreTick()
        {
            for (var i = 0; i < bossRules.Length; i++)
            {
                if (!Ongoing) return;
                if (balls[bossOwner[i]].Alive) bossRules[i].OnActiveTick();
            }
            Arena = LimitedArena(Config.ArenaAt(ActiveTick));
            if (!pushInRequested) return;
            pushInRequested = false;
            PushInside();
            DisableObstaclesOutside();
        }

        /// <summary>The normal arena, made smaller per axis by every boss limit (Warden). Unchanged without a limit.</summary>
        ArenaRect LimitedArena(ArenaRect normal)
        {
            var w = normal.Width;
            var h = normal.Height;
            var limited = false;
            foreach (var boss in bossRules)
            {
                if (!boss.ArenaLimit(out var bw, out var bh)) continue;
                if (bw < w) { w = bw; limited = true; }
                if (bh < h) { h = bh; limited = true; }
            }
            return limited ? ArenaRect.Centered(w, h) : normal;
        }

        /// <summary>Boss rules: emit a SimEventType.Boss event (Variant = kind).</summary>
        internal void EmitBoss(BossEvent kind, int a, int b, Fix value, FixVec2 point) => Emit(SimEventType.Boss, a, b, value, point, (byte)kind);

        /// <summary>Warden: push balls inside after this tick's arena update.</summary>
        internal void RequestPushIn() => pushInRequested = true;

        /// <summary>Normal speed cap of a ball: (MaxSpeed + weapon bonus) × (1 + SpeedPct).</summary>
        internal Fix SpeedCap(BallState b) => (Config.MaxSpeed + b.Weapon.MaxSpeedBonus) * (Fix.One + b.Bonus.SpeedPct);

        /// <summary>
        /// Balls caught outside the new walls are moved just inside (position only: no bounce, no damage, no event). Overlaps that
        /// creates are resolved by the normal ball-ball resolution, then everything is clamped again, until nothing overlaps (at most
        /// 32 rounds).
        /// </summary>
        void PushInside()
        {
            for (var round = 0; round < 32; round++)   // a push into a corner converges geometrically; usually 1-3 rounds
            {
                ClampInside();
                if (!AnyOverlap()) return;
                ResolveBallCollisions();
            }
            ClampInside();
        }

        void ClampInside()
        {
            var a = Arena;
            foreach (var b in balls)
            {
                if (!b.Alive) continue;
                var r = b.Radius;
                if (b.Pos.X - r < a.Left) b.Pos.X = a.Left + r;
                else if (b.Pos.X + r > a.Right) b.Pos.X = a.Right - r;
                if (b.Pos.Y - r < a.Bottom) b.Pos.Y = a.Bottom + r;
                else if (b.Pos.Y + r > a.Top) b.Pos.Y = a.Top - r;
            }
        }

        bool AnyOverlap()
        {
            for (var i = 0; i < balls.Length; i++)
                for (var j = i + 1; j < balls.Length; j++)
                {
                    var a = balls[i];
                    var b = balls[j];
                    if (!a.Alive || !b.Alive) continue;
                    if ((b.Pos - a.Pos).LengthSq < FixMath.Sq(a.Radius + b.Radius)) return true;
                }
            return false;
        }

        void DisableObstaclesOutside()
        {
            if (obstacleOff == null) return;
            var a = Arena;
            for (var k = 0; k < obstacleOff.Length; k++)
            {
                if (obstacleOff[k]) continue;
                var o = layout.Get(k);
                var c = o.Shape == ObstacleShape.Circle ? o.A : (o.A + o.B) / 2;
                if (c.X < a.Left || c.X > a.Right || c.Y < a.Bottom || c.Y > a.Top) obstacleOff[k] = true;
            }
        }

        /// <summary>Is a Weapon/Projectile hit on <paramref name="target"/> at <paramref name="contact"/> stopped by a boss rule (Shell King)?</summary>
        bool BossBlocks(BallState attacker, BallState target, FixVec2 contact)
        {
            if (bossRules.Length == 0) return false;
            for (var i = 0; i < bossRules.Length; i++)
                if (bossOwner[i] == target.Index && bossRules[i].BlocksHit(attacker, contact)) return true;
            return false;
        }

        ulong HashBosses(ulong h)
        {
            if (bossRules.Length == 0) return h;   // Versus and normal fights: hash unchanged
            h = SimHash.Mix(h, bossRules.Length);
            for (var i = 0; i < bossRules.Length; i++) h = bossRules[i].HashInto(SimHash.Mix(h, bossOwner[i]));
            h = SimHash.Mix(h, pushInRequested);
            for (var k = 0; k < obstacleOff.Length; k++) h = SimHash.Mix(h, obstacleOff[k]);
            return h;
        }
    }
}
