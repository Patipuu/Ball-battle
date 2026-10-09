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

        public static Result Play(uint seed, Policy policy)
        {
            var progress = new ProgressData();
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress));
            var rng = new SimRandom(seed ^ 0xB07B07u);
            var run = RunState.Start(seed, choices[(int)(rng.NextUInt() % (uint)choices.Count)], progress);

            while (!run.Over)
            {
                if (policy == Policy.Random) PickRandom(run, rng);
                else PickGreedy(run);

                var m = run.CreateMatch();
                while (m.Outcome == MatchOutcome.Ongoing) m.Step();
                run.ApplyResult(m);
            }
            return new Result(run.Won, run.History.Count(f => f.Won), run.BossesDefeated);
        }

        static void PickRandom(RunState run, SimRandom rng)
        {
            var options = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).ToArray();
            if (options.Length > 0) run.Pick(options[(int)(rng.NextUInt() % (uint)options.Length)]);
        }

        /// <summary>Heal when low, else upgrade/add traits, else damage, else max HP; rerolls bad hands, buys a second card when rich.</summary>
        static void PickGreedy(RunState run)
        {
            while (Best(run) < 0 && run.CanReroll && run.Coins > RunTuning.ExtraPickCost) run.Reroll();
            for (var pick = 0; pick < RunTuning.MaxPicksPerFight; pick++)
            {
                var best = Best(run);
                if (best < 0) best = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).DefaultIfEmpty(-1).First();
                if (best < 0 || !run.CanPick(best)) return;
                if (pick > 0 && run.Coins < RunTuning.ExtraPickCost + RunTuning.RerollCost) return;
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
                    CardKind.Heal => run.Build.Hp < run.Build.MaxHp * 0.5f ? 10 : 1,
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
