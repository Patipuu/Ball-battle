using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>BB-12g: skill-tree rules (costs, prerequisites, branches, respec) and what a run takes from the tree.</summary>
    public class MetaTreeTests
    {
        static BallProgress Ball(int level) => new BallProgress { Id = "blade", Level = level };

        [Test]
        public void LevelOneBuysOneRootRankOnly()
        {
            var b = Ball(1);
            Assert.That(MetaTree.CanAdd(b, TreeNode.Opener), Is.False);
            Assert.That(MetaTree.CanAdd(b, TreeNode.SkillA), Is.False);
            Assert.That(MetaTree.Add(b, TreeNode.Vitality), Is.True);
            Assert.That(MetaTree.Free(b), Is.Zero);
            Assert.That(MetaTree.Add(b, TreeNode.Power), Is.False, "no point left");
        }

        [Test]
        public void MiddleRowNeedsInstinctAndTwoRootPoints()
        {
            var b = Ball(10);
            MetaTree.Add(b, TreeNode.Vitality);
            MetaTree.Add(b, TreeNode.Power);
            Assert.That(MetaTree.CanAdd(b, TreeNode.Opener), Is.False, "2 root points but no INSTINCT");
            MetaTree.Add(b, TreeNode.Instinct);
            Assert.That(MetaTree.CanAdd(b, TreeNode.Opener), Is.True);
            Assert.That(MetaTree.CanAdd(b, TreeNode.SkillB), Is.True);
        }

        [Test]
        public void SkillLevelThreeNeedsFivePointsBelowAndFinaleNeedsIt()
        {
            var b = Ball(10);
            MetaTree.Add(b, TreeNode.Vitality);
            MetaTree.Add(b, TreeNode.Instinct);
            MetaTree.Add(b, TreeNode.SkillA);   // lower = 3
            Assert.That(MetaTree.CanAdd(b, TreeNode.SkillA), Is.False, "LV3 needs 5 points below");
            Assert.That(MetaTree.CanAdd(b, TreeNode.SkillB), Is.False, "the other branch is closed");
            Assert.That(MetaTree.CanAdd(b, TreeNode.Finale), Is.False);
            MetaTree.Add(b, TreeNode.Opener);   // lower = 5
            Assert.That(MetaTree.Add(b, TreeNode.SkillA), Is.True);
            Assert.That(MetaTree.Add(b, TreeNode.Finale), Is.True);
            Assert.That((b.SkillUps, b.Branch, b.Finale, MetaTree.Spent(b)), Is.EqualTo((2, 1, true, 9)));
            Assert.That(MetaTree.Valid(b), Is.True);
            MetaTree.Reset(b);
            Assert.That((MetaTree.Spent(b), b.Branch, b.Finale), Is.EqualTo((0, 0, false)));
        }

        /// <summary>The design counts 18 different builds that spend all 10 points at LV10, 16 of them with a FINALE (bb12-persona-web-v2 §2).</summary>
        [Test]
        public void EighteenFullBuildsAtLevelTen()
        {
            static string Key(BallProgress b) => $"{b.Vit}{b.Pow}{b.Growth}{(b.Start ? 1 : 0)}{b.SkillUps}{b.Branch}{(b.Finale ? 1 : 0)}";
            static BallProgress Copy(BallProgress b) => new BallProgress { Id = b.Id, Level = b.Level, Vit = b.Vit, Pow = b.Pow, Growth = b.Growth, Start = b.Start, SkillUps = b.SkillUps, Branch = b.Branch, Finale = b.Finale };
            var seen = new Dictionary<string, BallProgress>();
            var stack = new Stack<BallProgress>();
            stack.Push(Ball(10));
            var nodes = (TreeNode[])System.Enum.GetValues(typeof(TreeNode));
            while (stack.Count > 0)
            {
                var b = stack.Pop();
                if (seen.ContainsKey(Key(b))) continue;
                seen[Key(b)] = b;
                foreach (var n in nodes)
                {
                    var c = Copy(b);
                    if (MetaTree.Add(c, n)) stack.Push(c);
                }
            }
            Assert.That(seen.Values.All(MetaTree.Valid), Is.True);
            Assert.That(seen.Values.Count(b => MetaTree.Spent(b) == 10), Is.EqualTo(18));
            Assert.That(seen.Values.Count(b => MetaTree.Spent(b) == 10 && b.Finale), Is.EqualTo(16));
        }

        [Test]
        public void AnInvalidSavedTreeCountsAsEmpty()
        {
            var b = new BallProgress { Id = "blade", Level = 1, Vit = 2, Pow = 2 };   // 4 points at LV1
            Assert.That(MetaTree.Valid(b), Is.False);
            var build = new RunBuild { WeaponId = "blade" };
            MetaTree.ApplyToRun(build, b);
            Assert.That((build.TreeOn, build.MaxHp, build.DamagePct, build.SkillLevel), Is.EqualTo((true, RunTuning.PlayerStartHp, Fix.Zero, 1)));
        }

        [Test]
        public void RunStartTakesTheBallsTree()
        {
            var p = new ProgressData();
            var id = RunState.StartChoices(7, UnlockRules.Weapons(p))[0];
            var t = MetaRules.Ball(p, id);
            t.Level = 10;
            MetaTree.Add(t, TreeNode.Vitality);
            MetaTree.Add(t, TreeNode.Power);
            MetaTree.Add(t, TreeNode.Instinct);
            MetaTree.Add(t, TreeNode.SkillB); MetaTree.Add(t, TreeNode.Opener); MetaTree.Add(t, TreeNode.SkillB);
            MetaTree.Add(t, TreeNode.Finale);
            Assert.That(MetaTree.Spent(t), Is.EqualTo(10));
            var run = RunState.Start(7, id, p);
            Assert.That((run.Build.SkillLevel, run.Build.SkillBranch), Is.EqualTo((3, SkillBranch.B)));
            Assert.That(run.Build.MaxHp, Is.EqualTo(BallPower.BaseHp(id) * Fix.Ratio(100 + TreeTuning.HpPct(id), 100)));
            Assert.That(run.Build.Hp, Is.EqualTo(run.Build.MaxHp));
            Assert.That(run.Build.DamagePct, Is.EqualTo(Fix.Ratio(TreeTuning.DamagePct(id), 100)));
            Assert.That((run.Build.TreeOn, run.Build.TreeGrowth, run.Build.TreeStart, run.Build.TreeFinale), Is.EqualTo((true, 1, true, 2)));
            run.Pick(0);
            var m = run.CreateMatch();
            Assert.That(m.Balls[0].Tree, Is.Not.Null);
            Assert.That((m.Balls[0].Tree.Growth, m.Balls[0].Tree.Start, m.Balls[0].Tree.Finale), Is.EqualTo((1, true, 'B')));
            Assert.That(m.Balls[1].Tree, Is.Null, "enemies have no tree");
        }

        [Test]
        public void RunsNoLongerOfferSkillUp()
        {
            var p = new ProgressData();
            for (uint seed = 1; seed <= 30; seed++)
            {
                var run = RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(p))[0], p);
                for (var i = 0; i < run.OfferCount; i++) Assert.That(run.OfferCard(i).Kind, Is.Not.EqualTo(CardKind.SkillUp));
            }
        }

        [Test]
        public void AnEmptyTreeChangesNothingForVersusBalls()
        {
            // A ball with no tree (Versus, enemies) hashes and plays exactly as before BB-12g: the golden tests pin that;
            // here: a Run player with an empty tree still differs only by the Run-only baselines (WRECKER, MIRROR).
            var plain = new MatchSim(new MatchConfig(), 5, new[] { new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("pike")), new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("fang")) });
            var treed = new MatchSim(new MatchConfig(), 5, new[] { new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("pike")) { Tree = new TreeLoadout() }, new BallLoadout(BallBattle.Sim.Weapons.WeaponRegistry.Create("fang")) });
            for (var i = 0; i < 1500 && plain.Outcome == MatchOutcome.Ongoing; i++) { plain.Step(); treed.Step(); }
            Assert.That(treed.WinnerIndex, Is.EqualTo(plain.WinnerIndex), "ANGLER has no Run baseline: an empty tree plays the same fight");
            Assert.That(treed.Tick, Is.EqualTo(plain.Tick));
        }
    }
}
