using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Skills;

namespace BallBattle.View
{
    /// <summary>BB-12g: names and one-line effects of every ball's skill-tree nodes (English UI; the numbers match the Sim rules).</summary>
    public static class TreeText
    {
        /// <summary>Per ball: INSTINCT, OPENER, FINALE A, FINALE B names, then their effect lines (two lines each).</summary>
        sealed class Row
        {
            public string Instinct, Opener, FinA, FinB;
            public string[] InstinctFx, OpenerFx, FinAFx, FinBFx;
        }

        static readonly Dictionary<string, Row> Rows = new Dictionary<string, Row>
        {
            { "blade", new Row { Instinct = "KINDLING", Opener = "EMBERS", FinA = "REKINDLE", FinB = "FIRST BLOOD",
                InstinctFx = new[] { "DAMAGE GAINED PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "+0.2 DAMAGE", "AT THE START" },
                FinAFx = new[] { "AFTER A PARRY THE NEXT HIT", "GROWS +0.5. ONCE PER 6 S" }, FinBFx = new[] { "FIRST 2 HITS +0.25", "LATER GROWTH -5%" } } },
            { "fang", new Row { Instinct = "MOMENTUM", Opener = "SPIN UP", FinA = "AFTERSPIN", FinB = "BLOODLUST",
                InstinctFx = new[] { "SPIN GAIN FADES SLOWER", "0.92 TO 0.93 / 0.94" }, OpenerFx = new[] { "FIRST HIT", "+2 SPIN" },
                FinAFx = new[] { "2 S AFTER FRENZY: +50%", "SPIN GAIN. EVERY 12 S" }, FinBFx = new[] { "FIRST HIT IN FRENZY +10%", "AGAIN AFTER 3 S" } } },
            { "pike", new Row { Instinct = "MORE LINE", Opener = "FIRST CAST", FinA = "LONG CAST", FinB = "YANK",
                InstinctFx = new[] { "LENGTH AND DAMAGE PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "HOOK STARTS", "1 LONGER" },
                FinAFx = new[] { "IN LUNGE: +8% LENGTH", "-8% DAMAGE" }, FinBFx = new[] { "IN LUNGE: -8% LENGTH", "+8% DAMAGE" } } },
            { "brawler", new Row { Instinct = "HEADBUTT", Opener = "RUN-UP", FinA = "CAREFUL", FinB = "NO BRAKES",
                InstinctFx = new[] { "DAMAGE FACTOR PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "DAMAGE FACTOR", "+0.002 AT THE START" },
                FinAFx = new[] { "SPIKE WALLS HURT 15% LESS", "WALL BOOST -15%" }, FinBFx = new[] { "WALL BOOST +15%", "SPIKE WALLS HURT 15% MORE" } } },
            { "volley", new Row { Instinct = "FUSE", Opener = "OPENER", FinA = "FOCUS", FinB = "SCATTER",
                InstinctFx = new[] { "ARROW DAMAGE PER HIT", "+0.01 PER RANK" }, OpenerFx = new[] { "FIRST VOLLEY: 1 MORE", "ARROW AT 50%" },
                FinAFx = new[] { "1 ARROW FEWER", "SAME TOTAL DAMAGE" }, FinBFx = new[] { "1 ARROW MORE, MAX 9", "TOTAL DAMAGE +5%" } } },
            { "venom", new Row { Instinct = "SEEP", Opener = "SEED ROT", FinA = "OUTBREAK", FinB = "LINGER",
                InstinctFx = new[] { "POISON DAMAGE", "+3% PER RANK" }, OpenerFx = new[] { "FIRST 2 POISON HITS", "+1 STACK EACH" },
                FinAFx = new[] { "POISON +10% DAMAGE", "BUT SHORTER" }, FinBFx = new[] { "POISON -5% DAMAGE", "BUT LONGER" } } },
            { "aegis", new Row { Instinct = "GUARD UP", Opener = "RAISE", FinA = "BULLETPROOF", FinB = "PAYBACK",
                InstinctFx = new[] { "SHIELD WIDTH PER HIT", "+5% PER RANK" }, OpenerFx = new[] { "SHIELD STARTS", "0.25 WIDER" },
                FinAFx = new[] { "ABSORB +5% OF SHOTS", "REFLECTS -5%" }, FinBFx = new[] { "REFLECTS +10%", "ABSORB -5% OF SHOTS" } } },
            { "chorus", new Row { Instinct = "DEVOTION", Opener = "MORN LIGHT", FinA = "EVERLIGHT", FinB = "2X RAINBOW",
                InstinctFx = new[] { "COLOUR EFFECTS", "+6% PER RANK" }, OpenerFx = new[] { "THE FIGHT STARTS", "WITH PINK LIT" },
                FinAFx = new[] { "AFTER UNITY", "PINK STAYS LIT" }, FinBFx = new[] { "UNITY FIRES TWICE", "AT 75% EACH" } } },
            { "serpent", new Row { Instinct = "SCENT", Opener = "FIRST MEAL", FinA = "HYDRA", FinB = "SHED SKIN",
                InstinctFx = new[] { "HUNTS PREY HARDER", "+0.2 TURN PER RANK" }, OpenerFx = new[] { "START WITH", "2 TAIL SEGMENTS" },
                FinAFx = new[] { "EACH BITE HEALS", "0.2 PER SEGMENT" }, FinBFx = new[] { "WHEN CUT KEEP A", "QUARTER OF THE TAIL" } } },
            { "rig", new Row { Instinct = "WIND UP", Opener = "RESERVE", FinA = "HEAVY GUNS", FinB = "BULLET RAIN",
                InstinctFx = new[] { "TURRETS FIRE 1 TICK", "FASTER PER RANK" }, OpenerFx = new[] { "1 TURRET AT THE START", "GONE AFTER ITS VOLLEY" },
                FinAFx = new[] { "SLOWER VOLLEYS 10%", "SHOTS +5% DAMAGE" }, FinBFx = new[] { "FASTER VOLLEYS 10%", "SHOTS -13% DAMAGE" } } },
        };

        static Row RowOf(string weaponId) => weaponId != null && Rows.TryGetValue(weaponId, out var r) ? r : Rows["blade"];

        static string Branch(string weaponId, SkillBranch b) => SkillDefinition.ForWeapon(weaponId)?.BranchName(b) ?? (b == SkillBranch.A ? "A" : "B");

        /// <summary>Short name on the node box (at most 11 characters).</summary>
        public static string Name(string weaponId, TreeNode n, int skillLevel = 2)
        {
            var r = RowOf(weaponId);
            switch (n)
            {
                case TreeNode.Vitality: return "VITALITY";
                case TreeNode.Power: return "POWER";
                case TreeNode.Instinct: return r.Instinct;
                case TreeNode.Opener: return r.Opener;
                case TreeNode.SkillA: return Branch(weaponId, SkillBranch.A) + " LV" + skillLevel;
                case TreeNode.SkillB: return Branch(weaponId, SkillBranch.B) + " LV" + skillLevel;
                default: return "FINALE";
            }
        }

        public static string FinaleName(string weaponId, char branch) => branch == 'A' ? RowOf(weaponId).FinA : RowOf(weaponId).FinB;

        /// <summary>Two lines saying what a node does for this ball.</summary>
        public static string[] Effect(string weaponId, TreeNode n, char finaleBranch = 'A')
        {
            var r = RowOf(weaponId);
            var skill = SkillDefinition.ForWeapon(weaponId);
            switch (n)
            {
                case TreeNode.Vitality: return new[] { $"+{TreeTuning.HpPct(weaponId)}% MAX HP", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Power: return new[] { $"+{TreeTuning.DamagePct(weaponId)}% DAMAGE", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Instinct: return r.InstinctFx;
                case TreeNode.Opener: return r.OpenerFx;
                case TreeNode.SkillA: return new[] { $"{skill?.Name ?? "SKILL"} LV2 THEN LV3", skill?.SummaryA ?? "" };
                case TreeNode.SkillB: return new[] { $"{skill?.Name ?? "SKILL"} LV2 THEN LV3", skill?.SummaryB ?? "" };
                default: return finaleBranch == 'A' ? r.FinAFx : r.FinBFx;
            }
        }
    }
}
