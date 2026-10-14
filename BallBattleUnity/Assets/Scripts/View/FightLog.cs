using System.Globalization;
using BallBattle.Sim;

namespace BallBattle.View
{
    /// <summary>
    /// One Player.log line per finished fight, with the real values (2 decimals, not the rounded display):
    /// [BallBattle] Fight N ended: seed S, outcome Win|Lost|Draw, tick T, you &lt;id&gt; HP start a heal h end b,
    /// enemy &lt;id&gt; HP start a heal h end b, DMG DEALT d TAKEN t
    /// "you" is ball 0 (the player in Run mode, ball 1 of the menu in Versus).
    /// </summary>
    public static class FightLog
    {
        /// <param name="runSeed">Run mode: the run seed (what -bbSeed takes), printed as "seed S (match M)". Versus: null, the match seed.</param>
        public static string Ended(string fight, MatchSim match, FightStats stats, uint? runSeed = null)
        {
            if (match == null) return $"[BallBattle] {fight} ended: no match";
            var outcome = match.WinnerIndex == 0 ? "Win" : match.WinnerIndex < 0 ? "Draw" : "Lost";
            string Ball(int i)
            {
                if (i >= match.Balls.Count) return "-";
                var b = match.Balls[i];
                var start = stats != null ? stats.StartHp(i) : 0f;
                var heal = stats != null ? stats.Healed(i) : 0f;
                return $"{b.Weapon.Id} HP start {N(start)} heal {N(heal)} end {N(b.Hp.ToFloat() > 0f ? b.Hp.ToFloat() : 0f)}";
            }
            var dealt = stats != null ? stats.Dealt(0) : 0f;
            var taken = stats != null ? stats.Taken(0) : 0f;
            var seed = runSeed.HasValue ? $"{runSeed.Value} (match {match.Seed})" : match.Seed.ToString();
            return $"[BallBattle] {fight} ended: seed {seed}, outcome {outcome}, tick {match.Tick}, you {Ball(0)}, enemy {Ball(1)}, DMG DEALT {N(dealt)} TAKEN {N(taken)}";
        }

        static string N(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
