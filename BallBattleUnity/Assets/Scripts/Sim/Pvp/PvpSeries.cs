using System;

namespace BallBattle.Sim.Pvp
{
    /// <summary>Server-verifiable result of one ranked best-of-three challenge.</summary>
    public sealed class PvpSeriesResult
    {
        public readonly int Winner, ChallengerWins, DefenderWins, Draws, RulesVersion;
        public readonly uint[] RoundSeeds;
        public readonly ulong[] RoundHashes;

        internal PvpSeriesResult(int winner, int challengerWins, int defenderWins, int draws,
            uint[] seeds, ulong[] hashes)
        {
            Winner = winner;
            ChallengerWins = challengerWins;
            DefenderWins = defenderWins;
            Draws = draws;
            RulesVersion = SimVersion.Rules;
            RoundSeeds = seeds;
            RoundHashes = hashes;
        }
    }

    /// <summary>
    /// Ranked combat is deterministic and unattended on both sides. Each ball uses its full permanent tree and the
    /// same tap/active-skill code as Run; one identical policy triggers a ready active skill for both players.
    /// The service can recompute this result from its frozen snapshots instead of trusting the Unity client.
    /// </summary>
    public static class PvpSeries
    {
        public const int MaxRounds = 9;

        public static PvpSeriesResult Run(PvpDraft draft, uint matchSeed)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var seeds = new uint[MaxRounds];
            var hashes = new ulong[MaxRounds];
            var challengerWins = 0;
            var defenderWins = 0;
            var draws = 0;
            var rounds = 0;
            while (rounds < MaxRounds && challengerWins < 2 && defenderWins < 2)
            {
                var swapped = (rounds & 1) == 1;
                var seed = Series.SeedFor(matchSeed, rounds);
                var a = (swapped ? draft.Defender : draft.Challenger).ToLoadout();
                var b = (swapped ? draft.Challenger : draft.Defender).ToLoadout();
                var sim = new MatchSim(ArenaRegistry.Create(draft.ArenaId), seed, new[] { a, b });
                while (sim.Outcome == MatchOutcome.Ongoing)
                {
                    BeforeStep(sim);
                    sim.Step();
                }
                seeds[rounds] = seed;
                hashes[rounds] = sim.ComputeHash();
                rounds++;
                if (sim.WinnerIndex < 0) draws++;
                else if ((sim.WinnerIndex == 0) != swapped) challengerWins++;
                else defenderWins++;
            }
            Array.Resize(ref seeds, rounds);
            Array.Resize(ref hashes, rounds);
            var winner = challengerWins >= 2 ? 0 : defenderWins >= 2 ? 1 : -1;
            return new PvpSeriesResult(winner, challengerWins, defenderWins, draws, seeds, hashes);
        }

        /// <summary>Shared policy for the service simulation and Unity replay.</summary>
        public static void BeforeStep(MatchSim sim)
        {
            AutoTap(sim, 0);
            AutoTap(sim, 1);
        }

        static void AutoTap(MatchSim sim, int index)
        {
            var ball = sim.Balls[index];
            if (!ball.Alive || ball.Tap == null || ball.Skill?.Act == null) return;
            if (ball.Tap.ReadyAt <= sim.ActiveTick && ball.Skill.Act.CanTap) sim.Tap(index);
        }
    }
}
