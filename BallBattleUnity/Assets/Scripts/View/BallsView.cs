using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-12c BALLS: every ball's level and shards (2 x 4 tiles, locked balls dimmed), and a detail page per ball with its
    /// level track, shard bar, gold cost and UPGRADE. Opened from the menu hub. Reads and writes the save through the store
    /// (an upgrade is saved at once); MetaRules does the arithmetic.
    /// </summary>
    public sealed class BallsView : MonoBehaviour
    {
        public const int Order = MenuView.Order + 20;   // over the menu hub
        public const float TileW = 116f, TileH = 64f, TileGap = 8f, GridTop = 176f;
        public static readonly Rect BackArea = new Rect(-60, -222, 120, 30);
        public static readonly Rect UpgradeArea = new Rect(-80, -110, 160, 36);
        public static readonly Rect DetailBackArea = new Rect(-60, -222, 120, 30);
        /// <summary>BB-12g: opens the ball's SKILL TREE page.</summary>
        public static readonly Rect TreeArea = new Rect(-80, -172, 160, 30);
        public static readonly Color32 UpColor = new Color32(120, 230, 120, 255);
        const float DetailBarLeft = -100f, DetailBarRight = 100f;

        public event Action Closed;

        ArtLibrary art;
        ISaveStore store;
        Transform root, grid, detail;
        PixelText gold, detailName, detailLevel, detailShards, detailCost, detailPoints, detailNote, flash;
        PixelText shardHint;
        SpriteRenderer detailBall, detailBarFill;
        readonly List<SpriteRenderer> pips = new List<SpriteRenderer>();
        PixelButton upgrade, treeButton;
        TreeView tree;
        public TreeView Tree => tree;
        public PixelButton TreeButton => treeButton;
        readonly List<Tile> tiles = new List<Tile>();
        float flashLeft;

        sealed class Tile
        {
            public string Id;
            public PixelButton Button;
            public SpriteRenderer Ball, BarFill;
            public PixelText Name, Level, Tail;
        }

        public bool IsOpen => gameObject.activeSelf;
        public bool DetailOpen => detail.gameObject.activeSelf;
        public string DetailId { get; private set; }
        public PixelButton UpgradeButton => upgrade;
        public bool UpgradeReady { get; private set; }
        public string DetailLevelText => detailLevel.Text;
        public string DetailCostText => detailCost.Text;
        public string GoldText => gold.Text;
        public PixelButton TileButton(string id) => tiles.Find(t => t.Id == id)?.Button;
        public string TileText(string id)
        {
            var t = tiles.Find(x => x.Id == id);
            return t == null ? "" : $"{t.Name.Text} {t.Level.Text} {t.Tail.Text}";
        }
        public int TileCount => tiles.Count;

        public static Rect TileArea(int index)
        {
            var col = index % 2;
            var row = index / 2;
            var left = col == 0 ? -TileGap / 2f - TileW : TileGap / 2f;
            var top = GridTop - row * (TileH + TileGap);
            return new Rect(left, top - TileH, TileW, TileH);
        }

        public static BallsView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Balls");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BallsView>();
            v.art = art;
            v.root = go.transform;
            PixelRect.Set(PixelRect.Create(v.root, "Back", art.Pixel, Palette.Background, Order), -200, -300, 200, 300);
            v.gold = PixelText.Create(v.root, "Gold", art.Font, new Vector2(126, 210), PixelText.Align.Right, 1, Order + 6);
            v.shardHint = PixelText.Create(v.root, "ShardHint", art.Font, new Vector2(0, 184), PixelText.Align.Center, 1, Order + 7);
            v.BuildGrid();
            v.BuildDetail();
            v.flash = PixelText.Create(v.root, "Flash", art.Font, new Vector2(0, 184), PixelText.Align.Center, 2, Order + 30);
            v.gameObject.SetActive(false);
            return v;
        }

        void BuildGrid()
        {
            grid = new GameObject("Grid").transform;
            grid.SetParent(root, false);
            PixelText.Create(grid, "Title", art.Font, new Vector2(-126, 204), PixelText.Align.Left, 2, Order + 6).Set("BALLS", Palette.WallWarning);
            var i = 0;
            foreach (var e in WeaponRegistry.All)
            {
                var a = TileArea(i++);
                var t = new Tile { Id = e.Id };
                t.Button = PixelButton.Create(grid, "Tile_" + e.Id, art, a, null, 1, RunUi.Panel, Palette.Wall, Palette.Text, Order + 2);
                var id = e.Id;
                t.Button.Clicked += () => OpenDetail(id);
                var o = t.Button.IconOrder;
                t.Ball = RunUi.Icon(t.Button.transform, new Vector2(a.xMin + 20, a.yMax - 22), o);
                t.Ball.sprite = art.Get(e.Id).Ball;
                t.Name = PixelText.Create(t.Button.transform, "Name", art.Font, new Vector2(a.xMin + 40, a.yMax - 18), PixelText.Align.Left, 1, o + 1);
                t.Level = PixelText.Create(t.Button.transform, "Level", art.Font, new Vector2(a.xMin + 40, a.yMax - 33), PixelText.Align.Left, 1, o + 1);
                PixelRect.Set(PixelRect.Create(t.Button.transform, "BarBack", art.Pixel, Palette.HpBack, o), a.xMin + 6, a.yMin + 8, a.xMax - 6, a.yMin + 13);
                t.BarFill = PixelRect.Create(t.Button.transform, "BarFill", art.Pixel, Palette.Look(e.Id).Body, o + 1);
                // UI-FIX2: the state (UP! / MAX / 3/5) gets its own line under the level; on the level line "LÊN CẤP!" ran into "CẤP 10"
                t.Tail = PixelText.Create(t.Button.transform, "Tail", art.Font, new Vector2(a.xMin + 40, a.yMax - 46), PixelText.Align.Left, 1, o + 1);
                tiles.Add(t);
            }
            var back = PixelButton.Create(grid, "BallsBack", art, BackArea, "BACK", 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            back.ClickCue = AudioIds.UiBack;
            back.Clicked += Close;
        }

        void BuildDetail()
        {
            detail = new GameObject("Detail").transform;
            detail.SetParent(root, false);
            detailBall = RunUi.Icon(detail, new Vector2(0, 150), Order + 6);
            detailBall.transform.localScale = new Vector3(2f, 2f, 1f);
            detailName = PixelText.Create(detail, "Name", art.Font, new Vector2(0, 88), PixelText.Align.Center, 2, Order + 6);
            detailLevel = PixelText.Create(detail, "Level", art.Font, new Vector2(0, 70), PixelText.Align.Center, 1, Order + 6);
            for (var i = 0; i < MetaTuning.MaxLevel; i++)
            {
                var x = -95 + i * 20;
                pips.Add(PixelRect.Create(detail, "Pip", art.Pixel, Palette.HpBack, Order + 6));
                PixelRect.Set(pips[i], x, 50, x + 14, 60);
            }
            detailShards = PixelText.Create(detail, "Shards", art.Font, new Vector2(0, 28), PixelText.Align.Center, 1, Order + 6);
            PixelRect.Set(PixelRect.Create(detail, "BarBack", art.Pixel, Palette.HpBack, Order + 5), DetailBarLeft, 14, DetailBarRight, 22);
            detailBarFill = PixelRect.Create(detail, "BarFill", art.Pixel, Palette.WallWarning, Order + 6);
            detailCost = PixelText.Create(detail, "Cost", art.Font, new Vector2(0, -12), PixelText.Align.Center, 1, Order + 6);
            detailNote = PixelText.Create(detail, "Note", art.Font, new Vector2(0, -30), PixelText.Align.Center, 1, Order + 6);
            upgrade = PixelButton.Create(detail, "Upgrade", art, UpgradeArea, "UPGRADE", 2, Palette.WallWarning, Palette.WallWarning, Palette.Background, Order + 2);
            upgrade.ClickCue = null;   // SFX4: PressUpgrade plays level_up or ui_denied
            upgrade.Clicked += PressUpgrade;
            detailPoints = PixelText.Create(detail, "Points", art.Font, new Vector2(0, -136), PixelText.Align.Center, 1, Order + 6);
            treeButton = PixelButton.Create(detail, "TreeButton", art, TreeArea, "SKILL TREE", 2, Palette.Floor, BallsView.UpColor, BallsView.UpColor, Order + 2);
            treeButton.Clicked += OpenTree;
            tree = TreeView.Create(root, art);
            tree.Closed += () => { if (DetailId != null) OpenDetail(DetailId); };
            var back = PixelButton.Create(detail, "DetailBack", art, DetailBackArea, "BACK", 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            back.ClickCue = AudioIds.UiBack;
            back.Clicked += () => ShowGrid();
            detail.gameObject.SetActive(false);
        }

        ProgressData Progress() => store?.Load()?.Progress ?? new ProgressData();

        public void Show(ISaveStore saveStore)
        {
            store = saveStore;
            gameObject.SetActive(true);
            ShowGrid();
        }

        public void Close()
        {
            MarkShardHintSeen();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        public void ShowGrid()
        {
            detail.gameObject.SetActive(false);
            if (tree != null && tree.IsOpen) tree.gameObject.SetActive(false);
            grid.gameObject.SetActive(true);
            flashLeft = 0f; flash.gameObject.SetActive(false);   // UI-FIX2: LEVEL UP! belongs to the detail page, not over the grid title
            DetailId = null;
            var onboarding = Progress().Onboarding;
            shardHint.Set(onboarding != null && !onboarding.HintsOff && !onboarding.HasSeen("shards") ? "COLLECT SHARDS TO LEVEL UP BALLS" : "", Palette.WallWarning);
            RefreshGrid(Progress());
            PixelButton.NoteShownAll(grid);
        }

        void RefreshGrid(ProgressData p)
        {
            gold.Set("¤ " + p.Gold, Palette.PrimaryFill);
            var unlocked = UnlockRules.Weapons(p);
            foreach (var t in tiles)
            {
                var a = t.Button.Area;
                var open = unlocked.Contains(t.Id);
                t.Button.enabled = open;
                t.Ball.color = open ? Color.white : new Color(0.25f, 0.25f, 0.3f, 1f);
                t.Name.Set(WeaponNames.Of(t.Id), open ? Palette.Text : Palette.TextDim);
                if (!open)
                {
                    t.Level.Set("LOCKED", Palette.TextDim);
                    t.Tail.Set("", Palette.TextDim);
                    PixelRect.Set(t.BarFill, a.xMin + 6, a.yMin + 8, a.xMin + 6, a.yMin + 13);
                    t.BarFill.enabled = false;
                    t.Button.SetColors(Palette.Background, Palette.HpBack);
                    continue;
                }
                var level = MetaRules.LevelOf(p, t.Id);
                var (have, need) = ShardsOf(p, t.Id);
                t.Level.Set("LV " + level, Palette.Text);
                var up = MetaRules.CanUpgrade(p, t.Id);
                t.Tail.Set(level >= MetaTuning.MaxLevel ? "MAX" : up ? "UP!" : have + "/" + need, level >= MetaTuning.MaxLevel ? Palette.PrimaryFill : up ? UpColor : Palette.TextDim);
                var fill = need > 0 ? Mathf.Clamp01(have / (float)need) : 1f;
                t.BarFill.enabled = fill > 0f;
                PixelRect.Set(t.BarFill, a.xMin + 6, a.yMin + 8, Mathf.Round(a.xMin + 6 + (a.width - 12) * fill), a.yMin + 13);
                t.Button.SetColors(RunUi.Panel, up ? UpColor : Palette.Wall);
            }
        }

        static (int have, int need) ShardsOf(ProgressData p, string id)
        {
            var have = 0;
            if (p.Balls != null) foreach (var b in p.Balls) if (b.Id == id) have = b.Shards;
            return (have, MetaRules.ShardsToNext(MetaRules.LevelOf(p, id)));
        }

        /// <summary>BB-12g: the SKILL TREE page of the ball on the detail page.</summary>
        public void OpenTree()
        {
            if (DetailId == null) return;
            detail.gameObject.SetActive(false);
            tree.Show(store, DetailId);
        }

        public void OpenDetail(string id)
        {
            MarkShardHintSeen();
            shardHint.Set("", Palette.WallWarning);
            DetailId = id;
            grid.gameObject.SetActive(false);
            detail.gameObject.SetActive(true);
            RefreshDetail(Progress());
            PixelButton.NoteShownAll(detail);
        }

        void MarkShardHintSeen()
        {
            var data = store?.Load();
            if (data?.Progress?.Onboarding == null || data.Progress.Onboarding.HasSeen("shards")) return;
            data.Progress.Onboarding.MarkSeen("shards");
            store.Save(data);
        }

        void RefreshDetail(ProgressData p)
        {
            var id = DetailId;
            gold.Set("¤ " + p.Gold, Palette.PrimaryFill);
            detailBall.sprite = art.Get(id).Ball;
            detailName.Set(WeaponNames.Of(id), Palette.Look(id).Body);
            var level = MetaRules.LevelOf(p, id);
            detailLevel.Set($"LEVEL {level} / {MetaTuning.MaxLevel}", Palette.Text);
            for (var i = 0; i < pips.Count; i++) pips[i].color = i < level ? (Color32)Palette.Look(id).Body : Palette.HpBack;
            var (have, need) = ShardsOf(p, id);
            var max = level >= MetaTuning.MaxLevel;
            detailShards.Set(max ? "MAX LEVEL" : $"SHARDS {have} / {need}", max ? Palette.PrimaryFill : Palette.Text);
            var fill = max ? 1f : need > 0 ? Mathf.Clamp01(have / (float)need) : 0f;
            detailBarFill.enabled = fill > 0f;
            PixelRect.Set(detailBarFill, DetailBarLeft, 14, Mathf.Round(DetailBarLeft + (DetailBarRight - DetailBarLeft) * fill), 22);
            var goldNeed = MetaRules.GoldToNext(level);
            detailCost.Set(max ? "" : $"NEXT LEVEL: {need} SHARDS  {goldNeed} ¤", Palette.TextDim);
            UpgradeReady = MetaRules.CanUpgrade(p, id);
            string note;
            if (max) note = "THIS BALL IS MAXED";
            else if (UpgradeReady) note = "READY TO LEVEL UP";
            else if (have < need) note = $"NEED {need - have} MORE SHARDS";
            else note = $"NEED {goldNeed - p.Gold} MORE ¤";
            detailNote.Set(note, UpgradeReady ? UpColor : Palette.TextDim);
            upgrade.gameObject.SetActive(!max);
            upgrade.SetColors(UpgradeReady ? (Color32)Palette.WallWarning : Palette.Floor, UpgradeReady ? (Color32)Palette.WallWarning : Palette.HpBack);
            upgrade.SetText("UPGRADE", UpgradeReady ? Palette.Background : Palette.TextDim);
            var ball = MetaRules.Ball(p, id);
            var free = MetaTree.Free(ball);
            detailPoints.Set(free > 0 ? $"SKILL POINTS: {free} FREE OF {MetaRules.TreePoints(level)}" : $"SKILL POINTS: {MetaRules.TreePoints(level)} ALL SPENT",
                free > 0 ? UpColor : Palette.TextDim);
        }

        /// <summary>UPGRADE: spend and save at once; nothing happens when the ball cannot level yet.</summary>
        public void PressUpgrade()
        {
            if (store == null || DetailId == null) return;
            var data = store.Load();
            if (!MetaRules.Upgrade(data.Progress, DetailId)) { UiAudio.Play(AudioIds.UiDenied); return; }
            store.Save(data);
            UiAudio.Play(AudioIds.LevelUp);
            RefreshDetail(data.Progress);
            flash.Set("LEVEL UP!", UpColor);
            flash.gameObject.SetActive(true);
            flashLeft = 1.2f;
        }

        public void Tick(float dt)
        {
            if (flashLeft <= 0f) return;
            flashLeft -= dt;
            if (flashLeft <= 0f) flash.gameObject.SetActive(false);
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        public bool FlashShown => flash.gameObject.activeSelf && flashLeft > 0f;
    }
}
