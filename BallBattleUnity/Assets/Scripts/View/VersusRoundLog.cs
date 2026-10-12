using System;
using System.Globalization;
using BallBattle.Sim;

namespace BallBattle.View
{
    public static class VersusRoundLog
    {
        public static string Ended(Series series, MatchSim match)
        {
            var winner = match.WinnerIndex == 0 ? "A" : match.WinnerIndex == 1 ? "B" : "DRAW";
            string Hp(int i)
            {
                var hp = match.Balls[i].Hp;
                return SimTrace.Bits(hp) + "/" + hp.ToString("R", CultureInfo.InvariantCulture);
            }
            return $"[BallBattle] Versus round {series.DisplayRound} ended: match seed {series.MatchSeed}, round seed {match.Seed}, " +
                   $"{series.WeaponA} vs {series.WeaponB}, winner {winner}, tick {match.Tick}, hp A={Hp(0)} B={Hp(1)}";
        }
    }
}
