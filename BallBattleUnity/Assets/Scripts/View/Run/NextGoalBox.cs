using System;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// The quest panel (hub, Run title, Run summary), fixed 254x60 so it never moves the screen around it. Since the daily-quest
    /// update it shows DAILY QUESTS instead of the old NEXT GOAL ladder: a header ("DAILY QUESTS" left, time to the next refresh or
    /// "BONUS CHEST!" right) and one 14 px row per quest: a small chest in the reward's colour, the quest, and "2/3" or "DONE".
    /// Placed by its top-left corner in 270x480 screen pixels (origin top-left). The class keeps its old name and members so the
    /// screens and tools that place it are unchanged.
    /// </summary>
    public sealed class NextGoalBox : MonoBehaviour
    {
        public const int Width = 254;
        public const int Height = 60;
        const int Pad = 6;
        const int RowTop = 16, RowPitch = 14;
        const int ChestW = 9, ChestH = 7;

        public static readonly Color32 Green = new Color32(110, 220, 110, 255);

        PixelText header, refresh;
        readonly PixelText[] rowText = new PixelText[DailyQuests.PerDay], rowCount = new PixelText[DailyQuests.PerDay];
        readonly SpriteRenderer[] chestBody = new SpriteRenderer[DailyQuests.PerDay], chestLid = new SpriteRenderer[DailyQuests.PerDay],
            chestLock = new SpriteRenderer[DailyQuests.PerDay];
        float left, top;

        /// <summary>Old NEXT GOAL data for the same progress (kept for tools and tests; no longer drawn).</summary>
        public NextGoalInfo Info { get; private set; }
        /// <summary>The day's quests as drawn by the last Show.</summary>
        public DailyQuestState Quests { get; private set; }
        /// <summary>Header text ("DAILY QUESTS").</summary>
        public PixelText TitleText => header;
        /// <summary>Right side of the header (refresh time or bonus).</summary>
        public PixelText SubText => refresh;
        /// <summary>Count of the first quest row ("1/3" or "DONE").</summary>
        public PixelText CountText => rowCount[0];
        public PixelText RowText(int i) => rowText[i];
        public PixelText RowCount(int i) => rowCount[i];
        public float BarFillWidth => 0f;

        /// <summary>Screen (top-left origin) to the parent's local space (centre origin, y up).</summary>
        public static float LocalX(float screenX) => screenX - TextWrap.ScreenWidth / 2f;
        public static float LocalTop(float screenY) => TextWrap.ScreenHeight / 2f - screenY;

        /// <summary>Clock used for the day of the quests (tests replace it).</summary>
        public static Func<DateTime> Now = () => DateTime.Now;
        public static int Today => DailyQuests.DayOf(Now());

        /// <summary>
        /// BB-VIEW3: boss behind a boss goal (boss1 = fight 4, win1 = fight 8), or null. Kept for older callers (the goal ladder is gone).
        /// </summary>
        public static string GoalBoss(string goalId, ProgressData progress, RunState run)
        {
            var slot = goalId == UnlockRules.Boss1 ? 0 : goalId == UnlockRules.Win1 ? 1 : -1;
            if (slot < 0) return null;
            if (run != null && !run.Over) return run.BossFor(RunTuning.BossFights[slot]);
            var pool = BallBattle.Sim.Boss.BossIds.Slots[slot];
            if (BossSelection.IsFirstRun(progress)) return pool[0];
            var last = progress.LastBossOf(slot);
            string only = null;
            var count = 0;
            foreach (var id in pool)
            {
                if (id == last) continue;
                only = id;
                count++;
            }
            return count == 1 ? only : null;
        }

        public static NextGoalBox Create(Transform parent, ArtLibrary art, int screenX, int screenY, int order)
        {
            var go = new GameObject("NextGoal");
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<NextGoalBox>();
            b.Build(art, screenX, screenY, order);
            return b;
        }

        void Build(ArtLibrary art, int screenX, int screenY, int order)
        {
            left = LocalX(screenX);
            top = LocalTop(screenY);
            PixelRect.Set(PixelRect.Create(transform, "Border", art.Pixel, Palette.Wall, order + 1), left, top - Height, left + Width, top);
            PixelRect.Set(PixelRect.Create(transform, "Fill", art.Pixel, RunUi.Panel, order + 2), left + 1, top - Height + 1, left + Width - 1, top - 1);

            header = TextAt(art, left + Pad, 2, PixelText.Align.Left, order + 4);
            refresh = TextAt(art, left + Width - Pad, 2, PixelText.Align.Right, order + 4);
            for (var i = 0; i < DailyQuests.PerDay; i++)
            {
                var dy = RowTop + i * RowPitch;
                var chestTop = top - dy - 5;
                chestBody[i] = PixelRect.Create(transform, "Chest" + i, art.Pixel, Palette.Wood, order + 3);
                PixelRect.Set(chestBody[i], left + Pad, chestTop - ChestH, left + Pad + ChestW, chestTop);
                chestLid[i] = PixelRect.Create(transform, "ChestLid" + i, art.Pixel, Palette.WoodDark, order + 4);
                PixelRect.Set(chestLid[i], left + Pad, chestTop - 3, left + Pad + ChestW, chestTop - 2);
                chestLock[i] = PixelRect.Create(transform, "ChestLock" + i, art.Pixel, Palette.Outline, order + 5);
                PixelRect.Set(chestLock[i], left + Pad + 4, chestTop - 5, left + Pad + 5, chestTop - 3);
                rowText[i] = TextAt(art, left + Pad + ChestW + 5, dy, PixelText.Align.Left, order + 4);
                rowCount[i] = TextAt(art, left + Width - Pad, dy, PixelText.Align.Right, order + 4);
            }
        }

        /// <summary>Text whose top edge is <paramref name="dy"/> pixels below the box top.</summary>
        PixelText TextAt(ArtLibrary art, float x, int dy, PixelText.Align align, int order) =>
            PixelText.Create(transform, "Text", art.Font, new Vector2(x, top - dy - PixelFont.CellHeight), align, 1, order);

        public void Show(ProgressData progress) => Show(progress, null);

        /// <summary><paramref name="run"/> is unused since the daily quests (kept for callers).</summary>
        public void Show(ProgressData progress, RunState run)
        {
            var p = progress ?? new ProgressData();
            Info = UnlockRules.NextGoal(p);
            var q = DailyQuests.Today(p, Today);
            Quests = q;
            header.Set(RunStrings.DailyQuests, Palette.WallWarning);
            var all = DailyQuests.AllDone(q);
            refresh.Set(all ? RunStrings.QuestsAllDone : RunStrings.QuestsRefresh(Now()), all ? Green : Palette.TextDim);
            for (var i = 0; i < DailyQuests.PerDay; i++)
            {
                var slot = i < q.Slots.Count ? q.Slots[i] : null;
                var def = slot != null ? DailyQuests.Def(slot.Id) : null;
                var on = def != null;
                chestBody[i].enabled = chestLid[i].enabled = chestLock[i].enabled = on;
                if (!on) { rowText[i].Set("", Palette.Text); rowCount[i].Set("", Palette.Text); continue; }
                var tier = ChestColors(def.Reward);
                chestBody[i].color = slot.Done ? (Color)Palette.Wall : (Color)tier.body;
                chestLid[i].color = slot.Done ? (Color)Palette.Floor : (Color)tier.lid;
                rowText[i].Set(RunStrings.QuestTitle(slot), slot.Done ? Palette.TextDim : Palette.Text);
                rowCount[i].Set(slot.Done ? RunStrings.QuestDone : RunStrings.Progress(slot.Progress, def.Target), slot.Done ? Green : Palette.WallWarning);
            }
        }

        /// <summary>Body and lid colour of the small chest for a reward tier.</summary>
        public static (Color32 body, Color32 lid) ChestColors(ChestTier tier) =>
            tier == ChestTier.Gold ? (Palette.PrimaryFill, new Color32(176, 120, 30, 255))
            : tier == ChestTier.Silver ? (Palette.Metal, Palette.MetalDark)
            : (Palette.Wood, Palette.WoodDark);
    }
}
