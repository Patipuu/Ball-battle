using System;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Run;

namespace BallBattle.View
{
    /// <summary>
    /// Save-file form of the run (6a.6b/c). The Sim's RunState / RunBuild / FightRecord hold Fix values, which JsonUtility
    /// cannot write; these View-side copies keep the exact JSON field names of save version 2 and store every converted field
    /// as double. Fix → double is exact (Q16.16 fits a double's 53-bit mantissa), so a save written by this build reloads
    /// to the same Fix values. A float written by an older v2 build loads as a double and is rounded once to the nearest
    /// Fix (<see cref="SaveFix.ToFix"/>). Converted fields: RunBuild.DamagePct, SpeedPct, MaxHp, Hp, Radius and
    /// FightRecord.HpAfter. Every other field (ints, bools, strings, lists, OfferSlot, TraitSlot) is copied as is.
    /// </summary>
    [Serializable]
    public sealed class RunSave
    {
        public int SeedBits;
        public int FightIndex;
        public int RetryCount;
        public bool IsRetry;
        public bool StartPickPending;
        public int Coins = RunTuning.StartCoins;
        public RunBuildSave Build = new RunBuildSave();
        public int RerollCount;
        public int PicksTaken;
        public int PickedMask;
        public List<OfferSlot> OfferCards = new List<OfferSlot>();
        public List<string> Weapons = new List<string>();
        public List<string> Traits = new List<string>();
        public List<string> EnemyWeapons = new List<string>();
        public List<string> EnemyTraits = new List<string>();
        public List<string> Achievements = new List<string>();
        public List<FightRecordSave> History = new List<FightRecordSave>();
        public bool FightLocked;
        public bool Over;
        public bool Won;
        public bool TutorialRun;
        /// <summary>BB-04 (save v3).</summary>
        public List<string> BossIds = new List<string>();
        public string ForcedBossId = "";
        public int ForcedBossFight = -1;
        /// <summary>PvE chapter and HEAT (0-based). Older saves load 0 / 0.</summary>
        public int Chapter;
        public int Heat;

        public static RunSave From(RunState r)
        {
            if (r == null) return new RunSave();
            var s = new RunSave
            {
                SeedBits = r.SeedBits, FightIndex = r.FightIndex, RetryCount = r.RetryCount, IsRetry = r.IsRetry,
                StartPickPending = r.StartPickPending, Coins = r.Coins, Build = RunBuildSave.From(r.Build),
                RerollCount = r.RerollCount, PicksTaken = r.PicksTaken, PickedMask = r.PickedMask,
                Weapons = new List<string>(r.Weapons), Traits = new List<string>(r.Traits),
                EnemyWeapons = new List<string>(r.EnemyWeapons), EnemyTraits = new List<string>(r.EnemyTraits),
                Achievements = new List<string>(r.Achievements),
                FightLocked = r.FightLocked, Over = r.Over, Won = r.Won, TutorialRun = r.TutorialRun,
                BossIds = new List<string>(r.BossIds), ForcedBossId = r.ForcedBossId ?? "", ForcedBossFight = r.ForcedBossFight,
                Chapter = r.Chapter, Heat = r.Heat
            };
            foreach (var o in r.OfferCards)
            {
                var kind = o.Kind;
                var level = o.Level;
                if (r.SkillOverride.HasValue && kind == CardKind.SkillUp)
                {
                    if (r.Build.SkillLevel == 3) kind = CardKind.MaxHp;
                    else level = r.Build.SkillLevel + 1;
                }
                s.OfferCards.Add(new OfferSlot { Kind = kind, Id = o.Id, Level = level });
            }
            foreach (var h in r.History) s.History.Add(FightRecordSave.From(h));
            return s;
        }

        public RunState ToRun()
        {
            var r = new RunState
            {
                SeedBits = SeedBits, FightIndex = FightIndex, RetryCount = RetryCount, IsRetry = IsRetry,
                StartPickPending = StartPickPending, Coins = Coins, Build = (Build ?? new RunBuildSave()).ToBuild(),
                RerollCount = RerollCount, PicksTaken = PicksTaken, PickedMask = PickedMask,
                Weapons = Copy(Weapons), Traits = Copy(Traits), EnemyWeapons = Copy(EnemyWeapons), EnemyTraits = Copy(EnemyTraits),
                Achievements = Copy(Achievements),
                FightLocked = FightLocked, Over = Over, Won = Won, TutorialRun = TutorialRun,
                BossIds = Copy(BossIds), ForcedBossId = ForcedBossId ?? "", ForcedBossFight = ForcedBossFight,
                Chapter = Chapter, Heat = Heat
            };
            if (OfferCards != null)
                foreach (var o in OfferCards) r.OfferCards.Add(new OfferSlot { Kind = o.Kind, Id = o.Id, Level = o.Level });
            if (History != null)
                foreach (var h in History) r.History.Add(h.ToRecord());
            return r;
        }

