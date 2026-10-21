using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Pvp
{
    /// <summary>An immutable copy of one player's permanent ball build for a ranked challenge.</summary>
    public sealed class PvpBallSnapshot
    {
        public readonly string Id;
        public readonly int Level, Vit, Pow, Growth, SkillUps, Branch;
        public readonly bool Start, Finale;

        public PvpBallSnapshot(BallProgress progress)
        {
            if (progress == null || string.IsNullOrEmpty(progress.Id)) throw new ArgumentException("Missing ball progress.", nameof(progress));
            WeaponRegistry.Get(progress.Id);
            if (progress.Level < 1 || progress.Level > MetaTuning.MaxLevel)
                throw new ArgumentException("Invalid ball level.", nameof(progress));
            if (!MetaTree.Valid(progress)) throw new ArgumentException("Invalid skill tree.", nameof(progress));
            Id = progress.Id;
            Level = progress.Level;
            Vit = progress.Vit;
            Pow = progress.Pow;
            Growth = progress.Growth;
            Start = progress.Start;
            SkillUps = progress.SkillUps;
            Branch = progress.Branch;
            Finale = progress.Finale;
        }

        public RunBuild ToBuild()
        {
            var progress = new BallProgress
            {
                Id = Id, Level = Level, Vit = Vit, Pow = Pow, Growth = Growth,
                Start = Start, SkillUps = SkillUps, Branch = Branch, Finale = Finale
            };
            var build = new RunBuild { WeaponId = Id };
            build.MaxHp = build.Hp = BallPower.BaseHp(Id);   // the same ball as in Run
            MetaTree.ApplyToRun(build, progress);
            return build;
        }

        public BallLoadout ToLoadout()
        {
            var loadout = ToBuild().ToLoadout();   // the same ball as in Run (BallPower included), without traits or cards
            loadout.TapEnabled = true;   // the same active tree skill as Run: the challenger taps it, the defender's bot presses it
            return loadout;
        }
    }

    /// <summary>PvP-only settings. Balls are the same as in Run (see BallPower); only the offline defender's bot timing lives here.</summary>
    public static class PvpTuning
    {
        /// <summary>Defender bot (AutoSkill): first cast after this many ticks, then this long after the skill is ready again.</summary>
        public const int BotFirstTick = 180, BotRepeatTicks = 60;
        /// <summary>Balls per side: round pair 1 is team slot 1 against slot 1, and so on (a draw replays the same pair).</summary>
        public const int TeamSize = 3;
    }

    /// <summary>Blind selections submitted separately by each account before either is revealed.</summary>
    public readonly struct PvpBan
    {
        public readonly string BallId, ArenaId;

        public PvpBan(string ballId, string arenaId)
        {
            if (!KnownBall(ballId) || WeaponRegistry.IsPlayerOnly(ballId)) throw new ArgumentException("Unknown banned ball.", nameof(ballId));
            ArenaRegistry.Get(arenaId);
            BallId = ballId;
            ArenaId = arenaId;
        }

        static bool KnownBall(string id)
        {
            foreach (var entry in WeaponRegistry.All)
                if (entry.Id == id) return true;
            return false;
        }
    }

    /// <summary>
    /// Resolved ranked draft (21/10: teams of three). Both bans apply to both sides; a duplicate removes only one option.
    /// The challenger picks three distinct legal balls in order; the defender's team is their roster in priority order with
    /// banned balls replaced by their next balls (the rest of their collection after the roster, as the service sends it).
    /// </summary>
    public sealed class PvpDraft
    {
        public readonly string[] BannedBalls, BannedArenas;
        public readonly string ArenaId;
        public readonly PvpBallSnapshot[] ChallengerTeam, DefenderTeam;

        PvpDraft(string[] bannedBalls, string[] bannedArenas, string arenaId, PvpBallSnapshot[] challenger, PvpBallSnapshot[] defender)
        {
            BannedBalls = bannedBalls;
            BannedArenas = bannedArenas;
            ArenaId = arenaId;
            ChallengerTeam = challenger;
            DefenderTeam = defender;
        }

        /// <summary>21/10 live PvP: the draft the server already resolved (its arena and both teams), rebuilt on the client.</summary>
        public static PvpDraft ForFight(string arenaId, IReadOnlyList<PvpBallSnapshot> challenger, IReadOnlyList<PvpBallSnapshot> defender)
        {
            if (challenger == null || defender == null || challenger.Count != PvpTuning.TeamSize || defender.Count != PvpTuning.TeamSize)
                throw new ArgumentException("Both teams need three balls.");
            foreach (var b in challenger) if (b == null) throw new ArgumentException("Missing ball.");
            foreach (var b in defender) if (b == null) throw new ArgumentException("Missing ball.");
            ArenaRegistry.Get(arenaId);
            return new PvpDraft(new string[0], new string[0], arenaId, Copy(challenger), Copy(defender));
        }

        static PvpBallSnapshot[] Copy(IReadOnlyList<PvpBallSnapshot> list)
        {
            var a = new PvpBallSnapshot[list.Count];
            for (var i = 0; i < a.Length; i++) a[i] = list[i];
            return a;
        }

        public static PvpDraft Resolve(PvpBan challengerBan, PvpBan defenderBan,
            IReadOnlyList<string> challengerPicks, IReadOnlyList<PvpBallSnapshot> challengerBalls,
            IReadOnlyList<PvpBallSnapshot> defenderPriority, uint serverSeed)
        {
            if (challengerBalls == null || defenderPriority == null || defenderPriority.Count < PvpTuning.TeamSize)
                throw new ArgumentException("Both profiles need balls and the defender needs three fallbacks.");
            if (challengerPicks == null || challengerPicks.Count != PvpTuning.TeamSize)
                throw new ArgumentException("Pick three balls.");
            var distinctDefense = new HashSet<string>();
            foreach (var b in defenderPriority)
                if (b == null || !distinctDefense.Add(b.Id))
                    throw new ArgumentException("Defense roster must contain distinct valid balls.", nameof(defenderPriority));
            var bannedBalls = Distinct(challengerBan.BallId, defenderBan.BallId);
            var bannedArenas = Distinct(challengerBan.ArenaId, defenderBan.ArenaId);

            var challenger = new PvpBallSnapshot[PvpTuning.TeamSize];
            var picked = new HashSet<string>();
            for (var i = 0; i < challenger.Length; i++)
            {
                var id = challengerPicks[i];
                if (id == null || !picked.Add(id) || Contains(bannedBalls, id) || WeaponRegistry.IsPlayerOnly(id))
                    throw new ArgumentException("No legal picked ball remains.");
                foreach (var b in challengerBalls) if (b != null && b.Id == id) challenger[i] = b;
                if (challenger[i] == null) throw new ArgumentException("No legal picked ball remains.");
            }
            var defender = new List<PvpBallSnapshot>();
            foreach (var b in defenderPriority)   // BGLGXLQ (tribute) never fights as a defender
            {
                if (defender.Count == PvpTuning.TeamSize) break;
                if (!Contains(bannedBalls, b.Id) && !WeaponRegistry.IsPlayerOnly(b.Id)) defender.Add(b);
            }
            if (defender.Count < PvpTuning.TeamSize) throw new ArgumentException("No legal defender team remains.");

            var arenas = new List<string>();
            foreach (var a in ArenaRegistry.All)
                if (!Contains(bannedArenas, a.Id)) arenas.Add(a.Id);
            if (arenas.Count == 0) throw new ArgumentException("No legal arena remains.");
            var arena = arenas[(int)(serverSeed % (uint)arenas.Count)];
            return new PvpDraft(bannedBalls, bannedArenas, arena, challenger, defender.ToArray());
        }

        static string[] Distinct(string a, string b) => a == b ? new[] { a } : new[] { a, b };
        static bool Contains(string[] list, string id)
        {
            foreach (var value in list) if (value == id) return true;
            return false;
        }
    }

    /// <summary>Rating policy for one active challenger; the asynchronous defender's MMR is unaffected.</summary>
    public static class PvpRating
    {
        public const int Initial = 1000, Floor = 100, ProvisionalMatches = 10;

        public static double Expected(int self, int opponent) =>
            1.0 / (1.0 + Math.Pow(10.0, (opponent - self) / 400.0));

        /// <summary>Outcome: 1 win, 0 loss, -1 draw/invalid.</summary>
        public static int Delta(int self, int opponent, int completedMatches, int outcome)
        {
            if (outcome == -1) return 0;
            if (outcome != 0 && outcome != 1) throw new ArgumentOutOfRangeException(nameof(outcome));
            var k = completedMatches < ProvisionalMatches ? 32 : 24;
            var raw = (int)Math.Round(k * (outcome - Expected(self, opponent)), MidpointRounding.AwayFromZero);
            if (outcome == 1) return Math.Max(1, raw);
            return Math.Max(Floor - self, Math.Min(-1, raw));
        }
    }
}
