using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Skills
{
    /// <summary>Deterministic Greedy estimate of the two level-2 branches; ties favour A. No outcome-dependent tuning.</summary>
    public static class SkillChoice
    {
        public static Fix Score(RunBuild build, SkillBranch branch)
        {
            var a = branch == SkillBranch.A;
            switch (build.WeaponId)
            {
                case "blade": return a ? Fix.Ratio(5, 2) : (Fix)3;
                case "fang": return Fix.Ratio(3, 2); // 50% extra duration vs 50% extra spin gain
                case "pike": return a ? Fix.Ratio(1, 2) * 120 : Fix.Ratio(1, 3) * 180;
                case "brawler": return a ? Fix.Ratio(7, 4) / Fix.Ratio(3, 2) : Fix.Ratio(150, 90);
                case "volley": return a ? Fix.Ratio(720, 600) : Fix.One + Fix.Ratio(1, 4) / WeaponTuning.VolleyArrowDamage;
                case "venom": return a ? Fix.Ratio(2, 3) : Fix.Half * Fix.Ratio(WeaponTuning.VenomSeconds * 60 + 60, WeaponTuning.VenomSeconds * 60);
                case "aegis": return a ? Fix.Ratio(160, 120) : Fix.Ratio(2, 3) / Fix.Ratio(3, 5);
                case "rig": return a ? Fix.Ratio(3, 2) : (Fix)2;
                default: return Fix.Zero;
            }
        }
        public static SkillBranch Choose(RunBuild build) => Score(build, SkillBranch.B) > Score(build, SkillBranch.A) ? SkillBranch.B : SkillBranch.A;
    }
}
