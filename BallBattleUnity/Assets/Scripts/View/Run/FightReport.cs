using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;

namespace BallBattle.View
{
    /// <summary>
    /// Damage totals of one fight, from the values the sim reports in its events: Hit / ProjectileHit / Damage (reflect,
    /// traits) / StatusTick (poison) / ObstacleHit and WallBounce (arena). Each hit counts at most the HP its target
    /// still had (the overkill of a killing blow is dropped), so the totals match the HP bars: taken = start HP + heals
    /// - end HP. HP is followed from the match's start values and the Heal events (a Second Wind save included).
    /// </summary>
    public sealed class FightStats
    {
        readonly float[] dealt = new float[2];
        readonly float[] taken = new float[2];
        readonly float[] hp = new float[2];
        readonly float[] startHp = new float[2];
        readonly float[] healed = new float[2];
        readonly bool[] secondWind = new bool[2];
        bool tracking;

        /// <summary>Start a fight: totals to zero, HP from the match as it will start (spawn hooks applied).</summary>
        public void Reset(MatchSim match)
        {
            dealt[0] = dealt[1] = 0f;
            taken[0] = taken[1] = 0f;
            tracking = match != null;
            for (var i = 0; i < 2; i++) startHp[i] = hp[i] = match != null && i < match.Balls.Count ? Mathf0(match.Balls[i].Hp.ToFloat()) : 0f;
            healed[0] = healed[1] = 0f;
            secondWind[0] = secondWind[1] = false;
        }

        /// <summary>
        /// DMG DEALT of ball <paramref name="i"/> (0 = player): ALL the HP its opponent lost this fight (hits, poison, reflect,
        /// arena), symmetric with Taken = start HP + heals - end HP of the opponent, so it matches the opponent's HP bar.
        /// </summary>
        public float Dealt(int i) => i == 0 ? Taken(1) : i == 1 ? Taken(0) : 0f;

        /// <summary>Only the part of <see cref="Dealt"/> that ball <paramref name="i"/> itself caused (arena damage excluded).</summary>
        public float DealtBySource(int i) => i >= 0 && i < 2 ? dealt[i] : 0f;

        /// <summary>HP ball <paramref name="i"/> got back this fight (sum of its Heal events: Aegis regen, Vampire, Second Wind...).</summary>
        public float Healed(int i) => i >= 0 && i < 2 ? healed[i] : 0f;
        public bool SecondWindTriggered(int i) => i >= 0 && i < 2 && secondWind[i];

        /// <summary>HP ball <paramref name="i"/> started the fight with (spawn hooks applied).</summary>
        public float StartHp(int i) => i >= 0 && i < 2 ? startHp[i] : 0f;

        /// <summary>HP ball <paramref name="i"/> lost to every source (enemy, poison, reflect, arena).</summary>
        public float Taken(int i) => i >= 0 && i < 2 ? taken[i] : 0f;

        public void Add(SimEvent e)
        {
            switch (e.Type)
            {
                case SimEventType.TraitTriggered:
                    if (e.TraitId == BallBattle.Sim.Traits.SecondWindTrait.TraitId && e.A >= 0 && e.A < 2) secondWind[e.A] = true;
                    break;
                case SimEventType.Hit:
                case SimEventType.ProjectileHit:
                case SimEventType.Damage:
                    Count(e.A, e.B, e.Value.ToFloat());   // A = source (-1 none), B = target
                    break;
                case SimEventType.StatusTick:
                    Count(e.B, e.A, e.Value.ToFloat());   // A = poisoned ball, B = main source
                    break;
                case SimEventType.ObstacleHit:
                case SimEventType.WallBounce:
                    Count(-1, e.A, e.Value.ToFloat());    // arena hazard: nobody dealt it
                    break;
                case SimEventType.Heal:
                    if (e.A >= 0 && e.A < 2 && e.Value.ToFloat() > 0f)
                    {
                        healed[e.A] += e.Value.ToFloat();
                        if (tracking) hp[e.A] += e.Value.ToFloat();
                    }
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
            var removed = value;
            if (tracking && target >= 0 && target < 2)
            {
                removed = value < hp[target] ? value : hp[target];   // nothing beyond the HP left
                hp[target] = Mathf0(hp[target] - value);
            }
            if (removed <= 0f) return;
            if (source >= 0 && source < 2 && source != target) dealt[source] += removed;
            if (target >= 0 && target < 2) taken[target] += removed;
        }

        static float Mathf0(float v) => v > 0f ? v : 0f;
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
        /// <summary>HP the enemy healed this fight (0 = no FOE HEALED line).</summary>
        public float EnemyHealed;

        public int CoinsGained;
        public int CoinsTotal;
        public float HpNext;             // run HP carried into the next fight (after win heal / loss refill)
        public float MaxHpNext;
        public int RetriesBefore;
        public int RetriesAfter;
        public bool PlayerSecondWindTriggered;
        /// <summary>The same fight is played again (v4 single-retry rule); false under the current lives rule.</summary>
        public bool IsRetry;

        public bool RunOver;
        public bool RunWon;

        // Next fight (when the run goes on).
        public int NextFightNumber;
        public bool NextIsBoss;
        /// <summary>BB-VIEW3: boss of the fight just played (null for a normal fight).</summary>
        public string BossId;
        /// <summary>BB-VIEW3: boss of the next fight (the run's real roll, or a forced boss); null for a normal fight.</summary>
        public string NextBossId;
        public string NextWeaponId;
        public string NextTraits;
        /// <summary>Next enemy's trait names (with level), one entry per trait, for wrapping between traits (BB-FONT1). Null: split NextTraits.</summary>
        public System.Collections.Generic.List<string> NextTraitNames;
        public float NextEnemyHp;
        public string NextArenaId;

        /// <summary>
        /// Build the report. <paramref name="before"/> is a snapshot taken just before ApplyResult; <paramref name="run"/> is the
        /// run after it; <paramref name="match"/> the finished fight; <paramref name="stats"/> its event totals.
        /// </summary>
        public static FightReport Build(MatchSim match, FightStats stats, Snapshot before, RunState run) => Build(match, stats, before, run, null);

        /// <summary>BB-VIEW3: <paramref name="nextArenaOverride"/> = the arena the next fight is actually played in when it is not the
        /// run's own (-bbForceArena, RunFlow.ForceArenaId); null = RunState.ArenaId.</summary>
        public static FightReport Build(MatchSim match, FightStats stats, Snapshot before, RunState run, string nextArenaOverride)
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
                HpLeft = you.Hp.ToFloat() > 0f ? you.Hp.ToFloat() : 0f,
                MaxHpInFight = you.MaxHp.ToFloat(),
                DamageDealt = stats != null ? stats.Taken(1) : 0f,   // everything the enemy lost (hits, poison, reflect, arena), like DMG TAKEN
                DamageTaken = stats != null ? stats.Taken(0) : 0f,
                EnemyHealed = stats != null ? stats.Healed(1) : 0f,
                CoinsGained = run.Coins - before.Coins,
                CoinsTotal = run.Coins,
                HpNext = run.Build.Hp.ToFloat(),
                MaxHpNext = run.Build.MaxHp.ToFloat(),
                RetriesBefore = before.RetryCount,
                RetriesAfter = run.RetryCount,
                PlayerSecondWindTriggered = stats != null && stats.SecondWindTriggered(0),
                RunOver = run.Over,
                RunWon = run.Won,
                BossId = before.BossId,
            };
            r.IsRetry = r.Outcome != FightOutcome.Win && !run.Over && run.FightIndex == before.FightIndex;

