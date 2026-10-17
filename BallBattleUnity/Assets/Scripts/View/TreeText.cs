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
            { "blade", new Row { Instinct = "KINDLING", Opener = "EMBERS", FinA = "SMOLDER", FinB = "BACKDRAFT",
                InstinctFx = new[] { "DAMAGE GAINED PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "+0.2 DAMAGE", "AT THE START" },
                FinAFx = new[] { "BURN LASTS 2 S AFTER", "LEAVING THE TRAIL" }, FinBFx = new[] { "HITS ON A BURNING FOE +25%", "TRAIL 15% SHORTER" } } },
            { "fang", new Row { Instinct = "MOMENTUM", Opener = "SPIN UP", FinA = "AFTERSPIN", FinB = "BLOODLUST",
                InstinctFx = new[] { "SPIN GAIN FADES SLOWER", "0.92 TO 0.93 / 0.94" }, OpenerFx = new[] { "FIRST HIT", "+2 SPIN" },
                FinAFx = new[] { "AFTER A BLINK THE NEXT 3", "HITS +50% SPIN. EVERY 12 S" }, FinBFx = new[] { "PHANTOM SLASH +20% DAMAGE", "COOLDOWN +10%" } } },
            { "pike", new Row { Instinct = "MORE LINE", Opener = "FIRST CAST", FinA = "LONG CAST", FinB = "YANK",
                InstinctFx = new[] { "LENGTH AND DAMAGE PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "PIKE STARTS", "1 LONGER" },
                FinAFx = new[] { "HOOK FLIES 12% FARTHER", "STUN 10% SHORTER" }, FinBFx = new[] { "PULL +20%, HOOK DAMAGE +25%", "RANGE -10%" } } },
            { "brawler", new Row { Instinct = "HEADBUTT", Opener = "RUN-UP", FinA = "STEADY", FinB = "OVERCHARGE",
                InstinctFx = new[] { "DAMAGE FACTOR PER HIT", "+2% PER RANK" }, OpenerFx = new[] { "DAMAGE FACTOR", "+0.002 AT THE START" },
                FinAFx = new[] { "BACKFIRE STUN HALVED", "HOLD TOO LONG HURTS LESS" }, FinBFx = new[] { "HELD PAST FULL: RAM +30%", "BACKFIRE STUN +50%" } } },
            { "volley", new Row { Instinct = "FUSE", Opener = "OPENER", FinA = "SPLIT", FinB = "CHAIN",
                InstinctFx = new[] { "ARROW DAMAGE PER HIT", "+0.01 PER RANK" }, OpenerFx = new[] { "FIRST VOLLEY: 1 MORE", "ARROW AT 50%" },
                FinAFx = new[] { "LAST BOUNCE SPLITS INTO", "2 SPARKS AT 40% DAMAGE" }, FinBFx = new[] { "EACH HIT ADDS A BOUNCE", "UP TO 4 HITS, 20% SLOWER" } } },
            { "venom", new Row { Instinct = "SEEP", Opener = "SEED ROT", FinA = "OUTBREAK", FinB = "LINGER",
                InstinctFx = new[] { "POISON AND CLOUD DAMAGE", "+3% PER RANK" }, OpenerFx = new[] { "FIRST 2 POISON HITS", "+1 STACK EACH" },
                FinAFx = new[] { "WHEN THE CLOUD ENDS A", "SMALL CLOUD STAYS BEHIND" }, FinBFx = new[] { "POISON LASTS 50% LONGER", "AFTER THE FOE LEAVES" } } },
            { "aegis", new Row { Instinct = "GUARD UP", Opener = "RAISE", FinA = "RIPOSTE", FinB = "FLOW",
                InstinctFx = new[] { "SHIELD WIDTH PER HIT", "+5% PER RANK" }, OpenerFx = new[] { "SHIELD STARTS", "0.25 WIDER" },
                FinAFx = new[] { "A PARRY THROWS THE BLOW", "BACK +50% HARDER" }, FinBFx = new[] { "A PARRY REFUNDS 60% OF", "COOLDOWN. A MISS COSTS +25%" } } },
            { "chorus", new Row { Instinct = "DEVOTION", Opener = "MORN LIGHT", FinA = "EVERLIGHT", FinB = "2X RAINBOW",
                InstinctFx = new[] { "COLOUR EFFECTS", "+6% PER RANK" }, OpenerFx = new[] { "THE FIGHT STARTS", "WITH PINK LIT" },
                FinAFx = new[] { "AFTER UNITY", "PINK STAYS LIT" }, FinBFx = new[] { "UNITY FIRES TWICE", "AT 75% EACH" } } },
            { "serpent", new Row { Instinct = "SCENT", Opener = "FIRST MEAL", FinA = "HYDRA", FinB = "SHED SKIN",
                InstinctFx = new[] { "HUNTS PREY HARDER", "+0.2 TURN PER RANK" }, OpenerFx = new[] { "START WITH", "2 TAIL SEGMENTS" },
                FinAFx = new[] { "EACH BITE HEALS", "0.2 PER SEGMENT" }, FinBFx = new[] { "AFTER THE COIL, THE NEXT", "HIT WITHIN 1 S IS BLOCKED" } } },
            { "rig", new Row { Instinct = "WIND UP", Opener = "RESERVE", FinA = "QUEEN", FinB = "STING STORM",
                InstinctFx = new[] { "TURRETS FIRE 1 TICK", "FASTER PER RANK" }, OpenerFx = new[] { "1 TURRET AT THE START", "GONE AFTER ITS VOLLEY" },
                FinAFx = new[] { "ONE DRONE GUARDS: BLOCKS", "1 HIT WHILE SWARM IS OUT" }, FinBFx = new[] { "+2 DRONES IN THE SWARM", "EACH 25% WEAKER" } } },
        };

        /// <summary>BB-19c: what each skill node (level 2 / level 3 of branch A or B) gives, two lines; the level 3 line says what it becomes.</summary>
        static readonly Dictionary<string, string[]> SkillFx = new Dictionary<string, string[]>
        {
            { "blade:A2", new[] { "LV2: TRAIL BURNS 50% LONGER", "DASH GOES 25% FURTHER" } },
            { "blade:A3", new[] { "LV3: TRAIL BURNS 75% LONGER", "DASH GOES 25% FURTHER" } },
            { "blade:B2", new[] { "LV2: TRAIL HITS +70%", "SHORTER, NARROWER TRAIL" } },
            { "blade:B3", new[] { "LV3: TRAIL HITS +100%", "SHORTER, NARROWER TRAIL" } },
            { "fang:A2", new[] { "LV2: DECOY HITS FOR 60%", "DODGE WINDOW +50%" } },
            { "fang:A3", new[] { "LV3: DECOY HITS FOR 90%", "DODGE WINDOW +75%" } },
            { "fang:B2", new[] { "LV2: SLASH DOUBLES UNDER 30% HP", "" } },
            { "fang:B3", new[] { "LV3: SLASH DOUBLES UNDER 45% HP", "" } },
            { "pike:A2", new[] { "LV2: HOOK +30% SPEED AND RANGE", "HOOK 20% BIGGER" } },
            { "pike:A3", new[] { "LV3: HOOK +45% SPEED AND RANGE", "HOOK 30% BIGGER" } },
            { "pike:B2", new[] { "LV2: HOOK HITS +50%, STUN +60%", "PULL +2" } },
            { "pike:B3", new[] { "LV3: HOOK HITS +75%, STUN +90%", "PULL +3" } },
            { "brawler:A2", new[] { "LV2: HOLD 25% LONGER BEFORE", "THE BACKFIRE" } },
            { "brawler:A3", new[] { "LV3: HOLD 50% LONGER BEFORE", "THE BACKFIRE" } },
            { "brawler:B2", new[] { "LV2: FULL CHARGE IN HALF THE TIME", "RAM CAPPED AT 75%" } },
            { "brawler:B3", new[] { "LV3: FULL CHARGE IN HALF THE TIME", "RAM CAPPED AT 88%" } },
            { "volley:A2", new[] { "LV2: BOLT BOUNCES 2 MORE TIMES", "" } },
            { "volley:A3", new[] { "LV3: BOLT BOUNCES 3 MORE TIMES", "" } },
            { "volley:B2", new[] { "LV2: BOLT HITS +90%", "BIGGER BOLT, 2 BOUNCES" } },
            { "volley:B3", new[] { "LV3: BOLT HITS +125%", "BIGGER BOLT, 2 BOUNCES" } },
            { "venom:A2", new[] { "LV2: CLOUD 35% WIDER", "LASTS 30% LONGER" } },
            { "venom:A3", new[] { "LV3: CLOUD 55% WIDER", "LASTS 50% LONGER" } },
            { "venom:B2", new[] { "LV2: CLOUD POISON +80%", "SMALLER CLOUD, LESS HEAL" } },
            { "venom:B3", new[] { "LV3: CLOUD POISON +120%", "SMALLER CLOUD, LESS HEAL" } },
            { "aegis:A2", new[] { "LV2: PARRY WINDOW 67% LONGER", "" } },
            { "aegis:A3", new[] { "LV3: PARRY WINDOW 100% LONGER", "" } },
            { "aegis:B2", new[] { "LV2: WINDOW 42% SHORTER", "REFLECT +50%, STUN +33%" } },
            { "aegis:B3", new[] { "LV3: WINDOW 42% SHORTER", "REFLECT +75%, STUN +50%" } },
            { "rig:A2", new[] { "LV2: 9 DRONES, EACH STING -30%", "" } },
            { "rig:A3", new[] { "LV3: 10 DRONES, EACH STING -30%", "" } },
            { "rig:B2", new[] { "LV2: 4 DRONES, STING +90%", "DRONES LIVE 30% LONGER" } },
            { "rig:B3", new[] { "LV3: 4 DRONES, STING +125%", "DRONES LIVE 30% LONGER" } },
            { "chorus:A2", new[] { "LV2: UNITY HEALS 1% MAX HP", "" } },
            { "chorus:A3", new[] { "LV3: UNITY HEALS 1.5% MAX HP", "" } },
            { "chorus:B2", new[] { "LV2: UNITY DAMAGE +60%", "WIDER BLAST" } },
            { "chorus:B3", new[] { "LV3: UNITY DAMAGE +90%", "WIDER BLAST" } },
            { "serpent:A2", new[] { "LV2: COIL HOLDS 50% LONGER", "THEN THE FOE IS SLOWED" } },
            { "serpent:A3", new[] { "LV3: COIL HOLDS 80% LONGER", "THEN THE FOE IS SLOWED" } },
            { "serpent:B2", new[] { "LV2: 4 BITES", "EACH BITE HEALS 30% OF IT" } },
            { "serpent:B3", new[] { "LV3: 5 BITES", "EACH BITE HEALS 45% OF IT" } },
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
        public static string[] Effect(string weaponId, TreeNode n, char finaleBranch = 'A', int skillLevel = 2)
        {
            var r = RowOf(weaponId);
            var skill = SkillDefinition.ForWeapon(weaponId);
            switch (n)
            {
                case TreeNode.Vitality: return new[] { $"+{TreeTuning.HpPct(weaponId)}% MAX HP", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Power: return new[] { $"+{TreeTuning.DamagePct(weaponId)}% DAMAGE", "PER RANK. MAX 2 RANKS" };
                case TreeNode.Instinct: return r.InstinctFx;
                case TreeNode.Opener: return r.OpenerFx;
                case TreeNode.SkillA:
                    return SkillFx.TryGetValue(weaponId + ":A" + skillLevel, out var fa) ? fa : new[] { $"{skill?.Name ?? "SKILL"} LV{skillLevel}", skill?.SummaryA ?? "" };
                case TreeNode.SkillB:
                    return SkillFx.TryGetValue(weaponId + ":B" + skillLevel, out var fb) ? fb : new[] { $"{skill?.Name ?? "SKILL"} LV{skillLevel}", skill?.SummaryB ?? "" };
                default: return finaleBranch == 'A' ? r.FinAFx : r.FinBFx;
            }
        }
    }
}
