using System;

namespace BallBattle.Sim
{
    /// <summary>Per-tick bookkeeping: speed limits, cooldowns, deaths, match end, events, state hash.</summary>
    public sealed partial class MatchSim
    {
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

                var max = (Config.MaxSpeed + b.Weapon.MaxSpeedBonus) * (1f + b.Bonus.SpeedPct);
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
            if (!Ongoing || ActiveTick < Config.CapTicks) return;

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

        /// <summary>
        /// Every ball at HP &lt;= 0 gets one chance from its traits (TryPreventDeath, trait order); otherwise it dies
        /// (killer = whoever damaged it last) and its projectiles vanish. Index order, then the winner check.
        /// </summary>
        void ResolveDeaths()
        {
            if (!Ongoing) return;
            var anyDied = false;
            foreach (var b in balls)
            {
                if (!b.Alive || b.Hp > 0f) continue;
                if (TrySave(b)) continue;
                b.Hp = 0f;
                b.Alive = false;
                anyDied = true;
                projectiles.RemoveOwnedBy(b.Index);
                Emit(SimEventType.Death, b.Index, b.LastAttacker, 0f, b.Pos);
            }
            if (anyDied) EndIfDecided();
        }

        bool TrySave(BallState b)
        {
            var before = b.Hp;
            foreach (var t in b.Traits)
            {
                if (!t.TryPreventDeath()) continue;
                if (b.Hp <= 0f) throw new InvalidOperationException($"Trait '{t.Id}' claimed to prevent death but left HP at {b.Hp}");
                if (b.Hp > b.MaxHp) b.Hp = b.MaxHp;
                Emit(SimEventType.Heal, b.Index, -1, b.Hp - MathF.Max(before, 0f), b.Pos);
                return true;
            }
            return false;
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

        void Emit(SimEventType type, int a, int b, float value, Vec2 point, byte variant = 0)
        {
            Events.Add(new SimEvent { Type = type, Tick = Tick, A = a, B = b, Value = value, Point = point, Variant = variant });
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
            h = SimHash.Mix(h, spawnHooksDone);
            h = layout.HashInto(h);
            foreach (var b in balls) h = b.HashInto(h);
            var n = balls.Length;
            for (var i = 0; i < n; i++)
                for (var j = 0; j < n; j++)
                    h = SimHash.Mix(h, parryCooldown[i, j]);
            return projectiles.HashInto(h);
        }
    }
}