        static List<string> Copy(List<string> l) => l != null ? new List<string>(l) : new List<string>();
    }

    [Serializable]
    public sealed class RunBuildSave
    {
        public string WeaponId;
        public List<TraitSlot> Traits = new List<TraitSlot>();
        public double DamagePct;
        public double SpeedPct;
        public double MaxHp = SaveFix.ToDouble(RunTuning.PlayerStartHp);
        public double Hp = SaveFix.ToDouble(RunTuning.PlayerStartHp);
        public double Radius;
        public int SkillLevel = 1;
        public BallBattle.Sim.Skills.SkillBranch SkillBranch;
        /// <summary>BB-12g skill tree snapshot (absent in older saves: that run keeps playing without a tree).</summary>
        public bool TreeOn;
        public int TreeGrowth;
        public bool TreeStart;
        public int TreeFinale;

        public static RunBuildSave From(RunBuild b)
        {
            var s = new RunBuildSave
            {
                WeaponId = b.WeaponId,
                DamagePct = SaveFix.ToDouble(b.DamagePct), SpeedPct = SaveFix.ToDouble(b.SpeedPct),
                MaxHp = SaveFix.ToDouble(b.MaxHp), Hp = SaveFix.ToDouble(b.Hp), Radius = SaveFix.ToDouble(b.Radius),
                SkillLevel = b.SkillLevel, SkillBranch = b.SkillBranch,
                TreeOn = b.TreeOn, TreeGrowth = b.TreeGrowth, TreeStart = b.TreeStart, TreeFinale = b.TreeFinale
            };
            foreach (var t in b.Traits) s.Traits.Add(new TraitSlot(t.Id, t.Level));
            return s;
        }

        public RunBuild ToBuild()
        {
            var b = new RunBuild
            {
                WeaponId = WeaponId,
                DamagePct = SaveFix.ToFix(DamagePct), SpeedPct = SaveFix.ToFix(SpeedPct),
                MaxHp = SaveFix.ToFix(MaxHp), Hp = SaveFix.ToFix(Hp), Radius = SaveFix.ToFix(Radius),
                SkillLevel = SkillLevel, SkillBranch = SkillBranch,
                TreeOn = TreeOn, TreeGrowth = TreeGrowth, TreeStart = TreeStart, TreeFinale = TreeFinale
            };
            if (Traits != null)
                foreach (var t in Traits) b.Traits.Add(new TraitSlot(t.Id, t.Level));
            return b;
        }
    }

    [Serializable]
    public sealed class FightRecordSave
    {
        public int FightIndex;
        public bool Won;
        public bool Draw;
        public bool Retry;
        public string EnemyWeapon;
        public double HpAfter;
        /// <summary>BB-04 (save v3): boss fought, "" for a normal fight.</summary>
        public string BossId = "";

        public static FightRecordSave From(FightRecord f) => new FightRecordSave
        {
            FightIndex = f.FightIndex, Won = f.Won, Draw = f.Draw, Retry = f.Retry, EnemyWeapon = f.EnemyWeapon, HpAfter = SaveFix.ToDouble(f.HpAfter),
            BossId = f.BossId ?? ""
        };

        public FightRecord ToRecord() => new FightRecord
        {
            FightIndex = FightIndex, Won = Won, Draw = Draw, Retry = Retry, EnemyWeapon = EnemyWeapon, HpAfter = SaveFix.ToFix(HpAfter),
            BossId = BossId ?? ""
        };
    }

    /// <summary>The only double ↔ Fix conversions of the save (View side; the Sim never sees a double).</summary>
    public static class SaveFix
    {
        /// <summary>Exact: every Q16.16 value is a double.</summary>
        public static double ToDouble(Fix f) => f.Raw / (double)Fix.OneRaw;

        /// <summary>
        /// Nearest Q16.16, ties away from zero (v × 65536 rounded with MidpointRounding.AwayFromZero). Values outside the Fix
        /// range or NaN (a corrupted save) become 0, and RunValidation then rejects the run if that matters (MaxHp / Hp &gt; 0).
        /// </summary>
        public static Fix ToFix(double v)
        {
            var scaled = Math.Round(v * Fix.OneRaw, MidpointRounding.AwayFromZero);
            if (double.IsNaN(scaled) || scaled > int.MaxValue || scaled < int.MinValue) return Fix.Zero;
            return Fix.FromRaw((int)scaled);
        }
    }
}
