using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Daily quests (replaces the old NEXT GOAL ladder; balls no longer unlock through progress). Every local day the game rolls
    /// <see cref="DailyQuests.PerDay"/> quests from <see cref="DailyQuests.Pool"/>: one easy, one easy-or-medium, one medium-or-hard.
    /// Each finished quest drops its chest into ProgressData.PendingChests (opened from the menu like any waiting chest); finishing
    /// all of them adds a bonus chest. Progress only moves forward and resets with the day. Pure C#: the caller passes the day number.
    /// </summary>
    [Serializable]
    public sealed class DailyQuestState
    {
        /// <summary>Local day number (days since 2000-01-01) the quests belong to; -1 = never rolled.</summary>
        public int Day = -1;
        public List<QuestSlot> Slots = new List<QuestSlot>();
        public bool BonusGiven;
    }

    [Serializable]
    public sealed class QuestSlot
    {
        /// <summary>Quest id from <see cref="DailyQuests.Pool"/>.</summary>
        public string Id;
        /// <summary>Ball named by a "win with X" quest ("" otherwise).</summary>
        public string Ball = "";
        public int Progress;
        public bool Done;
    }

    public enum QuestKind
    {
        /// <summary>Win fights (any run, any ball).</summary>
        WinFights,
        /// <summary>Finish runs (won or lost).</summary>
        FinishRuns,
        /// <summary>Beat a boss.</summary>
        BeatBoss,
        /// <summary>Win this many fights inside one run.</summary>
        WinsInOneRun,
        /// <summary>Win this many fights in a row inside one run.</summary>
        WinStreak,
        /// <summary>Win fights with the slot's ball.</summary>
        WinWithBall,
        /// <summary>Win a whole run.</summary>
        WinRun,
    }

    public enum QuestTier { Easy, Medium, Hard }

    public sealed class QuestDef
    {
        public readonly string Id;
        public readonly QuestKind Kind;
        public readonly int Target;
        public readonly QuestTier Tier;
        public readonly ChestTier Reward;

        public QuestDef(string id, QuestKind kind, int target, QuestTier tier, ChestTier reward)
        {
            Id = id; Kind = kind; Target = target; Tier = tier; Reward = reward;
        }
    }

    public static class DailyQuests
    {
        public const int PerDay = 3;
        /// <summary>Chest for finishing every quest of the day.</summary>
        public const ChestTier BonusChest = ChestTier.Silver;
        /// <summary>Prefix of the RunState.Achievements entries that record a quest finished during the run ("quest:win3").</summary>
        public const string QuestMark = "quest:";
        public const string BonusId = "bonus";

        public static readonly QuestDef[] Pool =
        {
            new QuestDef("win3", QuestKind.WinFights, 3, QuestTier.Easy, ChestTier.Wood),
            new QuestDef("runs1", QuestKind.FinishRuns, 1, QuestTier.Easy, ChestTier.Wood),
            new QuestDef("ball2", QuestKind.WinWithBall, 2, QuestTier.Easy, ChestTier.Wood),
            new QuestDef("streak3", QuestKind.WinStreak, 3, QuestTier.Easy, ChestTier.Wood),
            new QuestDef("win6", QuestKind.WinFights, 6, QuestTier.Medium, ChestTier.Silver),
            new QuestDef("runs2", QuestKind.FinishRuns, 2, QuestTier.Medium, ChestTier.Silver),
            new QuestDef("boss1", QuestKind.BeatBoss, 1, QuestTier.Medium, ChestTier.Silver),
            new QuestDef("onerun5", QuestKind.WinsInOneRun, 5, QuestTier.Medium, ChestTier.Silver),
            new QuestDef("ball4", QuestKind.WinWithBall, 4, QuestTier.Hard, ChestTier.Silver),
            new QuestDef("boss2", QuestKind.BeatBoss, 2, QuestTier.Hard, ChestTier.Silver),
            new QuestDef("winrun", QuestKind.WinRun, 1, QuestTier.Hard, ChestTier.Gold),
        };

        public static QuestDef Def(string id)
        {
            foreach (var d in Pool) if (d.Id == id) return d;
            return null;
        }

        /// <summary>Local day number used by the save: days since 2000-01-01.</summary>
        public static int DayOf(DateTime local) => (int)(local.Date - new DateTime(2000, 1, 1)).TotalDays;

        /// <summary>The day's quests (rolled now if the saved ones are from another day or broken).</summary>
        public static DailyQuestState Today(ProgressData p, int day)
        {
            if (p.Daily == null) p.Daily = new DailyQuestState();
            var d = p.Daily;
            if (d.Day == day && Valid(d)) return d;
            d.Day = day;
            d.BonusGiven = false;
            d.Slots = Roll(day);
            return d;
        }

        static bool Valid(DailyQuestState d)
        {
            if (d.Slots == null || d.Slots.Count != PerDay) return false;
            foreach (var s in d.Slots)
            {
                var def = Def(s?.Id);
                if (def == null) return false;
                if (def.Kind == QuestKind.WinWithBall && !BallExists(s.Ball)) return false;
            }
            return true;
        }

        /// <summary>Same day = same quests (seeded by the day number).</summary>
        public static List<QuestSlot> Roll(int day)
        {
            var rng = new SimRandom(unchecked((uint)day * 2654435761u ^ 0xDA11u));
            var picked = new List<QuestDef>();
            var rules = new[] { new[] { QuestTier.Easy }, new[] { QuestTier.Easy, QuestTier.Medium }, new[] { QuestTier.Medium, QuestTier.Hard } };
            foreach (var allowed in rules)
            {
                var options = new List<QuestDef>();
                foreach (var q in Pool)
                    if (Array.IndexOf(allowed, q.Tier) >= 0 && !picked.Contains(q) && !SameKind(picked, q)) options.Add(q);
                picked.Add(options[rng.RangeInt(0, options.Count)]);
            }
            var slots = new List<QuestSlot>();
            foreach (var q in picked)
            {
                var slot = new QuestSlot { Id = q.Id };
                if (q.Kind == QuestKind.WinWithBall)
                {
                    var all = WeaponRegistry.All;
                    slot.Ball = all[rng.RangeInt(0, all.Count)].Id;
                }
                slots.Add(slot);
            }
            return slots;
        }

        static bool SameKind(List<QuestDef> picked, QuestDef q)
        {
            foreach (var p in picked) if (p.Kind == q.Kind) return true;
            return false;
        }

        static bool BallExists(string id)
        {
            foreach (var w in WeaponRegistry.All) if (w.Id == id) return true;
            return false;
        }

        /// <summary>
        /// Call once after every RunState.ApplyResult (same place as UnlockRules.Record). Moves the day's quests on, drops the chest of
        /// each quest finished now (and the bonus) into PendingChests, and returns the quests finished now (the view can announce them).
        /// </summary>
        public static List<QuestSlot> Record(ProgressData p, RunState run, int day)
        {
            var finished = new List<QuestSlot>();
            if (p == null || run == null || run.History.Count == 0) return finished;
            var d = Today(p, day);
            var last = run.History[run.History.Count - 1];
            var winsInRun = 0;
            foreach (var f in run.History) if (f.Won) winsInRun++;
            var streak = 0;
            for (var i = run.History.Count - 1; i >= 0 && run.History[i].Won; i--) streak++;
            var ball = run.Build?.WeaponId ?? "";

            foreach (var s in d.Slots)
            {
                if (s.Done) continue;
                var def = Def(s.Id);
                switch (def.Kind)
                {
                    case QuestKind.WinFights: if (last.Won) s.Progress++; break;
                    case QuestKind.FinishRuns: if (run.Over) s.Progress++; break;
                    case QuestKind.BeatBoss: if (last.Won && !string.IsNullOrEmpty(last.BossId)) s.Progress++; break;
                    case QuestKind.WinsInOneRun: s.Progress = Math.Max(s.Progress, winsInRun); break;
                    case QuestKind.WinStreak: s.Progress = Math.Max(s.Progress, streak); break;
                    case QuestKind.WinWithBall: if (last.Won && ball == s.Ball) s.Progress++; break;
                    case QuestKind.WinRun: if (run.Over && run.Won) s.Progress++; break;
                }
                if (s.Progress >= def.Target)
                {
                    s.Progress = def.Target;
                    s.Done = true;
                    Grant(p, def.Reward);
                    finished.Add(s);
                    run.Achievements.Add(QuestMark + s.Id);   // the run summary lists the quests this run finished
                }
            }
            if (!d.BonusGiven && AllDone(d))
            {
                d.BonusGiven = true;
                Grant(p, BonusChest);
                run.Achievements.Add(QuestMark + BonusId);
            }
            return finished;
        }

        public static bool AllDone(DailyQuestState d)
        {
            if (d?.Slots == null || d.Slots.Count == 0) return false;
            foreach (var s in d.Slots) if (!s.Done) return false;
            return true;
        }

        static void Grant(ProgressData p, ChestTier tier)
        {
            if (p.PendingChests == null) p.PendingChests = new List<ChestTier>();
            p.PendingChests.Add(tier);
        }
    }
}
