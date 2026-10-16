using BallBattle.Sim.Run;

namespace BallBattle.Sim.Meta
{
    /// <summary>The nodes of a ball's skill tree (BB-12g, docs/design/bb12-persona-web-v2.md).</summary>
    public enum TreeNode { Vitality, Power, Instinct, Opener, SkillA, SkillB, Finale }

    /// <summary>
    /// BB-12g skill tree rules (pure C#). Points = ball level (MetaRules.TreePoints). Costs: VITALITY / POWER / INSTINCT 1 per rank
    /// (max 2 each), OPENER 2, skill LV2 1, skill LV3 1, FINALE 3. OPENER and skill LV2 need INSTINCT 1 and 2 points in the root
    /// row; skill LV3 needs 5 points spent below it; FINALE needs skill LV3 (same branch). Re-spending is free outside a run (Reset).
    /// </summary>
    public static class MetaTree
    {
        public const int MaxRootRank = 2, OpenerCost = 2, SkillCost = 1, FinaleCost = 3;

        public static int Root(BallProgress b) => b.Vit + b.Pow + b.Growth;

        public static int Spent(BallProgress b) => Root(b) + (b.Start ? OpenerCost : 0) + b.SkillUps * SkillCost + (b.Finale ? FinaleCost : 0);

        public static int Free(BallProgress b) => MetaRules.TreePoints(b.Level) - Spent(b);

        /// <summary>The middle row (OPENER, skill LV2) is open: INSTINCT 1 and 2 root points.</summary>
        public static bool MiddleOpen(BallProgress b) => b.Growth >= 1 && Root(b) >= 2;

        /// <summary>Points spent below skill LV3: root + OPENER + skill LV2.</summary>
        public static int Lower(BallProgress b) => Root(b) + (b.Start ? OpenerCost : 0) + (b.SkillUps >= 1 ? SkillCost : 0);

        public static int Cost(TreeNode n) => n == TreeNode.Opener ? OpenerCost : n == TreeNode.Finale ? FinaleCost : 1;

        /// <summary>Can <paramref name="n"/> be bought now (its prerequisites, its own cap and enough free points)?</summary>
        public static bool CanAdd(BallProgress b, TreeNode n)
        {
            if (b == null || Free(b) < Cost(n)) return false;
            switch (n)
            {
                case TreeNode.Vitality: return b.Vit < MaxRootRank;
                case TreeNode.Power: return b.Pow < MaxRootRank;
                case TreeNode.Instinct: return b.Growth < MaxRootRank;
                case TreeNode.Opener: return !b.Start && MiddleOpen(b);
                case TreeNode.SkillA:
                case TreeNode.SkillB:
                    var branch = n == TreeNode.SkillA ? 1 : 2;
                    if (b.SkillUps == 0) return MiddleOpen(b);
                    return b.SkillUps == 1 && b.Branch == branch && Lower(b) >= 5;
                case TreeNode.Finale: return !b.Finale && b.SkillUps == 2;
                default: return false;
            }
        }

        public static bool Add(BallProgress b, TreeNode n)
        {
            if (!CanAdd(b, n)) return false;
            switch (n)
            {
                case TreeNode.Vitality: b.Vit++; break;
                case TreeNode.Power: b.Pow++; break;
                case TreeNode.Instinct: b.Growth++; break;
                case TreeNode.Opener: b.Start = true; break;
                case TreeNode.SkillA:
                case TreeNode.SkillB:
                    if (b.SkillUps == 0) b.Branch = n == TreeNode.SkillA ? 1 : 2;
                    b.SkillUps++;
                    break;
                case TreeNode.Finale: b.Finale = true; break;
            }
            return true;
        }

        /// <summary>Free respec: every point back.</summary>
        public static void Reset(BallProgress b)
        {
            b.Vit = b.Pow = b.Growth = b.SkillUps = b.Branch = 0;
            b.Start = b.Finale = false;
        }

        /// <summary>A saved tree that breaks the rules (edited save, a later rule change) is not used: it counts as empty.</summary>
        public static bool Valid(BallProgress b)
        {
            if (b == null) return false;
            if (b.Vit < 0 || b.Pow < 0 || b.Growth < 0 || b.Vit > MaxRootRank || b.Pow > MaxRootRank || b.Growth > MaxRootRank) return false;
            if (b.SkillUps < 0 || b.SkillUps > 2 || (b.SkillUps > 0) != (b.Branch == 1 || b.Branch == 2)) return false;
            if ((b.Start || b.SkillUps > 0) && !MiddleOpen(b)) return false;
            if (b.SkillUps == 2 && Lower(b) < 5) return false;
            if (b.Finale && b.SkillUps != 2) return false;
            return Spent(b) <= MetaRules.TreePoints(b.Level);
        }

        /// <summary>
        /// Run start: the ball's tree goes into the run. VITALITY / POWER scale max HP / damage (per-ball %), the skill level and branch
        /// come from the tree, and the in-match nodes are copied (RunBuild.Tree*). Every Run has a tree (maybe empty): TreeOn.
        /// </summary>
        public static void ApplyToRun(RunBuild build, BallProgress tree)
        {
            build.TreeOn = true;
            if (tree == null || !Valid(tree)) return;
            build.MaxHp *= Fix.Ratio(100 + TreeTuning.HpPct(build.WeaponId) * tree.Vit, 100);
            build.Hp = build.MaxHp;
            build.DamagePct += Fix.Ratio(TreeTuning.DamagePct(build.WeaponId) * tree.Pow, 100);
            build.SkillLevel = 1 + tree.SkillUps;
            build.SkillBranch = tree.SkillUps == 0 ? Skills.SkillBranch.None : tree.Branch == 1 ? Skills.SkillBranch.A : Skills.SkillBranch.B;
            build.TreeGrowth = tree.Growth;
            build.TreeStart = tree.Start;
            build.TreeFinale = tree.Finale ? tree.Branch : 0;
        }
    }
}
