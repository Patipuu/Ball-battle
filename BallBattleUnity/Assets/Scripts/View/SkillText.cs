using BallBattle.Sim.Skills;

namespace BallBattle.View
{
    public static class SkillText
    {
        public static string Line(string weapon, int level, SkillBranch branch)
        {
            var definition = SkillDefinition.ForWeapon(weapon);
            if (definition == null) return "";
            var label = UiVietnamese.Translate(definition.Name) + " " + level;
            return branch == SkillBranch.None ? label : label + " " + UiVietnamese.Translate(definition.BranchName(branch));
        }
    }
}
