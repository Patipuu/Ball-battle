using System;
using System.Collections.Generic;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    [Serializable]
    public sealed class TraitSlot
    {
        public string Id;
        public int Level = 1;

        public TraitSlot() { }
        public TraitSlot(string id, int level) { Id = id; Level = level; }
    }

    /// <summary>
    /// 21/10 (Rules 24) per-ball damage, in percent: the same in Run (player and enemies) and in PvP, so the two modes stay alike
    /// (PvP only drops traits and cards). Tuned on 1v1 PvP rounds (bot policy, LV1/5/10, both branches) and checked against the
    /// Run gate. Before (one-on-one round wins): HIVE 89%, SPORE 80%, EMBER 70%, MANTIS 68% ... WRECKER 7%, SPARKLER 27%, MIRROR 30%;
    /// after: all 42-52% except SPORE 75% (kept strong enough for Run, where its poison meets bigger HP pools). PvE skilled LV1 run
    /// wins per ball went from 0-78% (HIVE 78%) to 4-32%. Versus keeps its own profile and never reads this.
    /// </summary>
    public static class BallPower
    {
        /// <summary>Base max HP in percent of RunTuning.PlayerStartHp (the start of a run, an enemy before its stage bonus, a PvP ball).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, int> HpPercent =
            new System.Collections.Generic.Dictionary<string, int>
        {
            { "rig", 62 }, { "venom", 85 }, { "blade", 75 }, { "fang", 74 },          // too strong: lowered
            { "brawler", 135 }, { "volley", 116 }, { "aegis", 115 },              // too weak: raised
        };
        /// <summary>Damage in percent (every kind: weapon, shots, poison, reflects, skills).</summary>
        public static readonly System.Collections.Generic.Dictionary<string, int> DamagePercent =
            new System.Collections.Generic.Dictionary<string, int>
        {
            { "rig", 62 }, { "venom", 85 }, { "blade", 75 }, { "fang", 74 },          // too strong: lowered
            { "brawler", 135 }, { "volley", 116 }, { "aegis", 115 },              // too weak: raised
        };

        static int Get(System.Collections.Generic.Dictionary<string, int> table, string weaponId) =>
            weaponId != null && table.TryGetValue(weaponId, out var p) ? p : 100;

        public static int HpPercentFor(string weaponId) => Get(HpPercent, weaponId);
        public static int DamagePercentFor(string weaponId) => Get(DamagePercent, weaponId);

        /// <summary>The ball's base max HP (whole points).</summary>
        public static Fix BaseHp(string weaponId) => FixMath.RoundToEven(RunTuning.PlayerStartHp * Fix.Ratio(HpPercentFor(weaponId), 100));

        /// <summary>The damage bonus with the ball's power on top: (1 + bonus) * pct - 1.</summary>
        public static Fix Scale(string weaponId, Fix damagePct)
        {
            var pct = DamagePercentFor(weaponId);
            return pct == 100 ? damagePct : (Fix.One + damagePct) * Fix.Ratio(pct, 100) - Fix.One;
        }

        /// <summary>SWAP card: the new ball's base HP replaces the old one's, keeping everything earned on top in proportion.</summary>
        public static void Swap(RunBuild b, string newWeapon)
        {
            var from = HpPercentFor(b.WeaponId);
            var to = HpPercentFor(newWeapon);
            b.WeaponId = newWeapon;
            if (from == to) return;
            b.MaxHp = FixMath.Max(Fix.One, FixMath.RoundToEven(b.MaxHp * Fix.Ratio(to, from)));
            b.SetHp(b.Hp * Fix.Ratio(to, from));
        }
    }

    /// <summary>
    /// A ball described by ids and numbers (serializable, reusable). ToLoadout() makes fresh weapon/trait
    /// instances for one match. Used for the player (carried between fights) and for generated enemies.
    /// </summary>
    [Serializable]
    public sealed class RunBuild
    {
        public string WeaponId;
        public List<TraitSlot> Traits = new List<TraitSlot>();
        public Fix DamagePct;
        public Fix SpeedPct;
        public Fix MaxHp = RunTuning.PlayerStartHp;
        public Fix Hp = RunTuning.PlayerStartHp;
        /// <summary>&lt;= 0 = default ball size.</summary>
        public Fix Radius;
        /// <summary>BB-04: boss mechanic of a generated boss (Boss.BossIds); null for the player and normal enemies.</summary>
        public string BossId;
        public int SkillLevel = 1;
        public Skills.SkillBranch SkillBranch;
        /// <summary>BB-12g: the run's skill tree snapshot (player only). TreeOn = this build plays with a tree (a Run player since BB-12g,
        /// even with no point spent); the in-match nodes: INSTINCT rank, OPENER, FINALE (0 none, 1 A, 2 B).</summary>
        public bool TreeOn;
        public int TreeGrowth;
        public bool TreeStart;
        public int TreeFinale;

        /// <summary>HP is kept in whole points (what the screen shows), never below 1 while alive.</summary>
        public void SetHp(Fix hp) => Hp = FixMath.Max(Fix.One, FixMath.Min(MaxHp, FixMath.RoundToEven(hp)));

        /// <summary>Missing at least one whole HP point (so a heal card is worth offering).</summary>
        public bool Hurt => MaxHp - Hp >= Fix.One;

        public int TraitLevel(string id)
        {
            foreach (var t in Traits)
                if (t.Id == id) return t.Level;
            return 0;
        }

        public BallLoadout ToLoadout()
        {
            var l = new BallLoadout(!string.IsNullOrEmpty(BossId) && BossId != Boss.BossIds.Mimic
                ? (WeaponRule)new Boss.BossCoreRule(BossId) : WeaponRegistry.Create(WeaponId))
            {
                // BallPower: the ball's own damage (Run player, Run enemies and PvP alike; bosses keep theirs).
                Bonus = new StatBonus { DamagePct = string.IsNullOrEmpty(BossId) ? BallPower.Scale(WeaponId, DamagePct) : DamagePct, SpeedPct = SpeedPct },
                MaxHp = MaxHp,
                Hp = Hp,
                Radius = Radius,
                SkillLevel = SkillLevel,
                SkillBranch = SkillBranch
            };
            foreach (var t in Traits) l.Traits.Add(TraitRegistry.Create(t.Id, t.Level));
            if (!string.IsNullOrEmpty(BossId)) l.Boss = Boss.BossRegistry.Create(BossId);
            if (TreeOn) l.Tree = new TreeLoadout { Growth = TreeGrowth, Start = TreeStart, Finale = TreeFinale == 1 ? 'A' : TreeFinale == 2 ? 'B' : '-' };
            return l;
        }

        public RunBuild Clone()
        {
            var c = (RunBuild)MemberwiseClone();
            c.Traits = new List<TraitSlot>();
            foreach (var t in Traits) c.Traits.Add(new TraitSlot(t.Id, t.Level));
            return c;
        }
    }
}