            if (!run.Over)
            {
                var next = run.CreateMatch();   // the exact next fight: spawn hooks (Heavy, Glass Cannon, boss HP) applied
                var enemy = run.Enemy;
                r.HpNext = next.Balls[0].Hp.ToFloat();      // real start HP and max HP (Heavy: 170/170, not /100)
                r.MaxHpNext = next.Balls[0].MaxHp.ToFloat();
                r.NextFightNumber = run.FightIndex + 1;
                r.NextIsBoss = run.IsBossFight;
                r.NextBossId = run.CurrentBossId;
                r.NextWeaponId = enemy.WeaponId;
                r.NextTraits = CardText.Traits(enemy);
                r.NextTraitNames = CardText.TraitNames(enemy);
                r.NextEnemyHp = next.Balls[1].MaxHp.ToFloat();
                r.NextArenaId = nextArenaOverride ?? run.ArenaId;
            }
            return r;
        }

        /// <summary>The run numbers that ApplyResult changes, taken just before it.</summary>
        public struct Snapshot
        {
            public int FightIndex;
            public int Coins;
            public int RetryCount;
            /// <summary>BB-VIEW3: boss of the fight about to be applied (null for a normal fight).</summary>
            public string BossId;

            public static Snapshot Of(RunState run) =>
                new Snapshot { FightIndex = run.FightIndex, Coins = run.Coins, RetryCount = run.RetryCount, BossId = run.CurrentBossId };
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
                return lost + "  RUN OVER";
            }
        }

        /// <summary>Second consequence line: HP going into the next fight and, on a loss, lives left.</summary>
        public string CarryLine
        {
            get
            {
                if (RunOver) return "";
                var hp = "HP NEXT FIGHT " + HpText.Pair(HpNext, MaxHpNext);
                if (Outcome == FightOutcome.Win || IsRetry) return hp;
                return $"{hp}  RETRY {(RetriesAfter == 0 ? "1" : "USED")}";
            }
        }

        public string RunLine => !RunOver ? "" : RunWon ? "RUN WON!" : "RUN OVER";

        /// <summary>"NEXT: FIGHT 4/8  SHELL KING  RETRY" (BB-VIEW3: the real boss name; "GIANT BOSS" only when the id is unknown).</summary>
        public string NextHeader => $"NEXT: FIGHT {NextFightNumber}/{RunTuning.Fights}" +
            (NextIsBoss ? "  " + (string.IsNullOrEmpty(NextBossId) ? "GIANT BOSS" : BossText.Name(NextBossId)) : "") + (IsRetry ? "  RETRY" : "");

        public string NextStats => $"HP {HpText.Of(NextEnemyHp)}   ARENA {ArenaRegistry.Get(NextArenaId).DisplayName}";

        public string NextWeaponName => NextWeaponId != null ? WeaponNames.Of(NextWeaponId) : "";

        public static string Number(float v)
        {
            var r = (float)System.Math.Round(v, 1, System.MidpointRounding.AwayFromZero);
            return r.ToString(r % 1f == 0f ? "0" : "0.0", System.Globalization.CultureInfo.InvariantCulture);
        }

        static int Pct(float a, float b) => b > 0f ? (int)System.Math.Round(a / b * 100f) : 0;
    }
}
