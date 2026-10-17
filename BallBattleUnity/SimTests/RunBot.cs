using System;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;

namespace BallBattle.SimTests
{
    /// <summary>Headless run players for difficulty measurement (Phase 2 now, tuned in Phase 7).</summary>
    static class RunBot
    {
        [System.ThreadStatic] public static Action<RunState, Card, BallBattle.Sim.Skills.SkillBranch> PickObserved;

        static void Take(RunState run, int index, BallBattle.Sim.Skills.SkillBranch branch)
        {
            var card = run.OfferCard(index);
            PickObserved?.Invoke(run, card, branch);
            run.Pick(index, branch);
        }
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
        /// <summary>BB-19: the bot plays like a player who taps the skill the moment it is ready.</summary>
        public static void TapIfReady(MatchSim m)
        {
            var tap = m.Balls.Count > 0 ? m.Balls[0].Tap : null;
            if (tap != null && tap.Ready) m.Tap(0);
        }

        public static ProgressData BotProgress() => new ProgressData { RunsPlayed = 1 };

        public static Result Play(uint seed, Policy policy) => Play(seed, policy, null);

        /// <summary><paramref name="onFight"/> (BB-04 reports) sees every finished match before its result is applied.</summary>
        public static Result Play(uint seed, Policy policy, Action<RunState, MatchSim> onFight) => Play(seed, policy, onFight, null);

        /// <summary><paramref name="onStep"/> (BB-6e diagnostics) sees the match after every Step.</summary>
        public static Result Play(uint seed, Policy policy, Action<RunState, MatchSim> onFight, Action<RunState, MatchSim> onStep)
            => Play(seed, policy, onFight, onStep, null);

        /// <summary>BB-BAL1: <paramref name="forcedWeapon"/> replaces the start weapon (any of the 8, unlocked or not); null = normal.</summary>
        public static Result Play(uint seed, Policy policy, Action<RunState, MatchSim> onFight, Action<RunState, MatchSim> onStep, string forcedWeapon)
        {
            var progress = BotProgress();
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress));
            var rng = new SimRandom(seed ^ 0xB07B07u);
            var run = RunState.Start(seed, choices[(int)(rng.NextUInt() % (uint)choices.Count)], progress);
            if (forcedWeapon != null) run.Build.WeaponId = forcedWeapon;
            if (policy == Policy.Random) PickRandom(run, rng);
            else PickGreedyStart(run);

            while (!run.Over)
            {
                if (policy == Policy.Random) PickRandom(run, rng);
                else if (run.StartPickPending) PickGreedyStart(run); // retry grants one card and goes straight to the fight
                else PickGreedy(run);

                var m = run.CreateMatch();
                while (m.Outcome == MatchOutcome.Ongoing) { TapIfReady(m); m.Step(); onStep?.Invoke(run, m); }
                onFight?.Invoke(run, m);
                run.ApplyResult(m);
            }
            return new Result(run.Won, run.History.Count(f => f.Won), run.BossesDefeated);
        }

        static void PickRandom(RunState run, SimRandom rng)
        {
            var options = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).ToArray();
            if (options.Length > 0)
            {
                var index = options[(int)(rng.NextUInt() % (uint)options.Length)];
                var branch = run.OfferCard(index).Kind == CardKind.SkillUp && run.Build.SkillLevel == 1 && rng.NextBool() ?
                    BallBattle.Sim.Skills.SkillBranch.B : BallBattle.Sim.Skills.SkillBranch.A;
                Take(run, index, branch);
            }
        }

        internal static void PickGreedyStart(RunState run)
        {
            var best = Best(run);
            if (best < 0) best = Enumerable.Range(0, run.OfferCount).Where(run.CanPick).DefaultIfEmpty(-1).First();
            if (best >= 0) Take(run, best, GreedyBranch(run, best));
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
                Take(run, best, GreedyBranch(run, best));
            }
        }

        /// <summary>Fixed seeds per level-1 SKILL UP decision (BB-BAL1).</summary>
        public const int BranchTrialSeeds = 8;

        /// <summary>
        /// BB-BAL1: Greedy picks a skill branch by measurement. Only the level-1 SKILL UP chooses (later levels keep the branch): its
        /// current build is played with branch A and with branch B against the next fight's known opponent on that fight's arena,
        /// BranchTrialSeeds seeds derived from the run seed and fight index. Higher win count wins; tie: lower total damage taken;
        /// tie again: A. Bot logic only, no gameplay change; deterministic.
        /// </summary>
        internal static BallBattle.Sim.Skills.SkillBranch GreedyBranch(RunState run, int index)
        {
            var card = run.OfferCard(index);
            if (card.Kind != CardKind.SkillUp || run.Build.SkillLevel != 1) return run.Build.SkillBranch;
            var a = TrialBranch(run, card, BallBattle.Sim.Skills.SkillBranch.A);
            var b = TrialBranch(run, card, BallBattle.Sim.Skills.SkillBranch.B);
            if (b.wins != a.wins) return b.wins > a.wins ? BallBattle.Sim.Skills.SkillBranch.B : BallBattle.Sim.Skills.SkillBranch.A;
            return b.damageTaken < a.damageTaken ? BallBattle.Sim.Skills.SkillBranch.B : BallBattle.Sim.Skills.SkillBranch.A;
        }

        static (int wins, long damageTaken) TrialBranch(RunState run, Card card, BallBattle.Sim.Skills.SkillBranch branch)
        {
            var build = run.Build.Clone();
            card.ApplyTo(build, branch);
            var wins = 0;
            long taken = 0;
            for (var k = 0; k < BranchTrialSeeds; k++)
            {
                var seed = RunTuning.Derive(run.Seed, 7000 + run.FightIndex, k);
                var m = new MatchSim(ArenaRegistry.Create(run.ArenaId), seed, new[] { build.ToLoadout(), run.Enemy.ToLoadout() });
                m.ApplySpawnHooks();
                var start = m.Balls[0].Hp;
                while (m.Outcome == MatchOutcome.Ongoing) { TapIfReady(m); m.Step(); }
                if (m.WinnerIndex == 0) wins++;
                var end = m.Balls[0].Hp > Fix.Zero ? m.Balls[0].Hp : Fix.Zero;
                taken += (start - end).Raw;
            }
            return (wins, taken);
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
                    CardKind.SkillUp => run.Build.SkillLevel == 2 ? 8 : 7,
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
