using System;
using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Shared helpers for Run tests.</summary>
    static class RunFixtures
    {
        public static readonly ProgressData Fresh = new ProgressData();

        public static RunState NewRun(uint seed = 77)
        {
            var choices = RunState.StartChoices(seed, UnlockRules.Weapons(Fresh));
            var run = RunState.Start(seed, choices[0], Fresh);
            run.Pick(0);
            return run;
        }

        public static MatchSim Finish(MatchSim m)
        {
            while (m.Outcome == MatchOutcome.Ongoing) m.Step();
            return m;
        }

        /// <summary>Ends the fight with a chosen winner and player HP fraction, without simulating it.</summary>
        public static MatchSim Forced(RunState run, bool playerWins, float playerHpFraction = 1f)
        {
            var m = run.CreateMatch();
            var p = m.Balls[0];
            p.Hp = p.MaxHp * FT.X(playerHpFraction);
            m.DealDamage(null, m.Balls[playerWins ? 1 : 0], FT.X(20000), DamageKind.Hazard);   // < 32767 / 1.2 (SHELL KING takes x1.2 unshelled; Q16.16 would overflow)
            return Finish(m);
        }
    }
}
