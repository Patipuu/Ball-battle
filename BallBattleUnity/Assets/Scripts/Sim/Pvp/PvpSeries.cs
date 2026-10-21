using System;
using System.Collections.Generic;

namespace BallBattle.Sim.Pvp
{
    /// <summary>Server-verifiable result of one ranked best-of-three challenge.</summary>
    public sealed class PvpSeriesResult
    {
        public readonly int Winner, ChallengerWins, DefenderWins, Draws, RulesVersion;
        public readonly uint[] RoundSeeds;
        public readonly ulong[] RoundHashes;
        /// <summary>Per round: team pair played (0..2) and who won it (0 challenger, 1 defender, -1 draw).</summary>
        public readonly int[] RoundPairs, RoundWinners;

        internal PvpSeriesResult(int winner, int challengerWins, int defenderWins, int draws,
            uint[] seeds, ulong[] hashes, int[] pairs, int[] winners)
        {
            RoundPairs = pairs;
            RoundWinners = winners;
            Winner = winner;
            ChallengerWins = challengerWins;
            DefenderWins = defenderWins;
            Draws = draws;
            RulesVersion = SimVersion.Rules;
            RoundSeeds = seeds;
            RoundHashes = hashes;
        }
    }

    /// <summary>One press of the challenger's skill: before step <see cref="Step"/> (0-based) of round <see cref="Round"/>.</summary>
    public readonly struct PvpTap
    {
        public readonly int Round, Step;
        public PvpTap(int round, int step) { Round = round; Step = step; }
    }

    /// <summary>
    /// 21/10 (Rules 24) live ranked combat, teams of three: round pair p is team slot p against slot p; a decided round moves to
    /// the next pair, a draw replays the same pair; first to two rounds wins. The challenger plays: their taps press the ball's active skill, recorded as
    /// (round, step). The defender is not online, so a bot presses its skill (BallLoadout.AutoSkill, PvpTuning timing).
    /// The service replays the recorded taps on the same deterministic sim, so the client never reports a result, only
    /// its inputs. No taps (left the fight, never sent) is simply a fight where the challenger never pressed.
    /// </summary>
    public static class PvpSeries
    {
        public const int MaxRounds = 9;
        /// <summary>Taps accepted per series (a press every few frames for nine long rounds stays far below).</summary>
        public const int MaxTaps = 4000;
        /// <summary>A round longer than this many steps is cut (the sim's own time cap ends rounds long before).</summary>
        public const int MaxStepsPerRound = 60 * 60 * 5;

        /// <summary>Round <paramref name="round"/> with team pair <paramref name="pair"/> (sides swap every round) and the challenger's index.</summary>
        public static MatchSim CreateRound(PvpDraft draft, uint matchSeed, int round, int pair, out int challengerIndex)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (pair < 0 || pair >= PvpTuning.TeamSize) throw new ArgumentOutOfRangeException(nameof(pair));
            var swapped = (round & 1) == 1;
            challengerIndex = swapped ? 1 : 0;
            var challenger = draft.ChallengerTeam[pair].ToLoadout();
            var defender = draft.DefenderTeam[pair].ToLoadout();
            defender.AutoSkill = true;
            defender.BotFirstTick = PvpTuning.BotFirstTick;
            defender.BotRepeatTicks = PvpTuning.BotRepeatTicks;
            return new MatchSim(ArenaRegistry.Create(draft.ArenaId), SeedFor(matchSeed, round),
                swapped ? new[] { defender, challenger } : new[] { challenger, defender });
        }

        public static uint SeedFor(uint matchSeed, int round) => Series.SeedFor(matchSeed, round);

        /// <summary>Pair after a round: the next one when it was decided, the same one after a draw.</summary>
        public static int NextPair(int pair, int roundWinner) => roundWinner < 0 ? pair : pair + 1;

        /// <summary>True when the series goes on after these counts.</summary>
        public static bool Continues(int rounds, int pair, int challengerWins, int defenderWins) =>
            rounds < MaxRounds && pair < PvpTuning.TeamSize && challengerWins < 2 && defenderWins < 2;

        /// <summary>The series with the challenger's taps (null or empty: they never pressed).</summary>
        public static PvpSeriesResult Run(PvpDraft draft, uint matchSeed, IReadOnlyList<PvpTap> taps = null)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (taps != null && taps.Count > MaxTaps) throw new ArgumentException("Too many taps.", nameof(taps));
            var seeds = new List<uint>();
            var hashes = new List<ulong>();
            var pairs = new List<int>();
            var winners = new List<int>();
            int challengerWins = 0, defenderWins = 0, draws = 0, rounds = 0, pair = 0;
            var next = 0;   // taps are applied in order; out-of-order or past ones are skipped
            while (Continues(rounds, pair, challengerWins, defenderWins))
            {
                var sim = CreateRound(draft, matchSeed, rounds, pair, out var me);
                var step = 0;
                while (sim.Outcome == MatchOutcome.Ongoing && step < MaxStepsPerRound)
                {
                    while (taps != null && next < taps.Count && (taps[next].Round < rounds || (taps[next].Round == rounds && taps[next].Step < step))) next++;
                    var pressed = false;
                    while (taps != null && next < taps.Count && taps[next].Round == rounds && taps[next].Step == step)
                    {
                        if (!pressed) sim.Tap(me);   // several taps on one step are one press
                        pressed = true;
                        next++;
                    }
                    sim.Step();
                    step++;
                }
                var w = sim.WinnerIndex < 0 ? -1 : sim.WinnerIndex == me ? 0 : 1;
                seeds.Add(SeedFor(matchSeed, rounds));
                hashes.Add(sim.ComputeHash());
                pairs.Add(pair);
                winners.Add(w);
                rounds++;
                if (w < 0) draws++;
                else if (w == 0) challengerWins++;
                else defenderWins++;
                pair = NextPair(pair, w);
            }
            var winner = challengerWins >= 2 ? 0 : defenderWins >= 2 ? 1 : -1;
            return new PvpSeriesResult(winner, challengerWins, defenderWins, draws, seeds.ToArray(), hashes.ToArray(), pairs.ToArray(), winners.ToArray());
        }
    }
}
