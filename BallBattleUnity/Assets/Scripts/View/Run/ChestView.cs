using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// BB-12b CHEST screen, drawn over whatever is under it (Run summary, start pick, main menu). The chest is already applied
    /// to the progress when this opens (MetaRules.Open), so it only shows it: the chest shakes, opens, then one row per ball
    /// that got shards (ball, +N, bar toward the next level, UP! when it can level now) and the gold. A tap anywhere skips the
    /// animation; COLLECT closes. ODDS shows the drop table (the same numbers MetaTuning uses).
    /// </summary>
    public sealed class ChestView : MonoBehaviour
    {
        public const int Order = RunUi.Order + 40;   // over the Run screens and the tutorial tips
        public const float ShakeSeconds = 0.55f, RowSeconds = 0.12f;
        public const int RowPitch = 20, MaxRows = 8;
        public const float RowsTop = 52f;
        public static readonly Rect CollectArea = new Rect(-80, -205, 160, 32);
        public static readonly Rect OddsArea = new Rect(70, 203, 56, 24);
        public static readonly Rect OddsBackArea = new Rect(-60, -222, 120, 30);
        const float BarLeft = 2f, BarRight = 72f;

        public event Action Collected;

        ArtLibrary art;
        Transform root, chestRoot, rowsRoot, oddsRoot;
        PixelText title, subtitle, goldLine, goldTotal;
        PixelButton skip, collect, odds;
        readonly List<GameObject> rows = new List<GameObject>();
        readonly List<PixelText> oddsLines = new List<PixelText>();
        SpriteRenderer lid, lidBand, glow, inside;
        readonly List<SpriteRenderer> rays = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> chestParts = new List<SpriteRenderer>();
        float t;
        int shown;
        ChestResult chest;

        public bool IsOpen => gameObject.activeSelf;
        public bool Revealed { get; private set; }
        public bool LidOpen { get; private set; }
        public bool OddsOpen => oddsRoot != null && oddsRoot.gameObject.activeSelf;
        public PixelButton CollectButton => collect;
        public PixelButton OddsButton => odds;
        public PixelButton SkipButton => skip;
        public ChestResult Chest => chest;
        /// <summary>Text of each reward row ("EMBER +15 12/15"), in order (tests).</summary>
        public List<string> RowTexts { get; } = new List<string>();
        public int RowsVisible { get; private set; }
        public string TitleText => title.Text;
        public string GoldText => goldLine.Text;
        public List<string> OddsText { get; } = new List<string>();

        public static string TierName(ChestTier t) => t == ChestTier.Gold ? "GOLD" : t == ChestTier.Silver ? "SILVER" : "WOOD";

        /// <summary>Body, dark and trim colours of each tier's chest.</summary>
        public static (Color32 body, Color32 dark, Color32 trim) Colors(ChestTier t)
        {
            switch (t)
            {
                case ChestTier.Gold: return (new Color32(244, 197, 66, 255), new Color32(170, 112, 28, 255), new Color32(255, 244, 200, 255));
                case ChestTier.Silver: return (new Color32(160, 170, 196, 255), new Color32(92, 100, 128, 255), new Color32(236, 240, 250, 255));
                default: return (new Color32(150, 98, 58, 255), new Color32(92, 58, 36, 255), new Color32(200, 205, 220, 255));
            }
        }

        /// <summary>The drop table as text lines (ODDS panel). <paramref name="unlocked"/> = balls that can get random shards.</summary>
        public static List<string> OddsLines(int unlocked)
        {
            var l = new List<string> { "ONE CHEST AFTER EVERY RUN" };
            string Row(ChestTier t, string when) => $"{TierName(t)}: {when}";
            l.Add(Row(ChestTier.Wood, "RUN ENDS BEFORE BOSS 1"));
            l.Add(Row(ChestTier.Silver, "BOSS 1 BEATEN"));
            l.Add(Row(ChestTier.Gold, "RUN WON"));
            l.Add("");
            foreach (var t in new[] { ChestTier.Wood, ChestTier.Silver, ChestTier.Gold })
            {
                var i = (int)t;
                l.Add($"{TierName(t)}: {MetaTuning.PlayedShards[i]} + {MetaTuning.RandomShards[i]} SHARDS  {MetaTuning.ChestGold[i]} ¤");
            }
            l.Add("");
            l.Add("FIRST NUMBER: YOUR BALL");
            l.Add(unlocked > 0 ? $"OTHERS: 1 IN {unlocked} PER UNLOCKED BALL" : "OTHERS: A RANDOM UNLOCKED BALL");
            l.Add("LOCKED BALLS GET NONE");
            l.Add($"LV{MetaTuning.MaxLevel} BALL: 1 SHARD BECOMES {MetaTuning.MaxedShardGold} ¤");
            return l;
        }

        public static ChestView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Chest");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ChestView>();
            v.art = art;
            v.root = go.transform;
            PixelRect.Set(PixelRect.Create(v.root, "Back", art.Pixel, new Color32(16, 16, 26, 248), Order), -200, -300, 200, 300);
            // A tap anywhere (behind every button) skips the animation.
            v.skip = PixelButton.Create(v.root, "Skip", art, new Rect(-135, -240, 270, 480), null, 1, new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0), Palette.Text, Order + 1);
            v.skip.ClickCue = null;
            v.skip.Clicked += v.SkipAnimation;
            v.title = PixelText.Create(v.root, "Title", art.Font, new Vector2(0, 178), PixelText.Align.Center, 2, Order + 6);
            v.subtitle = PixelText.Create(v.root, "Subtitle", art.Font, new Vector2(0, 162), PixelText.Align.Center, 1, Order + 6);

            v.chestRoot = new GameObject("ChestArt").transform;
            v.chestRoot.SetParent(v.root, false);
            v.rowsRoot = new GameObject("Rows").transform;
            v.rowsRoot.SetParent(v.root, false);
            v.goldLine = PixelText.Create(v.root, "Gold", art.Font, new Vector2(0, -128), PixelText.Align.Center, 2, Order + 6);
            v.goldTotal = PixelText.Create(v.root, "GoldTotal", art.Font, new Vector2(0, -148), PixelText.Align.Center, 1, Order + 6);

            v.collect = PixelButton.Create(v.root, "Collect", art, CollectArea, "COLLECT", 2, Palette.WallWarning, Palette.WallWarning, Palette.Background, Order + 3);
            v.collect.Clicked += v.PressCollect;
            v.odds = PixelButton.Create(v.root, "Odds", art, OddsArea, "ODDS", 1, Palette.Floor, Palette.TextDim, Palette.Text, Order + 3);
            v.odds.Clicked += () => v.ShowOdds(true);
            v.BuildOdds();
            v.gameObject.SetActive(false);
            return v;
        }

        void BuildOdds()
        {
            oddsRoot = new GameObject("OddsPanel").transform;
            oddsRoot.SetParent(root, false);
            PixelRect.Set(PixelRect.Create(oddsRoot, "OddsBorder", art.Pixel, Palette.Wall, Order + 20), -129, -232, 129, 200);
            PixelRect.Set(PixelRect.Create(oddsRoot, "OddsFill", art.Pixel, RunUi.Panel, Order + 21), -128, -231, 128, 199);
            PixelText.Create(oddsRoot, "OddsTitle", art.Font, new Vector2(0, 176), PixelText.Align.Center, 2, Order + 22).Set("CHEST ODDS", Palette.WallWarning);
            for (var i = 0; i < 14; i++)
                oddsLines.Add(PixelText.Create(oddsRoot, "OddsLine", art.Font, new Vector2(-120, 156 - i * PixelFont.CellHeight), PixelText.Align.Left, 1, Order + 22));
            var back = PixelButton.Create(oddsRoot, "OddsBack", art, OddsBackArea, "BACK", 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 23);
            back.ClickCue = AudioIds.UiBack;
            back.Clicked += () => ShowOdds(false);
            oddsRoot.gameObject.SetActive(false);
        }

        public void ShowOdds(bool on)
        {
            if (on)
            {
                OddsText.Clear();
                var lines = OddsLines(unlockedCount);
                for (var i = 0; i < oddsLines.Count; i++)
                {
                    var s = i < lines.Count ? lines[i] : "";
                    oddsLines[i].Set(s, i == 0 ? Palette.Text : Palette.TextDim);
                    if (s.Length > 0) OddsText.Add(s);
                }
            }
            oddsRoot.gameObject.SetActive(on);
            skip.enabled = collect.enabled = odds.enabled = !on;
            if (on) PixelButton.NoteShownAll(oddsRoot);
            else PixelButton.NoteShownAll(root);
        }

        int unlockedCount;

        readonly List<PixelButton> blocked = new List<PixelButton>();

        /// <summary>
        /// Show a chest already applied to <paramref name="after"/> (the progress after it). The buttons under
        /// <paramref name="under"/> are switched off while it is open (input reaches every button on screen), and back on at COLLECT.
        /// </summary>
        public void Show(ChestResult result, ProgressData after, string caption, params Transform[] under)
        {
            Unblock();
            if (under != null)
                foreach (var u in under)
                    if (u != null)
                        foreach (var b in u.GetComponentsInChildren<PixelButton>(true))
                            if (b.enabled) { b.enabled = false; blocked.Add(b); }
            chest = result;
            unlockedCount = UnlockRules.Weapons(after ?? new ProgressData()).Count;
            gameObject.SetActive(true);
            oddsRoot.gameObject.SetActive(false);
            skip.enabled = collect.enabled = odds.enabled = true;
            var c = Colors(result.Tier);
            title.Set(TierName(result.Tier) + " CHEST", result.Tier == ChestTier.Wood ? Palette.Text : c.body);
            subtitle.Set(caption ?? "", Palette.TextDim);
            DrawChest(result.Tier);
            BuildRows(result, after ?? new ProgressData());
            goldLine.Set("+" + result.TotalGold + " ¤", Palette.PrimaryFill);
            goldTotal.Set((result.ConvertedGold > 0 ? $"{result.ConvertedGold} FROM LV{MetaTuning.MaxLevel} BALLS   " : "") + "¤ " + (after?.Gold ?? 0), Palette.TextDim);
            t = 0f;
            shown = 0;
            Revealed = false;
            SetLid(false);
            ApplyReveal();
            PixelButton.NoteShownAll(root);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            Unblock();
        }

        void Unblock()
        {
            foreach (var b in blocked) if (b != null) b.enabled = true;
            blocked.Clear();
        }

        void DrawChest(ChestTier tier)
        {
            foreach (var p in chestParts) if (p != null) Kill(p.gameObject);
            chestParts.Clear();
            var (body, dark, trim) = Colors(tier);
            SpriteRenderer R(string n, Color32 col, float l, float b, float r, float tp, int o)
            {
                var sr = PixelRect.Create(chestRoot, n, art.Pixel, col, Order + o);
                PixelRect.Set(sr, l, b, r, tp);
                chestParts.Add(sr);
                return sr;
            }
            // Light from the open chest: three short rays above it (shown only when open).
            glow = R("Rays", new Color32(255, 240, 170, 200), -1, 132, 1, 150, 3);
            rays.Clear();
            rays.Add(R("RayL", new Color32(255, 240, 170, 160), -16, 130, -14, 143, 3));
            rays.Add(R("RayR", new Color32(255, 240, 170, 160), 14, 130, 16, 143, 3));
            inside = R("Inside", Palette.Outline, -27, 110, 27, 113, 6);
            R("Shadow", Palette.Outline, -30, 84, 30, 87, 3);
            R("BodyEdge", Palette.Outline, -29, 86, 29, 114, 3);
            R("Body", body, -28, 87, 28, 113, 4);
            R("BodyDark", dark, -28, 87, 28, 92, 5);
            R("BandL", trim, -20, 87, -16, 113, 5);
            R("BandR", trim, 16, 87, 20, 113, 5);
            R("Lock", trim, -4, 100, 4, 110, 6);
            R("Keyhole", Palette.Outline, -1, 102, 1, 106, 7);
            lid = R("Lid", body, -29, 113, 29, 127, 4);
            lidBand = R("LidBand", trim, -29, 113, 29, 116, 5);
        }

        void SetLid(bool open)
        {
            LidOpen = open;
            var lift = open ? 12f : 0f;
            if (lid != null) PixelRect.Set(lid, -29, 113 + lift, 29, 127 + lift);
            if (lidBand != null) PixelRect.Set(lidBand, -29, 113 + lift, 29, 116 + lift);
            if (glow != null) glow.enabled = open;
            if (inside != null) inside.enabled = open;
            foreach (var r in rays) if (r != null) r.enabled = open;
        }

        void BuildRows(ChestResult r, ProgressData after)
        {
            foreach (var row in rows) if (row != null) Kill(row);
            rows.Clear();
            RowTexts.Clear();
            var tierColor = Colors(r.Tier).body;
            for (var i = 0; i < r.Shards.Count && i < MaxRows; i++)
            {
                var s = r.Shards[i];
                var y = RowsTop - i * RowPitch;
                var go = new GameObject("Row_" + s.Ball);
                go.transform.SetParent(rowsRoot, false);
                rows.Add(go);
                var t = go.transform;
                var icon = RunUi.Icon(t, new Vector2(-112, y + 4), Order + 6);
                icon.sprite = art.Get(s.Ball).Ball;
                icon.transform.localScale = new Vector3(0.5f, 0.5f, 1f);
                PixelText.Create(t, "Name", art.Font, new Vector2(-100, y - 3), PixelText.Align.Left, 1, Order + 6).Set(WeaponNames.Of(s.Ball), Palette.Text);
                PixelText.Create(t, "Plus", art.Font, new Vector2(-6, y - 3), PixelText.Align.Right, 1, Order + 6)
                    .Set("+" + s.Count, s.Ball == r.PlayedBall ? Palette.PrimaryFill : (Color32)Palette.Text);
                var level = MetaRules.LevelOf(after, s.Ball);
                var have = 0;
                if (after.Balls != null) foreach (var b in after.Balls) if (b.Id == s.Ball) have = b.Shards;
                var need = MetaRules.ShardsToNext(level);
                PixelRect.Set(PixelRect.Create(t, "BarBack", art.Pixel, Palette.HpBack, Order + 5), BarLeft, y, BarRight, y + 6);
                var fill = need > 0 ? Mathf.Clamp01(have / (float)need) : 1f;
                if (fill > 0f) PixelRect.Set(PixelRect.Create(t, "BarFill", art.Pixel, tierColor, Order + 6), BarLeft, y, Mathf.Round(BarLeft + (BarRight - BarLeft) * fill), y + 6);
                string tail;
                Color32 tailColor;
                if (level >= MetaTuning.MaxLevel) { tail = "MAX"; tailColor = Palette.PrimaryFill; }
                else if (MetaRules.CanUpgrade(after, s.Ball)) { tail = "UP!"; tailColor = new Color32(120, 230, 120, 255); }
                else { tail = have + "/" + need; tailColor = Palette.TextDim; }
                PixelText.Create(t, "Tail", art.Font, new Vector2(126, y - 3), PixelText.Align.Right, 1, Order + 6).Set(tail, tailColor);
                RowTexts.Add($"{WeaponNames.Of(s.Ball)} +{s.Count} LV{level} {tail}");
            }
        }

        static void Kill(GameObject go)
        {
            go.SetActive(false);
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        /// <summary>One frame (Update's body; tests drive it).</summary>
        public void Tick(float dt)
        {
            if (!IsOpen || Revealed) return;
            t += dt;
            if (!LidOpen)
            {
                var shake = Mathf.Repeat(t * 30f, 2f) < 1f ? 1f : -1f;
                chestRoot.localPosition = new Vector3(t < ShakeSeconds ? shake : 0f, 0f, 0f);
                if (t >= ShakeSeconds) { chestRoot.localPosition = Vector3.zero; SetLid(true); UiAudio.Play(AudioIds.ChestOpen); }
            }
            else
            {
                var before = shown;
                shown = Mathf.Min(rows.Count + 1, Mathf.FloorToInt((t - ShakeSeconds) / RowSeconds) + 1);
                if (shown > before && before > 0) UiAudio.Play(AudioIds.ChestItem);   // SFX4: one ting per reward row
                if (shown >= rows.Count + 1) Revealed = true;
            }
            ApplyReveal();
        }

        void Update() => Tick(Time.unscaledDeltaTime);

        void ApplyReveal()
        {
            RowsVisible = 0;
            for (var i = 0; i < rows.Count; i++)
            {
                var on = LidOpen && i < shown;
                rows[i].SetActive(on);
                if (on) RowsVisible++;
            }
            var goldOn = LidOpen && shown > rows.Count;
            goldLine.gameObject.SetActive(goldOn);
            goldTotal.gameObject.SetActive(goldOn);
            collect.gameObject.SetActive(Revealed);
        }

        /// <summary>Jump to the end of the animation (a tap anywhere).</summary>
        public void SkipAnimation()
        {
            if (!IsOpen || Revealed) return;
            chestRoot.localPosition = Vector3.zero;
            if (!LidOpen) UiAudio.Play(AudioIds.ChestOpen);
            SetLid(true);
            shown = rows.Count + 1;
            Revealed = true;
            ApplyReveal();
            PixelButton.NoteShownAll(root);
        }

        void PressCollect()
        {
            if (!Revealed) { SkipAnimation(); return; }
            Hide();
            Collected?.Invoke();
        }
    }
}
