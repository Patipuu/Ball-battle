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
            MetaTree.ApplyToRun(build, progress);
            return build;
        }

        public BallLoadout ToLoadout()
        {
            var build = ToBuild();
            PvpTuning.Apply(build);
            var loadout = build.ToLoadout();
            loadout.TapEnabled = true;   // the same active tree skill as Run: the challenger taps it, the defender's bot presses it
            return loadout;
        }
    }

    /// <summary>
    /// 21/10 PvP-only balance (Rules 24): each ball's max HP and damage in ranked fights, in percent of its Run values, for its
    /// skill branch (none before skill LV2, then A or B). Run and Versus never read this. Tuned with PvP series simulations
    /// (both sides on the PvP bot policy, levels 1, 5 and 10) so every ball and branch wins about half of its series.
    /// </summary>
    public static class PvpTuning
    {
        /// <summary>Power in percent of Run HP and damage: { no branch, branch A, branch B } per weapon id.</summary>
        public static readonly Dictionary<string, int[]> Power = new Dictionary<string, int[]>
        {
            // Before (all 100): HIVE won 80-98% of its series, SPORE 70-90%, EMBER 60-90%; WRECKER 0-5%, SPARKLER ~20%, MIRROR ~25%.
            { "blade", new[] { 95, 81, 103 } }, { "fang", new[] { 90, 93, 95 } }, { "pike", new[] { 118, 130, 121 } },
            { "brawler", new[] { 153, 170, 153 } }, { "volley", new[] { 129, 142, 139 } }, { "venom", new[] { 98, 71, 91 } },
            { "aegis", new[] { 130, 136, 144 } }, { "rig", new[] { 80, 79, 69 } }, { "serpent", new[] { 134, 149, 116 } },
        };

        /// <summary>Defender bot (AutoSkill): first cast after this many ticks, then this long after the skill is ready again.</summary>
        public const int BotFirstTick = 180, BotRepeatTicks = 60;

        public static int PercentFor(string weaponId, Skills.SkillBranch branch) =>
            Power.TryGetValue(weaponId ?? "", out var p) ? p[(int)branch] : 100;

        public static void Apply(RunBuild build)
        {
            var pct = PercentFor(build.WeaponId, build.SkillBranch);
            if (pct == 100) return;
            build.MaxHp = FixMath.Max(Fix.One, FixMath.RoundToEven(build.MaxHp * Fix.Ratio(pct, 100)));
            build.Hp = build.MaxHp;
            build.DamagePct = (Fix.One + build.DamagePct) * Fix.Ratio(pct, 100) - Fix.One;   // scales the tree's bonus too
        }
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

    /// <summary>Resolved ranked draft. Both bans apply to both sides; a duplicate removes only one option.</summary>
    public sealed class PvpDraft
    {
        public readonly string[] BannedBalls, BannedArenas;
        public readonly string ArenaId;
        public readonly PvpBallSnapshot Challenger, Defender;

        PvpDraft(string[] bannedBalls, string[] bannedArenas, string arenaId,
            PvpBallSnapshot challenger, PvpBallSnapshot defender)
        {
            BannedBalls = bannedBalls;
            BannedArenas = bannedArenas;
            ArenaId = arenaId;
            Challenger = challenger;
            Defender = defender;
        }

        /// <summary>21/10 live PvP: the draft the server already resolved (its arena and both picked balls), rebuilt on the client.</summary>
        public static PvpDraft ForFight(string arenaId, PvpBallSnapshot challenger, PvpBallSnapshot defender)
        {
            if (challenger == null || defender == null) throw new ArgumentException("Both balls are needed.");
            ArenaRegistry.Get(arenaId);
            return new PvpDraft(new string[0], new string[0], arenaId, challenger, defender);
        }

        public static PvpDraft Resolve(PvpBan challengerBan, PvpBan defenderBan,
            string challengerPick, IReadOnlyList<PvpBallSnapshot> challengerBalls,
            IReadOnlyList<PvpBallSnapshot> defenderPriority, uint serverSeed)
        {
            if (challengerBalls == null || defenderPriority == null || defenderPriority.Count < 3)
                throw new ArgumentException("Both profiles need balls and the defender needs three fallbacks.");
            var distinctDefense = new HashSet<string>();
            foreach (var b in defenderPriority)
                if (b == null || !distinctDefense.Add(b.Id))
                    throw new ArgumentException("Defense roster must contain distinct valid balls.", nameof(defenderPriority));
            var bannedBalls = Distinct(challengerBan.BallId, defenderBan.BallId);
            var bannedArenas = Distinct(challengerBan.ArenaId, defenderBan.ArenaId);
            PvpBallSnapshot challenger = null, defender = null;
            foreach (var b in challengerBalls)
                if (b != null && b.Id == challengerPick && !Contains(bannedBalls, b.Id) && !WeaponRegistry.IsPlayerOnly(b.Id)) challenger = b;
            foreach (var b in defenderPriority)   // BGLGXLQ (tribute) never fights as a defender
                if (b != null && !Contains(bannedBalls, b.Id) && !WeaponRegistry.IsPlayerOnly(b.Id)) { defender = b; break; }
            if (challenger == null || defender == null) throw new ArgumentException("No legal picked ball remains.");

            var arenas = new List<string>();
            foreach (var a in ArenaRegistry.All)
                if (!Contains(bannedArenas, a.Id)) arenas.Add(a.Id);
            if (arenas.Count == 0) throw new ArgumentException("No legal arena remains.");
            var arena = arenas[(int)(serverSeed % (uint)arenas.Count)];
            return new PvpDraft(bannedBalls, bannedArenas, arena, challenger, defender);
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
