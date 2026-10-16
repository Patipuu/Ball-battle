using System;

namespace BallBattle.Sim.Skills
{
    public enum SkillBranch : byte { None, A, B }

    public readonly struct SkillSpec
    {
        public readonly int Level;
        public readonly SkillBranch Branch;
        public SkillSpec(int level, SkillBranch branch = SkillBranch.None)
        {
            if (!Valid(level, branch)) throw new ArgumentException("Skill level 1 has no branch; levels 2-3 require A or B");
            Level = level; Branch = branch;
        }
        public static bool Valid(int level, SkillBranch branch) => level == 1 ? branch == SkillBranch.None :
            (level == 2 || level == 3) && (branch == SkillBranch.A || branch == SkillBranch.B);
    }

    /// <summary>BB-10a authored data. Fractions are Q16.16; all clocks count ActiveTicks.</summary>
    public sealed class SkillDefinition
    {
        public readonly string WeaponId, Name, Role, BranchA, BranchB, Summary, SummaryA, SummaryB;
        public readonly int Damage, Range, Defense, Difficulty;
        public readonly string Id, AudioId;
        SkillDefinition(string weapon, string name, string role, int damage, int range, int defense, int difficulty,
            string branchA, string branchB, string summary, string summaryA, string summaryB)
        {
            WeaponId = weapon; Name = name; Role = role; Damage = damage; Range = range; Defense = defense; Difficulty = difficulty;
            BranchA = branchA; BranchB = branchB; Summary = summary; SummaryA = summaryA; SummaryB = summaryB;
            Id = weapon + "_" + name.ToLowerInvariant(); AudioId = "skill_" + Id;
        }
        public string BranchName(SkillBranch branch) => branch == SkillBranch.A ? BranchA : branch == SkillBranch.B ? BranchB : "";
        public static readonly SkillDefinition[] All = {
            new SkillDefinition("blade", "WHETSTONE", "BRUISER", 4,2,2,1, "EDGE", "HONE", "NEXT 3 HITS +0.5 DMG", "EDGE: MORE HITS", "HONE: MORE DAMAGE"),
            new SkillDefinition("fang", "FRENZY", "DUELIST", 3,1,2,3, "ENDURE", "FURY", "HIGH SPIN: NO DECAY", "ENDURE: LONGER FRENZY", "FURY: MORE SPIN GAIN"),
            new SkillDefinition("pike", "LUNGE", "LANCER", 3,5,2,2, "REACH", "HOLD", "TEMPORARY EXTRA REACH", "REACH: LONGER LUNGE", "HOLD: LONGER DURATION"),
            new SkillDefinition("brawler", "CHARGE", "RAMMER", 4,1,3,3, "SLAM", "RUSH", "MAX SPEED. FIRST HIT X1.5", "SLAM: STRONGER FIRST HIT", "RUSH: LONGER CHARGE"),
            new SkillDefinition("volley", "BARRAGE", "RANGER", 2,5,1,2, "RAPID", "HEAVY", "FIRE AN EXTRA VOLLEY", "RAPID: LOWER COOLDOWN", "HEAVY: STRONGER ARROWS"),
            new SkillDefinition("venom", "DRAIN", "POISONER", 3,2,2,3, "SIP", "ROT", "3 POISON: DRAIN HALF DMG", "SIP: MORE POISON HEAL", "ROT: LONGER POISON"),
            new SkillDefinition("aegis", "BULWARK", "GUARDIAN", 1,2,5,4, "WALL", "SPITE", "TEMPORARY EXTRA REFLECT", "WALL: LONGER BULWARK", "SPITE: MORE PARRY REFLECT"),
            new SkillDefinition("rig", "OVERCLOCK", "BUILDER", 2,4,2,4, "BURST", "DEPLOY", "2 TURRET VOLLEYS FASTER", "BURST: MORE FAST VOLLEYS", "DEPLOY: ADD TURRETS"),
            new SkillDefinition("chorus", "HYMN", "HARMONIST", 3,3,3,3, "CHORUS", "RADIANCE", "LIGHT THE NEXT 2 COLOURS", "CHORUS: UNITY HEALS", "RADIANCE: BIGGER UNITY"),
            new SkillDefinition("serpent", "STRIKE", "HUNTER", 3,2,2,4, "CONSTRICT", "SWALLOW", "LUNGE AT THE FOE. BITE +50%", "CONSTRICT: TAIL SLOWS", "SWALLOW: EAT ON THE WAY")
        };
        public static SkillDefinition ForWeapon(string id)
        {
            foreach (var skill in All) if (skill.WeaponId == id) return skill;
            return null; // test-only weapons have no authored signature
        }
    }
}
