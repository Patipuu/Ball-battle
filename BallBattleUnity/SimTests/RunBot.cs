using System;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;

namespace BallBattle.SimTests
{
    /// <summary>Headless run players for difficulty measurement (Phase 2 now, tuned in Phase 7).</summary>
    static class RunBot
    {
        public enum Policy { Random, Greedy }

        public readonly struct Result
        {
            public readonly bool Won;
            public readonly int FightsWon;
            public readonly int BossesDefeated;
            public Result(bool won, int fightsWon, int bosses) { Won = won; FightsWon = fightsWon; BossesDefeated = bosses; }
        }

        /// <summary>
        /// BB-04: the bot plays as a returning player (one run already played, no "last boss" remembered), so each run's bosses come
        /// from its seed (both bosses of each slot appear across seeds). A fresh save would always meet SHELL KING + MIMIC (first-run
        /// rule). Unlocks depend only on achievements, so the bot's weapons and traits are the same as before.
        /// </summary>
        public static ProgressData BotProgress() => new ProgressData { RunsPlayed = 1 };

        public static Result Play(uint seed, Policy policy) => Play(seed, policy, null);

        /// <summary><paramref name="onFight"/> (BB-04 reports) sees every finished match before its result is applied.</summary>
        public static Result Play(uint seed, Policy policy, Action<RunState, MatchSim> onFight) => Play(seed, policy, onFight, null);

        /// <summary><paramref name="onStep"/> (BB-6e diagnostics) sees the match after every Step.</summary>
        public static Result Play(uint seed, Policy policy, Action<RunState, MatchSim> onFight, Action<RunState, MatchSim> onStep)
        {
            var progress = BotProgress();
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress));
            var rng = new SimRandom(seed ^ 0xB07B07u);
            var run = RunState.Start(seed, choices[(int)(rng.NextUInt() % (uint)choices.Count)], progress);
            if (policy == Policy.Random) PickRandom(run, rng);
            else PickGreedyStart(run);

            while (!run.Over)
            {
                if (policy == Policy.Random) PickRandom(run, rng);
                else if (run.StartPickPending) PickGreedyStart(run); // retry grants one card and goes straight to the fight
                else PickGreedy(run);

                var m = run.CreateMatch();
                while (m.Outcome == MatchOutcome.Ongoing) { m.Step(); onStep?.Invoke(run, m); }
                onFight?.Invoke(run, m);
                run.ApplyResult(m);
            }
            return new Result(run.Won, run.History.Count(f => f.Won), run.BossesDefeated);
        }

        static void PickRandom(RunState run, SimRandom rng)
        {
            var options = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).ToArray();
            if (options.Length > 0) run.Pick(options[(int)(rng.NextUInt() % (uint)options.Length)]);
        }

        internal static void PickGreedyStart(RunState run)
        {
            var best = Best(run);
            if (best < 0) best = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).DefaultIfEmpty(-1).First();
            if (best >= 0) run.Pick(best);
        }

        /// <summary>Heal when low, else upgrade/add traits, else damage, else max HP; rerolls bad hands, buys a second card when rich.</summary>
        internal static void PickGreedy(RunState run)
        {
            while (Best(run) < 0 && run.CanReroll && run.Coins > RunTuning.ExtraPickCost) run.Reroll();
            for (var pick = 0; pick < RunTuning.MaxPicksPerFight; pick++)
            {
                var best = Best(run);
                if (best < 0) best = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).DefaultIfEmpty(-1).First();
                if (best < 0 || !run.CanPick(best)) return;
                if (pick > 0 && run.Coins < RunTuning.ExtraPickCost + run.NextRerollCost) return;
                run.Pick(best);
            }
        }

        /// <summary>Index of the most wanted card, or -1 if none is worth it.</summary>
        static int Best(RunState run)
        {
            var bestScore = 0;
            var best = -1;
            for (var i = 0; i < run.OfferCount; i++)
            {
                if (!run.CanPick(i)) continue;
                var c = run.OfferCard(i);
                var score = c.Kind switch
                {
                    CardKind.Heal => run.Build.Hp < run.Build.MaxHp / 2 ? 10 : 1,
                    CardKind.Trait => c.Level == 2 ? 8 : 7,
                    CardKind.DamageUp => 6,
                    CardKind.MaxHp => 4,
                    CardKind.SpeedUp => 3,
                    _ => 0
                };
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return bestScore >= 3 ? best : -1;
        }
    }
}
