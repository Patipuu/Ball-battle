using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;

namespace BallBattle.View
{
    /// <summary>
    /// Damage totals of one fight, summed from the values the sim reports in its events (the HP each one removed).
    /// Nothing is recomputed: Hit / ProjectileHit / Damage (reflect, traits) / StatusTick (poison) / ObstacleHit and
    /// WallBounce (arena) carry exactly the damage applied. A killing blow counts in full (the sim then sets the dead
    /// ball's HP to 0), so a knocked-out ball has taken at least its HP.
    /// </summary>
    public sealed class FightStats
    {
        readonly float[] dealt = new float[2];
        readonly float[] taken = new float[2];

        public void Reset()
        {
            dealt[0] = dealt[1] = 0f;
            taken[0] = taken[1] = 0f;
        }

        /// <summary>Damage ball <paramref name="i"/> (0 = player) dealt to others.</summary>
        public float Dealt(int i) => i >= 0 && i < 2 ? dealt[i] : 0f;

        /// <summary>Damage ball <paramref name="i"/> took from every source (enemy, poison, reflect, arena).</summary>
        public float Taken(int i) => i >= 0 && i < 2 ? taken[i] : 0f;

        public void Add(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.Hit:
                case SimEventType.ProjectileHit:
                case SimEventType.Damage:
                    Count(e.A, e.B, e.Value);   // A = source (-1 none), B = target
                    break;
                case SimEventType.StatusTick:
                    Count(e.B, e.A, e.Value);   // A = poisoned ball, B = main source
                    break;
                case SimEventType.ObstacleHit:
                case SimEventType.WallBounce:
                    Count(-1, e.A, e.Value);    // arena hazard: nobody dealt it
                    break;
            }
        }

        public void AddAll(IReadOnlyList<SimEvent> events)
        {
            for (var i = 0; i < events.Count; i++) Add(events[i]);
        }

