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

    /// <summary>One press of the challenger's skill: before step <see cref="Step"/> (0-based) of round <see cref="Round"/>.</summary>
    public readonly struct PvpTap
    {
        public readonly int Round, Step;
        public PvpTap(int round, int step) { Round = round; Step = step; }
    }

    /// <summary>
    /// 21/10 (Rules 24) live ranked combat. The challenger plays: their taps press the ball's active skill, recorded as
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

        /// <summary>The two loadouts of round <paramref name="round"/> (sides swap every round) and the challenger's index.</summary>
        public static MatchSim CreateRound(PvpDraft draft, uint matchSeed, int round, out int challengerIndex)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            var swapped = (round & 1) == 1;
            challengerIndex = swapped ? 1 : 0;
            var challenger = draft.Challenger.ToLoadout();
            var defender = draft.Defender.ToLoadout();
            defender.AutoSkill = true;
            defender.BotFirstTick = PvpTuning.BotFirstTick;
            defender.BotRepeatTicks = PvpTuning.BotRepeatTicks;
            return new MatchSim(ArenaRegistry.Create(draft.ArenaId), SeedFor(matchSeed, round),
                swapped ? new[] { defender, challenger } : new[] { challenger, defender });
        }

        public static uint SeedFor(uint matchSeed, int round) => Series.SeedFor(matchSeed, round);

        /// <summary>The series with the challenger's taps (null or empty: they never pressed).</summary>
        public static PvpSeriesResult Run(PvpDraft draft, uint matchSeed, IReadOnlyList<PvpTap> taps = null)
        {
            if (draft == null) throw new ArgumentNullException(nameof(draft));
            if (taps != null && taps.Count > MaxTaps) throw new ArgumentException("Too many taps.", nameof(taps));
            var seeds = new uint[MaxRounds];
            var hashes = new ulong[MaxRounds];
            var challengerWins = 0;
            var defenderWins = 0;
            var draws = 0;
            var rounds = 0;
            var next = 0;   // taps are applied in order; out-of-order or past ones are skipped
            while (rounds < MaxRounds && challengerWins < 2 && defenderWins < 2)
            {
                var sim = CreateRound(draft, matchSeed, rounds, out var me);
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
                seeds[rounds] = SeedFor(matchSeed, rounds);
                hashes[rounds] = sim.ComputeHash();
                rounds++;
                if (sim.WinnerIndex < 0) draws++;
                else if (sim.WinnerIndex == me) challengerWins++;
                else defenderWins++;
            }
            Array.Resize(ref seeds, rounds);
            Array.Resize(ref hashes, rounds);
            var winner = challengerWins >= 2 ? 0 : defenderWins >= 2 ? 1 : -1;
            return new PvpSeriesResult(winner, challengerWins, defenderWins, draws, seeds, hashes);
        }
    }
}
