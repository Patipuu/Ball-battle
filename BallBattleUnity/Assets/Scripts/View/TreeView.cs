using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// SKILL TREE page of one ball (opened from its BALLS detail). BB-12g rules; BB-12h look: ten ICON nodes in four rows
    /// (root VITALITY / POWER / INSTINCT; skill LV2 A, OPENER, skill LV2 B; skill LV3 A / B; FINALE A / B), each with its rank or
    /// cost under the icon. Tap a node once to open its card:
    /// big icon, name, what it does, its state and an UPGRADE button. RESET takes every point back (free, outside a run).
    /// Each change is saved at once; the next run with this ball uses it.
    /// </summary>
    public sealed class TreeView : MonoBehaviour
    {
        public const int Order = BallsView.Order + 10;
        public const int PopupOrder = Order + 20;
        public enum Box { Vitality, Power, Instinct, Opener, A2, B2, A3, B3, FinA, FinB }
        public const float BoxW = 48f, BoxH = 46f;
        public static readonly float[] ColX = { -84f, 0f, 84f };
        public static readonly float[] RowBottom = { -40f, 12f, 64f, 116f };
        public static readonly Rect ResetArea = new Rect(-60, -124, 120, 30);
        public static readonly Rect BackArea = new Rect(-60, -222, 120, 30);
        // The card (popup) and its buttons.
        public static readonly Rect CardArea = new Rect(-122, -96, 244, 210);
        public static readonly Rect UpgradeArea = new Rect(-112, -88, 140, 32);
        public static readonly Rect CloseArea = new Rect(36, -88, 76, 32);
        /// <summary>Kept for older callers: the UPGRADE button on the card.</summary>
        public static readonly Rect AddArea = UpgradeArea;

        static readonly Color32 BoughtFill = new Color32(70, 58, 20, 255), Locked = new Color32(60, 60, 78, 255);
        static readonly Color32 IconLocked = new Color32(110, 110, 130, 255), Scrim = new Color32(8, 8, 14, 200);

        public event Action Closed;

        ArtLibrary art;
        ISaveStore store;
        string ballId;
        PixelText title, points, caption, tapHint;
        PixelButton reset;
        readonly Dictionary<Box, PixelButton> boxes = new Dictionary<Box, PixelButton>();
        readonly Dictionary<Box, SpriteRenderer> icons = new Dictionary<Box, SpriteRenderer>();
        readonly Dictionary<Box, PixelText> boxRank = new Dictionary<Box, PixelText>(), boxLevel = new Dictionary<Box, PixelText>();
        readonly Dictionary<Box, string> boxName = new Dictionary<Box, string>();
        readonly SpriteRenderer[] selectFrame = new SpriteRenderer[4];

        // Card
        Transform card;
        SpriteRenderer cardIcon;
        PixelText cardName, cardLine1, cardLine2, cardLine3, cardLine4, cardState, cardRank;
        PixelButton upgrade, close;

        public Box Selected { get; private set; } = Box.Vitality;
        public bool IsOpen => gameObject.activeSelf;
        public bool CardOpen => card != null && card.gameObject.activeSelf;
        public PixelButton BoxButton(Box b) => boxes[b];
        /// <summary>The UPGRADE button on the node card (it acts on the selected node).</summary>
        public PixelButton AddButton => upgrade;
        public PixelButton UpgradeButton => upgrade;
        public PixelButton CloseButton => close;
        public PixelButton ResetButton => reset;
        public string PointsText => points.Text;
        public string CaptionText => caption.Text;
        public string InfoState => cardState.Text;
        public string CardName => cardName.Text;
        public string BoxLabel(Box b) => boxName[b] + " " + boxRank[b].Text;
        public Sprite BoxIcon(Box b) => icons[b].sprite;
        public bool CanAddSelected { get; private set; }

        public static Rect BoxArea(Box b)
        {
            int row, col;
            switch (b)
            {
                case Box.Vitality: row = 0; col = 0; break;
                case Box.Power: row = 0; col = 1; break;
                case Box.Instinct: row = 0; col = 2; break;
                case Box.A2: row = 1; col = 0; break;
                case Box.Opener: row = 1; col = 1; break;
                case Box.B2: row = 1; col = 2; break;
                case Box.A3: row = 2; col = 0; break;
                case Box.B3: row = 2; col = 2; break;
                case Box.FinA: row = 3; col = 0; break;
                default: row = 3; col = 2; break;
            }
            return new Rect(ColX[col] - BoxW / 2f, RowBottom[row], BoxW, BoxH);
        }

        public static TreeNode NodeOf(Box b) =>
            b == Box.Vitality ? TreeNode.Vitality : b == Box.Power ? TreeNode.Power : b == Box.Instinct ? TreeNode.Instinct :
            b == Box.Opener ? TreeNode.Opener : b == Box.A2 || b == Box.A3 ? TreeNode.SkillA : b == Box.B2 || b == Box.B3 ? TreeNode.SkillB : TreeNode.Finale;

        static char BranchOf(Box b) => b == Box.B2 || b == Box.B3 || b == Box.FinB ? 'B' : 'A';

        /// <summary>The ball's body colour lifted toward white, so dark balls still read on the node tile.</summary>
        Color32 BallTint()
        {
            Color32 c = Palette.Look(ballId).Body;
            return new Color32((byte)(c.r + (255 - c.r) * 0.35f), (byte)(c.g + (255 - c.g) * 0.35f), (byte)(c.b + (255 - c.b) * 0.35f), 255);
        }

        static int SkillLevelOf(Box b) => b == Box.A2 || b == Box.B2 ? 2 : b == Box.A3 || b == Box.B3 ? 3 : 0;

        public static TreeView Create(Transform parent, ArtLibrary art)
        {
            var go = new GameObject("Tree");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<TreeView>();
            v.art = art;
            var root = go.transform;
            PixelRect.Set(PixelRect.Create(root, "Back", art.Pixel, Palette.Background, Order), -200, -300, 200, 300);
            v.title = PixelText.Create(root, "Title", art.Font, new Vector2(0, 205), PixelText.Align.Center, 2, Order + 6);
            v.points = PixelText.Create(root, "Points", art.Font, new Vector2(0, 186), PixelText.Align.Center, 1, Order + 6);
            // Connectors: each branch column and the middle (INSTINCT opens the middle row).
            foreach (var x in new[] { ColX[0], ColX[2] })
                for (var row = 0; row < 3; row++)
                    PixelRect.Set(PixelRect.Create(root, "Link", art.Pixel, Palette.HpBack, Order + 1), x - 1, RowBottom[row] + BoxH, x + 1, RowBottom[row + 1]);
            PixelRect.Set(PixelRect.Create(root, "Link", art.Pixel, Palette.HpBack, Order + 1), ColX[1] - 1, RowBottom[0] + BoxH, ColX[1] + 1, RowBottom[1]);
            foreach (Box b in Enum.GetValues(typeof(Box)))
            {
                var a = BoxArea(b);
                var btn = PixelButton.Create(root, "Box_" + b, art, a, null, 1, Palette.Floor, Palette.Wall, Palette.Text, Order + 2);
                btn.ClickCue = AudioIds.UiClick;
                var box = b;
                btn.Clicked += () => v.OpenCard(box);
                v.boxes[b] = btn;
                var icon = new GameObject("Icon").AddComponent<SpriteRenderer>();
                icon.transform.SetParent(btn.transform, false);
                icon.sprite = TreeIcons.Get(TreeIcons.KindOf(NodeOf(b)));
                icon.sortingOrder = btn.IconOrder;
                icon.transform.localPosition = new Vector3(a.center.x, a.yMin + 13 + TreeIcons.Size, 0f);   // 32 px icon above the rank line
                icon.transform.localScale = new Vector3(2f, 2f, 1f);
                v.icons[b] = icon;
                v.boxRank[b] = PixelText.Create(btn.transform, "Rank", art.Font, new Vector2(a.center.x, a.yMin + 3), PixelText.Align.Center, 1, btn.IconOrder + 1);
                v.boxLevel[b] = PixelText.Create(btn.transform, "Level", art.Font, new Vector2(a.xMax - 8, a.yMin + 14), PixelText.Align.Center, 1, btn.IconOrder + 2);
                v.boxName[b] = "";
            }
            for (var i = 0; i < 4; i++) v.selectFrame[i] = PixelRect.Create(root, "Select", art.Pixel, BallsView.UpColor, Order + 5);
            v.caption = PixelText.Create(root, "Caption", art.Font, new Vector2(0, -62), PixelText.Align.Center, 1, Order + 6);
            v.tapHint = PixelText.Create(root, "TapHint", art.Font, new Vector2(0, -80), PixelText.Align.Center, 1, Order + 6);
            v.tapHint.Set("TAP A NODE TO UPGRADE", Palette.TextDim);
            v.reset = PixelButton.Create(root, "Reset", art, ResetArea, "RESET", 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            v.reset.ClickCue = null;   // SFX4: tree_reset from PressReset
            v.reset.Clicked += v.PressReset;
            var back = PixelButton.Create(root, "TreeBack", art, BackArea, "BACK", 2, Palette.Floor, Palette.TextDim, Palette.Text, Order + 2);
            back.ClickCue = AudioIds.UiBack;
            back.Clicked += () => { v.CloseCard(); v.gameObject.SetActive(false); v.Closed?.Invoke(); };
            v.BuildCard(root);
            v.gameObject.SetActive(false);
            return v;
        }

        void BuildCard(Transform root)
        {
            card = new GameObject("Card").transform;
            card.SetParent(root, false);
            // Tapping outside the card closes it: a dark see-through shade and an invisible full-screen button over it.
            PixelRect.Set(PixelRect.Create(card, "ScrimShade", art.Pixel, Scrim, PopupOrder), -200, -300, 200, 300);
            var clear = new Color32(0, 0, 0, 0);
            var scrim = PixelButton.Create(card, "Scrim", art, new Rect(-200, -300, 400, 600), null, 1, clear, clear, Palette.Text, PopupOrder + 1);
            scrim.ClickCue = AudioIds.UiBack;
            // Every active PixelButton receives the same pointer event. The full-screen scrim
            // also covers UPGRADE, so it must ignore taps inside the card.
            scrim.Clicked += () => { if (!CardArea.Contains(scrim.LastReleaseNative)) CloseCard(); };
            var a = CardArea;
            PixelRect.Set(PixelRect.Create(card, "CardBorder", art.Pixel, Palette.WallWarning, PopupOrder + 3), a);
            PixelRect.Set(PixelRect.Create(card, "CardFill", art.Pixel, RunUi.Panel, PopupOrder + 4), a.xMin + 2, a.yMin + 2, a.xMax - 2, a.yMax - 2);
            // Icon plate + big icon (3x) top left, name and rank to its right.
            PixelRect.Set(PixelRect.Create(card, "IconPlate", art.Pixel, Palette.Floor, PopupOrder + 5), a.xMin + 10, a.yMax - 66, a.xMin + 66, a.yMax - 10);
            cardIcon = new GameObject("CardIcon").AddComponent<SpriteRenderer>();
            cardIcon.transform.SetParent(card, false);
            cardIcon.sortingOrder = PopupOrder + 6;
            cardIcon.transform.localPosition = new Vector3(a.xMin + 38, a.yMax - 38, 0f);
            cardIcon.transform.localScale = new Vector3(3f, 3f, 1f);
            cardName = PixelText.Create(card, "CardName", art.Font, new Vector2(a.xMin + 76, a.yMax - 28), PixelText.Align.Left, 1, PopupOrder + 7);
            cardRank = PixelText.Create(card, "CardRank", art.Font, new Vector2(a.xMin + 76, a.yMax - 48), PixelText.Align.Left, 1, PopupOrder + 7);
            cardLine1 = PixelText.Create(card, "CardLine1", art.Font, new Vector2(a.xMin + 12, a.yMax - 92), PixelText.Align.Left, 1, PopupOrder + 7);
            cardLine2 = PixelText.Create(card, "CardLine2", art.Font, new Vector2(a.xMin + 12, a.yMax - 106), PixelText.Align.Left, 1, PopupOrder + 7);
            cardLine3 = PixelText.Create(card, "CardLine3", art.Font, new Vector2(a.xMin + 12, a.yMax - 120), PixelText.Align.Left, 1, PopupOrder + 7);
            cardLine4 = PixelText.Create(card, "CardLine4", art.Font, new Vector2(a.xMin + 12, a.yMax - 134), PixelText.Align.Left, 1, PopupOrder + 7);
            cardState = PixelText.Create(card, "CardState", art.Font, new Vector2(a.xMin + 12, a.yMax - 154), PixelText.Align.Left, 1, PopupOrder + 7);
            upgrade = PixelButton.Create(card, "Upgrade", art, UpgradeArea, "UPGRADE", 2, Palette.WallWarning, Palette.WallWarning, Palette.Background, PopupOrder + 6);
            upgrade.ClickCue = null;   // SFX4: tree_add or ui_denied from PressAdd
            upgrade.Clicked += PressAdd;
            close = PixelButton.Create(card, "Close", art, CloseArea, "CLOSE", 1, Palette.Floor, Palette.TextDim, Palette.Text, PopupOrder + 6);
            close.ClickCue = AudioIds.UiBack;
            close.Clicked += CloseCard;
            card.gameObject.SetActive(false);
        }

        public void Show(ISaveStore saveStore, string weaponId)
        {
            store = saveStore;
            ballId = weaponId;
            gameObject.SetActive(true);
            card.gameObject.SetActive(false);
            Selected = Box.Vitality;
            Refresh();
            PixelButton.NoteShownAll(transform);
        }

        BallProgress Ball(SaveData data) => MetaRules.Ball(data.Progress, ballId);

        public void Select(Box b)
        {
            Selected = b;
            Refresh();
        }

        /// <summary>Selects <paramref name="b"/> and opens its card on a tap.</summary>
        public void OpenCard(Box b)
        {
            Selected = b;
            card.gameObject.SetActive(true);
            Refresh();
            PixelButton.NoteShownAll(card);   // the finger that opened it does not press UPGRADE
        }

        public void CloseCard()
        {
            if (card == null || !card.gameObject.activeSelf) return;
            card.gameObject.SetActive(false);
            PixelButton.NoteShownAll(transform);
        }

        // ---------------- state ----------------

        enum State { Bought, Open, Locked }

        /// <summary>The box is the next step of its node (the branch boxes go A2 → A3 → FINALE A in order).</summary>
        static State StateOf(BallProgress t, Box b, out string why)
        {
            why = "";
            var branch = b == Box.A2 || b == Box.A3 || b == Box.FinA ? 1 : b == Box.B2 || b == Box.B3 || b == Box.FinB ? 2 : 0;
            if (branch != 0 && t.Branch != 0 && t.Branch != branch) { why = "OTHER BRANCH CHOSEN. RESET TO SWITCH"; return State.Locked; }
            switch (b)
            {
                case Box.Vitality: if (t.Vit >= MetaTree.MaxRootRank) return State.Bought; break;
                case Box.Power: if (t.Pow >= MetaTree.MaxRootRank) return State.Bought; break;
                case Box.Instinct: if (t.Growth >= MetaTree.MaxRootRank) return State.Bought; break;
                case Box.Opener: if (t.Start) return State.Bought; break;
                case Box.A2: case Box.B2: if (t.SkillUps >= 1) return State.Bought; break;
                case Box.A3: case Box.B3:
                    if (t.SkillUps >= 2) return State.Bought;
                    if (t.SkillUps < 1) { why = "BUY LV2 OF THIS BRANCH FIRST"; return State.Locked; }
                    break;
                case Box.FinA: case Box.FinB:
                    if (t.Finale) return State.Bought;
                    if (t.SkillUps < 2) { why = "NEEDS LV3 OF THIS BRANCH"; return State.Locked; }
                    break;
            }
            var n = NodeOf(b);
            if (MetaTree.CanAdd(t, n)) return State.Open;
            if ((n == TreeNode.Opener || ((b == Box.A2 || b == Box.B2) && t.SkillUps == 0)) && !MetaTree.MiddleOpen(t))
                why = "NEEDS INSTINCT 1 AND 2 ROOT POINTS";
            else if ((b == Box.A3 || b == Box.B3) && MetaTree.Lower(t) < 5) why = "NEEDS 5 POINTS SPENT BELOW";
            else why = "NOT ENOUGH POINTS";
            return State.Locked;
        }

        string NameOf(Box b) => b == Box.FinA ? TreeText.FinaleName(ballId, 'A') : b == Box.FinB ? TreeText.FinaleName(ballId, 'B')
            : TreeText.Name(ballId, NodeOf(b), b == Box.A3 || b == Box.B3 ? 3 : 2);

        static string RankOf(BallProgress t, Box b, State state) =>
            b == Box.Vitality ? $"{t.Vit}/2" : b == Box.Power ? $"{t.Pow}/2" : b == Box.Instinct ? $"{t.Growth}/2"
            : state == State.Bought ? "OWNED" : MetaTree.Cost(NodeOf(b)) + (MetaTree.Cost(NodeOf(b)) == 1 ? " PT" : " PTS");

        public void Refresh()
        {
            var data = store?.Load() ?? new SaveData();
            var t = Ball(data);
            title.Set(WeaponNames.Of(ballId), Palette.Look(ballId).Body);
            var total = MetaRules.TreePoints(t.Level);
            points.Set($"LV {t.Level}   POINTS {MetaTree.Free(t)} FREE OF {total}", Palette.Text);
            foreach (var kv in boxes)
            {
                var b = kv.Key;
                var state = StateOf(t, b, out _);
                var partly = (b == Box.Vitality && t.Vit > 0) || (b == Box.Power && t.Pow > 0) || (b == Box.Instinct && t.Growth > 0);
                var lit = state == State.Bought || partly;
                kv.Value.SetColors(lit ? BoughtFill : state == State.Open ? (Color32)Palette.Floor : Palette.Background,
                    lit ? (Color32)Palette.WallWarning : state == State.Open ? (Color32)Palette.Text : Locked);
                // BB-12i: the ball's own icon; an open node shows it in the ball's colour.
                icons[b].sprite = TreeIcons.For(ballId, NodeOf(b), BranchOf(b));
                icons[b].color = lit ? (Color32)Palette.WallWarning : state == State.Open ? BallTint() : IconLocked;
                boxName[b] = NameOf(b);
                boxRank[b].Set(RankOf(t, b, state), lit ? Palette.WallWarning : state == State.Open ? Palette.Text : Palette.TextDim);
                var lv = SkillLevelOf(b);
                boxLevel[b].Set(lv > 0 ? lv.ToString() : b == Box.FinA ? "A" : b == Box.FinB ? "B" : "", lit ? Palette.Background : Palette.Text);
            }
            var sel = BoxArea(Selected);
            PixelRect.Set(selectFrame[0], sel.xMin - 2, sel.yMax + 1, sel.xMax + 2, sel.yMax + 2);
            PixelRect.Set(selectFrame[1], sel.xMin - 2, sel.yMin - 2, sel.xMax + 2, sel.yMin - 1);
            PixelRect.Set(selectFrame[2], sel.xMin - 2, sel.yMin - 1, sel.xMin - 1, sel.yMax + 1);
            PixelRect.Set(selectFrame[3], sel.xMax + 1, sel.yMin - 1, sel.xMax + 2, sel.yMax + 1);

            var s = StateOf(t, Selected, out var why);
            CanAddSelected = s == State.Open;
            var selName = Selected == Box.FinA || Selected == Box.FinB ? "FINALE: " + NameOf(Selected) : NameOf(Selected);
            caption.Set(selName + (s == State.Open ? "  " + RankOf(t, Selected, s) : s == State.Bought ? "  OWNED" : ""),
                s == State.Open ? BallsView.UpColor : s == State.Bought ? Palette.WallWarning : Palette.TextDim);

            // Card
            var fx = TreeText.Effect(ballId, NodeOf(Selected), Selected == Box.FinB ? 'B' : 'A', Selected == Box.A3 || Selected == Box.B3 ? 3 : 2);
            cardIcon.sprite = icons[Selected].sprite;
            cardIcon.color = icons[Selected].color;
            cardName.Set(selName, Palette.WallWarning);
            cardRank.Set(Selected == Box.Vitality || Selected == Box.Power || Selected == Box.Instinct ? "RANK " + RankOf(t, Selected, s)
                : s == State.Bought ? "OWNED" : "COST " + RankOf(t, Selected, s), Palette.Text);
            cardLine1.Set(fx[0], Palette.Text);
            cardLine2.Set(fx.Length > 1 ? fx[1] : "", Palette.Text);
            cardLine3.Set(fx.Length > 2 ? fx[2] : "", Palette.Text);
            cardLine4.Set(fx.Length > 3 ? fx[3] : "", Palette.Text);
            cardState.Set(s == State.Bought ? "OWNED" : s == State.Open ? $"READY. COSTS {MetaTree.Cost(NodeOf(Selected))} OF {MetaTree.Free(t)} FREE" : why,
                s == State.Open ? BallsView.UpColor : Palette.TextDim);
            upgrade.SetColors(CanAddSelected ? (Color32)Palette.WallWarning : Palette.Floor, CanAddSelected ? (Color32)Palette.WallWarning : Palette.HpBack);
            upgrade.SetText(s == State.Bought ? "OWNED" : "UPGRADE", CanAddSelected ? Palette.Background : Palette.TextDim);
        }

        public void PressAdd()
        {
            if (store == null || !CanAddSelected) { UiAudio.Play(AudioIds.UiDenied); return; }
            var data = store.Load();
            if (!MetaTree.Add(Ball(data), NodeOf(Selected))) { UiAudio.Play(AudioIds.UiDenied); return; }
            store.Save(data);
            UiAudio.Play(AudioIds.TreeAdd);
            Refresh();
        }

        public void PressReset()
        {
            if (store == null) return;
            var data = store.Load();
            UiAudio.Play(MetaTree.Spent(Ball(data)) > 0 ? AudioIds.TreeReset : AudioIds.UiClick);
            MetaTree.Reset(Ball(data));
            store.Save(data);
            Refresh();
        }
    }
}