        void Count(int source, int target, float value)
        {
            if (value <= 0f) return;
            if (source >= 0 && source < 2 && source != target) dealt[source] += value;
            if (target >= 0 && target < 2) taken[target] += value;
        }
    }

    public enum FightOutcome { Win, Lost, Draw }

    /// <summary>
    /// Everything the post-fight result screen shows, read from the finished match, the fight's event totals and the
    /// run before / after RunState.ApplyResult. Built once by RunFlow; the screen only formats it.
    /// </summary>
    public sealed class FightReport
    {
        public int FightNumber;          // 1-based fight that was played
        public bool WasBoss;
        public string EnemyWeaponId;
        public FightOutcome Outcome;
        public MatchEndReason EndReason;
        public float Seconds;

        public float HpLeft;             // player HP when the fight ended (never below 0)
        public float MaxHpInFight;
        public float DamageDealt;
        public float DamageTaken;

        public int CoinsGained;
        public int CoinsTotal;
        public float HpNext;             // run HP carried into the next fight (after win heal / loss refill)
        public float MaxHpNext;
        public int LivesBefore;
        public int LivesAfter;
        /// <summary>The same fight is played again (v4 single-retry rule); false under the current lives rule.</summary>
        public bool IsRetry;

        public bool RunOver;
        public bool RunWon;

        // Next fight (when the run goes on).
        public int NextFightNumber;
        public bool NextIsBoss;
        public string NextWeaponId;
        public string NextTraits;
        public float NextEnemyHp;
        public string NextArenaId;

        /// <summary>
        /// Build the report. <paramref name="before"/> is a snapshot taken just before ApplyResult; <paramref name="run"/> is the
        /// run after it; <paramref name="match"/> the finished fight; <paramref name="stats"/> its event totals.
        /// </summary>
        public static FightReport Build(MatchSim match, FightStats stats, Snapshot before, RunState run)
        {
            var you = match.Balls[0];
            var r = new FightReport
            {
                FightNumber = before.FightIndex + 1,
                WasBoss = RunTuning.IsBoss(before.FightIndex),
                EnemyWeaponId = match.Balls[1].Weapon.Id,
                Outcome = match.WinnerIndex == 0 ? FightOutcome.Win : match.WinnerIndex < 0 ? FightOutcome.Draw : FightOutcome.Lost,
                EndReason = match.EndReason,
                Seconds = match.ActiveTick / (float)MatchConfig.TicksPerSecond,
                HpLeft = you.Hp > 0f ? you.Hp : 0f,
                MaxHpInFight = you.MaxHp,
                DamageDealt = stats != null ? stats.Dealt(0) : 0f,
                DamageTaken = stats != null ? stats.Taken(0) : 0f,
                CoinsGained = run.Coins - before.Coins,
                CoinsTotal = run.Coins,
                HpNext = run.Build.Hp,
                MaxHpNext = run.Build.MaxHp,
                LivesBefore = before.Lives,
                LivesAfter = run.Lives,
                RunOver = run.Over,
                RunWon = run.Won,
            };
            r.IsRetry = r.Outcome != FightOutcome.Win && !run.Over && run.FightIndex == before.FightIndex;

            if (!run.Over)
            {
                var next = run.CreateMatch();   // the exact next fight: spawn hooks (Heavy, Glass Cannon, boss HP) applied
                var enemy = run.Enemy;
                r.NextFightNumber = run.FightIndex + 1;
                r.NextIsBoss = run.IsBossFight;
                r.NextWeaponId = enemy.WeaponId;
                r.NextTraits = CardText.Traits(enemy);
                r.NextEnemyHp = next.Balls[1].MaxHp;
                r.NextArenaId = run.ArenaId;
            }
            return r;
        }

        /// <summary>The run numbers that ApplyResult changes, taken just before it.</summary>
        public struct Snapshot
        {
            public int FightIndex;
            public int Coins;
            public int Lives;

            public static Snapshot Of(RunState run) => new Snapshot { FightIndex = run.FightIndex, Coins = run.Coins, Lives = run.Lives };
        }

        // ---- Wording (docs/design/boss-and-loss-rules.md: WIN / LOST / DRAW, a draw counts as lost, RETRY) ----

        public string Title => Outcome == FightOutcome.Win ? "WIN" : Outcome == FightOutcome.Draw ? "DRAW" : "LOST";

        public string HowLine
        {
            get
            {
                var how = EndReason == MatchEndReason.TimeCap ? "BY TIME" : "BY KO";
                return $"{how}  {(int)System.Math.Round(Seconds)}S";
            }
        }

        /// <summary>What the result means for the run, in the design doc's words.</summary>
        public string ConsequenceLine
        {
            get
            {
                if (Outcome == FightOutcome.Win) return $"WIN  +{CoinsGained} COINS";
                var lost = Outcome == FightOutcome.Draw ? "DRAW COUNTS AS LOST" : "LOST";
                if (IsRetry) return lost + $" - RETRY AT {Pct(HpNext, MaxHpNext)}% HP";
                return lost + "  -1 LIFE";
            }
        }

        /// <summary>Second consequence line: HP going into the next fight and, on a loss, lives left.</summary>
        public string CarryLine
        {
            get
            {
                if (RunOver) return "";
                var hp = $"HP NEXT FIGHT {Ceil(HpNext)}/{Ceil(MaxHpNext)}";
                if (Outcome == FightOutcome.Win || IsRetry) return hp;
                return $"{hp}  LIVES {LivesAfter}";
            }
        }

        public string RunLine => !RunOver ? "" : RunWon ? "RUN WON!" : "RUN OVER";

        public string NextHeader => $"NEXT: FIGHT {NextFightNumber}/{RunTuning.Fights}" + (NextIsBoss ? "  GIANT BOSS" : "") + (IsRetry ? "  RETRY" : "");

        public string NextStats => $"HP {Ceil(NextEnemyHp)}   ARENA {ArenaRegistry.Get(NextArenaId).DisplayName}";

        public string NextWeaponName => NextWeaponId != null ? WeaponRegistry.Get(NextWeaponId).DisplayName : "";

        public static string Number(float v)
        {
            var r = (float)System.Math.Round(v, 1, System.MidpointRounding.AwayFromZero);
            return r.ToString(r % 1f == 0f ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        static int Ceil(float v) => (int)System.Math.Ceiling(v);
        static int Pct(float a, float b) => b > 0f ? (int)System.Math.Round(a / b * 100f) : 0;
    }
}
