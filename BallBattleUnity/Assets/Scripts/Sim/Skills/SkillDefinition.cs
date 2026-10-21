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
            Id = weapon + "_" + IdKey(weapon, name); AudioId = "skill_" + Id;
        }
        /// <summary>BB-19b: the display names changed with the tapped skills; ids (and audio ids) keep the original key.</summary>
        static string IdKey(string weapon, string name)
        {
            switch (weapon)
            {
                case "blade": return "whetstone";
                case "fang": return "frenzy";
                case "pike": return "lunge";
                case "brawler": return "charge";
                case "volley": return "barrage";
                case "venom": return "drain";
                case "aegis": return "bulwark";
                case "rig": return "overclock";
                case "chorus": return "hymn";
                case "serpent": return "strike";
            }
            return name.ToLowerInvariant();
        }
        public string BranchName(SkillBranch branch) => branch == SkillBranch.A ? BranchA : branch == SkillBranch.B ? BranchB : "";
        public static readonly SkillDefinition[] All = {
            new SkillDefinition("blade", "FIRE TRAIL", "BRUISER", 4,2,2,1, "LONG TRAIL", "WILDFIRE", "DASH, LEAVING A TRAIL OF FIRE", "LONG TRAIL: BURNS LONGER", "WILDFIRE: HOTTER, SHORTER"),
            new SkillDefinition("fang", "PHANTOM", "DUELIST", 3,1,2,3, "AFTERIMAGE", "EXECUTE", "BLINK BEHIND FOE, SLASH, DODGE", "AFTERIMAGE: THE DECOY HITS TOO", "EXECUTE: DOUBLE UNDER 30% HP"),
            new SkillDefinition("pike", "HOOK", "LANCER", 3,5,2,2, "LONG LINE", "BARB", "HOOK THE FOE, REEL IT IN, STUN", "LONG LINE: FASTER, BIGGER HOOK", "BARB: LONGER STUN, HARDER PULL"),
            new SkillDefinition("brawler", "RAM", "RAMMER", 4,1,3,3, "PATIENCE", "HAIR TRIGGER", "TAP TO WIND UP, TAP TO RAM", "PATIENCE: LONGER WIND UP", "HAIR TRIGGER: FAST, WEAKER"),
            new SkillDefinition("volley", "RICOCHET", "RANGER", 2,5,1,2, "MORE BOUNCES", "HEAVY SHOT", "A BOLT THAT BOUNCES OFF WALLS", "MORE BOUNCES: 2 EXTRA", "HEAVY SHOT: BIGGER, 2 BOUNCES"),
            new SkillDefinition("venom", "SPORE CLOUD", "POISONER", 3,2,2,3, "GROWTH", "BLIGHT", "CLOUD SLOWS; HEALS YOU", "GROWTH: BIGGER, LASTS LONGER", "BLIGHT: STRONGER POISON, SMALLER"),
            new SkillDefinition("aegis", "PARRY", "GUARDIAN", 1,2,5,4, "WIDE GLASS", "SHARD", "TIMED PARRY THROWS THE BLOW BACK", "WIDE GLASS: LONGER WINDOW", "SHARD: SHORT WINDOW, HARDER HIT"),
            new SkillDefinition("rig", "SWARM", "BUILDER", 2,4,2,4, "SWARM", "STINGERS", "DRONES HUNT AND STING THE FOE", "SWARM: MORE DRONES", "STINGERS: FEWER, HARDER STINGS"),
            new SkillDefinition("chorus", "UNITY", "HARMONIST", 3,3,3,3, "CHORUS", "RADIANCE", "TAP TO BURST THE LIT ORBS", "CHORUS: UNITY HEALS", "RADIANCE: BIGGER UNITY"),
            new SkillDefinition("serpent", "COIL", "HUNTER", 3,2,2,4, "CONSTRICT", "SWALLOW", "LUNGE AND COIL THE FOE, 3 BITES", "CONSTRICT: HOLDS LONGER, SLOWS", "SWALLOW: MORE BITES, HEALS")
        };
        public static SkillDefinition ForWeapon(string id)
        {
            foreach (var skill in All) if (skill.WeaponId == id) return skill;
            return null; // test-only weapons have no authored signature
        }
    }
}
