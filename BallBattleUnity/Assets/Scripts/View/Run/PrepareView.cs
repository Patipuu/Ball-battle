using System;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Before each fight: enemy preview, your build, 3 cards (first pick free, one more for coins), REROLL, FIGHT.
    /// Redrawn from RunState on every change (rare: a tap), so building strings here is fine.
    /// A tap is never ignored silently: a card or REROLL that cannot be used shows why (on the card / under REROLL)
    /// and blinks when tapped; FIGHT with the free pick unused asks "SKIP FREE PICK?" first.
    /// </summary>
    public sealed class PrepareView : MonoBehaviour
    {
        // ---- Layout (native pixels, screen centred). Public so tests can check hitboxes. ----
        /// <summary>BB-FONT1 (5x7 font, 13 px cells): 58 px cards = title row (26) + two detail rows (13 + 13), 1 px between rows, 2 px padding.</summary>
        public const float CardHeight = 58f;
        static readonly float[] CardBottoms = { -49f, -110f, -171f };
        public static Rect CardArea(int i) => new Rect(-125, CardBottoms[i], 250, CardHeight);
        public static readonly Rect RerollArea = new Rect(-125, -205, 110, 30);
        public static readonly Rect FightArea = new Rect(-5, -205, 130, 30);
        /// <summary>Card text (title, details) stays left of this x (2 px inside the card's right edge).</summary>
        public const float CardTextRight = 123f;
        /// <summary>Traits lines in the ENEMY / YOU boxes: from x -74 to 121.</summary>
        const int TraitsWidth = 195;
        /// <summary>Gap between the REROLL button and the reason line under it.</summary>
        public const float RerollReasonGap = 2f;
        /// <summary>Bottom of the scale-1 reason line: its top is RerollReasonGap below the button.</summary>
        public static readonly float RerollReasonY = RerollArea.yMin - RerollReasonGap - PixelFont.CellHeight;
        public static readonly Rect ConfirmPanel = ConfirmDialog.PanelAt(0);   // where SKIP FREE PICK? opens (off FIGHT)
        public static readonly Rect ConfirmYesArea = ConfirmDialog.YesAt(0);
        public static readonly Rect ConfirmNoArea = ConfirmDialog.NoAt(0);

        const float BlinkSeconds = 0.45f;
        static readonly Color32 TakenFill = new Color32(30, 56, 40, 255);
        static readonly Color32 TakenAccent = new Color32(110, 220, 120, 255);
        static readonly Color32 Blocked = Palette.Hot;

        public event Action<int> CardPicked;
        public event Action RerollRequested, FightRequested;

        ArtLibrary art;
        PixelText header, purse, pickHint, enemyLabel, enemyName, enemyTraits, enemyTraits2, enemyStats, youName, youTraits, youTraits2, youStats;
        SpriteRenderer enemyIcon, youIcon;
        PixelText arenaName;
        Transform arenaRoot;
        string shownArena;
        PixelButton reroll;
        PixelText rerollReason;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];
        readonly PixelText[] cardTitles = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardDetails = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardDetails2 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardTags = new PixelText[RunTuning.OfferSize];
        readonly SpriteRenderer[] cardFrames = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] cardStripes = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] cardIcons = new SpriteRenderer[RunTuning.OfferSize];
        /// <summary>A title too long to sit beside the reason tag wraps onto these two small lines (the icon stays).</summary>
        readonly PixelText[] cardTitleSmall1 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardTitleSmall2 = new PixelText[RunTuning.OfferSize];
        readonly CardFace[] faces = new CardFace[RunTuning.OfferSize];

        /// <summary>Card text starts here; with a 2x icon (28 px) it moves right past it.</summary>
        public const float CardTextX = -117f;
        public const float CardTextXWithIcon = -86f;
        static readonly Color32 FrameTaken = new Color32(150, 230, 160, 255);
        static readonly Color32 FrameBlocked = new Color32(120, 120, 132, 255);
        readonly float[] cardBlink = new float[RunTuning.OfferSize];
        float rerollBlink;
        ConfirmDialog dialog;
        RunState run;
        /// <summary>Taps are ignored while the SKIP FREE PICK dialog covers the cards.</summary>
        bool TapSwallowed => ConfirmOpen;
        bool onScreen;

        /// <summary>On entering the screen every tap is dropped for this long (the tap that closed the fight result).</summary>
        public const float EntryInputBlockSeconds = 0.6f;

        public static PrepareView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "Prepare", art);
            var v = root.gameObject.AddComponent<PrepareView>();
            v.art = art;
            v.Build(root);
            return v;
        }

        void Build(Transform root)
        {
            header = RunUi.Text(root, art, 0, 212, 2);    // cell 212..238
            purse = RunUi.Text(root, art, 0, 198, 1);     // cell 198..211

            // ENEMY box 113..196: label 181, name x2 154, traits 141 and 128 (wrapped between traits), stats 115.
            RunUi.Box(root, art, -125, 113, 125, 196);
            enemyLabel = RunUi.Text(root, art, -119, 181, 1, PixelText.Align.Left);
            enemyIcon = RunUi.Icon(root, new Vector2(-100, 152), RunUi.Order + 3);
            enemyName = RunUi.Text(root, art, -74, 154, 2, PixelText.Align.Left);
            enemyTraits = RunUi.Text(root, art, -74, 141, 1, PixelText.Align.Left);
            enemyTraits2 = RunUi.Text(root, art, -74, 128, 1, PixelText.Align.Left);
            enemyStats = RunUi.Text(root, art, -74, 115, 1, PixelText.Align.Left);
            arenaName = RunUi.Text(root, art, 121, 115, 1, PixelText.Align.Right);   // right-aligned inside the box ("ARENA BUMPERS" no longer fits centred under the preview)
            arenaRoot = new GameObject("ArenaPreview").transform;
            arenaRoot.SetParent(root, false);

            // YOU box 26..109: label 94, name x2 67, traits 54 and 41, stats 28.
            RunUi.Box(root, art, -125, 26, 125, 109);
            RunUi.Text(root, art, -119, 94, 1, PixelText.Align.Left, "YOU", Palette.TextDim);
            youIcon = RunUi.Icon(root, new Vector2(-100, 67), RunUi.Order + 3);
            youName = RunUi.Text(root, art, -74, 67, 2, PixelText.Align.Left);
            youTraits = RunUi.Text(root, art, -74, 54, 1, PixelText.Align.Left);
            youTraits2 = RunUi.Text(root, art, -74, 41, 1, PixelText.Align.Left);
            youStats = RunUi.Text(root, art, -74, 28, 1, PixelText.Align.Left);

            pickHint = RunUi.Text(root, art, 0, 11, 1);   // cell 11..24, between YOU (26) and card 1 (top 9)
            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                var area = CardArea(i);
                cards[i] = RunUi.Button(root, art, area, null, 1, Palette.Wall, () => { if (!TapSwallowed) OnCardClicked(index); });
                cards[i].ClickCue = null;   // card_pick (RunFlow.CardTaken) or ui_denied, decided in OnCardClicked
                // Art v1: 9-slice frame over the button fill, 4 px stripe on the left, trait icon at 2x, then the text.
                var frameGo = new GameObject("Frame");
                frameGo.transform.SetParent(cards[i].transform, false);
                frameGo.transform.localPosition = new Vector3(area.center.x, area.center.y, 0f);
                cardFrames[i] = frameGo.AddComponent<SpriteRenderer>();
                cardFrames[i].sortingOrder = cards[i].IconOrder;
                cardStripes[i] = PixelRect.Create(cards[i].transform, "Stripe", art.Pixel, Palette.Text, cards[i].IconOrder + 1);
                PixelRect.Set(cardStripes[i], area.xMin + 2, area.yMin + 2, area.xMin + 6, area.yMax - 2);
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(cards[i].transform, false);
                iconGo.transform.localPosition = new Vector3(area.xMin + 22, area.yMin + CardHeight / 2f, 0f);   // 28x28 at x -117..-89, centred
                iconGo.transform.localScale = new Vector3(2f, 2f, 1f);
                cardIcons[i] = iconGo.AddComponent<SpriteRenderer>();
                cardIcons[i].sortingOrder = cards[i].IconOrder + 1;
                var order = cards[i].IconOrder + 2;
                cardTitles[i] = PixelText.Create(cards[i].transform, "Title", art.Font, new Vector2(-117, area.yMin + TitleY), PixelText.Align.Left, 2, order);
                cardTags[i] = PixelText.Create(cards[i].transform, "Tag", art.Font, new Vector2(117, area.yMin + TagY), PixelText.Align.Right, 1, order);
                cardTitleSmall1[i] = PixelText.Create(cards[i].transform, "TitleLine1", art.Font, new Vector2(-117, area.yMin + SmallLine1Y), PixelText.Align.Left, 1, order);
                cardTitleSmall2[i] = PixelText.Create(cards[i].transform, "TitleLine2", art.Font, new Vector2(-117, area.yMin + SmallLine2Y), PixelText.Align.Left, 1, order);
                cardDetails[i] = PixelText.Create(cards[i].transform, "Detail", art.Font, new Vector2(-117, area.yMin + Detail1Y), PixelText.Align.Left, 1, order);
                cardDetails2[i] = PixelText.Create(cards[i].transform, "Detail2", art.Font, new Vector2(-117, area.yMin + Detail2Y), PixelText.Align.Left, 1, order);
            }

            reroll = RunUi.Button(root, art, RerollArea, $"REROLL {RunTuning.RerollCost}", 1, Palette.TextDim, () => { if (!TapSwallowed) OnRerollClicked(); });   // cell inside 30 px
            reroll.ClickCue = null;   // card_reroll (RunFlow.Rerolled) or ui_denied
            // One small line 2 px under the button (it used to sit inside it, over the label).
            rerollReason = PixelText.Create(reroll.transform, "Reason", art.Font, new Vector2(RerollArea.center.x, RerollReasonY), PixelText.Align.Center, 1, reroll.IconOrder + 1);
            RunUi.Button(root, art, FightArea, "FIGHT!", 2, Palette.WallWarning, () => { if (!TapSwallowed) OnFightClicked(); });   // x3 (39 px) no longer fits 30 px
            RunUi.Text(root, art, 0, -235, 1, initial: "ESC: MENU   THE RUN IS SAVED", color: Palette.TextDim);   // cell -235..-222, under the reason line

            BuildConfirm(root);
        }

        void BuildConfirm(Transform root)
        {
            dialog = ConfirmDialog.Create(root, art, RunUi.Order + 20, "Confirm");   // over the cards and their text
            dialog.Closed += NoteShownUnderDialog;   // the tap on YES/NO must not go on to a card under it
        }
        /// <summary>Sprite radius the preview icons are scaled from (the sim radius 24 of a boss = x1.5).</summary>
        float IconNativeRadius => art != null && art.BallFlash != null ? art.BallFlash.rect.width / art.BallFlash.pixelsPerUnit * 0.5f : 16f;
        const float ArenaPreviewX = 92f;
        const float ArenaPreviewY = 164f;
        const float ArenaPreviewWidth = 46f;   // the widest arena (260) maps to this many pixels
        /// <summary>The arena preview draws the floor tile at this scale (5 px instead of 10).</summary>
        public const float PreviewTileScale = 0.5f;
        SpriteRenderer previewFloor;

        /// <summary>Floor tile renderer of the arena preview (tests).</summary>
        public SpriteRenderer PreviewFloor => previewFloor;

        public void Show(RunState state)
        {
            run = state;
            var entering = !onScreen || !gameObject.activeSelf;
            onScreen = true;
            gameObject.SetActive(true);
            if (entering) OnEnter();
            var boss = run.IsBossFight;
            header.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (boss ? "  BOSS" : ""), boss ? Palette.WallWarning : Palette.Text);
            purse.Set($"RETRY {(run.RetryCount == 0 ? "1" : "USED")}   COINS {run.Coins}", Palette.Text);

            // Numbers exactly as the fight starts: the same match RunFlow will create, spawn hooks (Heavy, Glass Cannon) applied.
            var preview = run.Over ? null : run.CreateMatch();
            var you = preview?.Balls[0];
            var foe = preview?.Balls[1];

            var enemy = run.Enemy;
            var bossId = run.CurrentBossId;
            enemyLabel.Set(boss ? BossText.Name(bossId) : "ENEMY", boss ? Palette.WallWarning : Palette.TextDim);
            SetBall(enemyIcon, enemyName, enemy.WeaponId, (foe != null ? foe.Radius.ToFloat() : (enemy.Radius > Fix.Zero ? enemy.Radius.ToFloat() : IconNativeRadius)) / IconNativeRadius);
            if (bossId == BallBattle.Sim.Boss.BossIds.Mimic) enemyName.Set(BossText.CopiesYou, Palette.WallWarning);   // its loadout is the player's at fight start
            if (boss) SetTraits(enemyTraits, enemyTraits2, BossLines(enemy, bossId, TraitsWidth));
            else SetTraits(enemyTraits, enemyTraits2, CardText.TraitLines(enemy, TraitsWidth));
            enemyStats.Set(EnemyHpText((foe != null ? foe.Hp : enemy.MaxHp).ToFloat(), (foe != null ? foe.MaxHp : enemy.MaxHp).ToFloat()), Palette.TextDim);

            SetBall(youIcon, youName, run.Build.WeaponId, (you != null ? you.Radius.ToFloat() : IconNativeRadius) / IconNativeRadius);
            SetTraits(youTraits, youTraits2, CardText.TraitLines(run.Build, TraitsWidth));
            youStats.Set(you != null ? CardText.Stats(run.Build, you.Hp.ToFloat(), you.MaxHp.ToFloat()) : CardText.Stats(run.Build), Palette.TextDim);
            ShowArena(run.ArenaId);

            var cost = run.NextPickCost;
            pickHint.Set(cost < 0 ? "NO MORE PICKS - FIGHT!" : cost == 0 ? "PICK ONE CARD - FREE" : $"BUY ONE MORE: {cost} COINS",
                         cost > 0 && run.Coins < cost ? Palette.TextDim : Palette.WallWarning);

            for (var i = 0; i < cards.Length; i++)
            {
                var has = i < run.OfferCount;
                cards[i].gameObject.SetActive(has);
                if (!has) continue;
                ApplyFace(i, CardFace.For(run.OfferCard(i), art));
                StyleCard(i);
            }

            var rerollWhy = RerollBlockReason(run);
            reroll.SetText($"REROLL {run.NextRerollCost}", rerollWhy == null ? Palette.Text : Palette.HpBack);
            reroll.SetColors(Palette.Floor, rerollWhy == null ? Palette.TextDim : Palette.HpBack);
            if (rerollWhy != null) rerollReason.Set(rerollWhy, Blocked);
            else rerollReason.Set(RerollAgainText(run), Palette.TextDim);   // reroll is repeatable: say it costs again
        }

        /// <summary>Up to two traits lines (wrapped between traits); a third line, which three traits never need, joins the second.</summary>
        static void SetTraits(PixelText line1, PixelText line2, System.Collections.Generic.List<string> lines)
        {
            line1.Set(lines.Count > 0 ? lines[0] : "", Palette.Text);
            line2.Set(lines.Count > 1 ? string.Join(" ", lines.GetRange(1, lines.Count - 1)) : "", Palette.Text);
        }

        /// <summary>
        /// BB-04 ENEMY box lines under a boss's name: its traits on the first line (when it rolled any; never for MIMIC, whose traits
        /// are the player's), then the one-line hint, wrapped to two lines when there are no traits. At most two lines.
        /// </summary>
        public static System.Collections.Generic.List<string> BossLines(RunBuild enemy, string bossId, int width)
        {
            var lines = new System.Collections.Generic.List<string>();
            if (bossId != BallBattle.Sim.Boss.BossIds.Mimic && enemy != null && enemy.Traits.Count > 0)
                lines.Add(TextWrap.Cut(string.Join(" ", CardText.TraitNames(enemy)), width));
            var hint = BossText.Hint(bossId);
            if (lines.Count > 0) lines.Add(TextWrap.Cut(hint, width));
            else
            {
                var words = new System.Collections.Generic.List<string>(hint.Split(' '));
                foreach (var l in TextWrap.Items(words, "", " ", width)) if (lines.Count < 2) lines.Add(l);
            }
            return lines;
        }

        /// <summary>ENEMY box label, name and the two lines under it (tests).</summary>
        public string EnemyLabelText => enemyLabel.Text;
        public string EnemyLinesText => (enemyTraits.Text + " " + enemyTraits2.Text).Trim();

        /// <summary>Traits lines of the YOU box (tests).</summary>
        public string YouTraitsText => (youTraits.Text + " " + youTraits2.Text).Trim();

        /// <summary>Enemy HP line: the HP the fight really starts with (Heavy, Glass Cannon, boss HP all applied).</summary>
        /// <summary>The line under REROLL ("NOT ENOUGH COINS" in red, or the dim "AGAIN ..." note; tests).</summary>
        public PixelText RerollReasonText => rerollReason;
        public PixelButton RerollButton => reroll;

        public static string EnemyHpText(float hp, float maxHp)
        {
            var h = HpText.Of(hp);
            var m = HpText.Of(maxHp);
            return h == m ? $"HP {m}" : $"HP {h}/{m}";
        }

        /// <summary>
        /// Screen entry: drop all taps for EntryInputBlockSeconds and ignore any press that started before the screen
        /// was shown (the click that dismissed the fight result must never land on a card, REROLL or FIGHT).
        /// </summary>
        void OnEnter()
        {
            PixelInput.BlockFor(EntryInputBlockSeconds);
            PixelButton.NoteShownAll(transform);
            for (var i = 0; i < cardBlink.Length; i++) cardBlink[i] = 0f;
            rerollBlink = 0f;
        }

        void NoteShownUnderDialog()
        {
            foreach (var c in cards) c.NoteShown();
            reroll.NoteShown();
            foreach (var b in GetComponentsInChildren<PixelButton>(false)) if (!b.transform.IsChildOf(dialog.Root)) b.NoteShown();
        }

        /// <summary>Card look: available (kind color), taken (green fill + TAKEN), blocked (grey + reason in red).</summary>
        void StyleCard(int i)
        {
            if (run == null || i >= run.OfferCount) return;
            var card = run.OfferCard(i);
            var taken = run.IsPicked(i);
            var why = taken ? null : BlockReason(run, i);
            var blink = cardBlink[i] > 0f;
            var face = faces[i] ?? CardFace.For(card, art);
            if (taken)
            {
                cards[i].SetColors(TakenFill, blink ? Palette.Text : TakenAccent);
                cardTitles[i].Set(face.Title, TakenAccent);
                cardTags[i].Set("TAKEN", blink ? Palette.Text : TakenAccent);
                cardFrames[i].color = blink ? Color.white : (Color)(Color32)FrameTaken;
            }
            else if (why != null)
            {
                cards[i].SetColors(Palette.Floor, blink ? Blocked : Palette.HpBack);
                cardTitles[i].Set(face.Title, Palette.TextDim);
                cardTags[i].Set(why, blink ? Palette.Text : Blocked);
                cardFrames[i].color = blink ? (Color)(Color32)Blocked : (Color)(Color32)FrameBlocked;
            }
            else
            {
                cards[i].SetColors(Palette.Floor, KindColor(card.Kind));
                cardTitles[i].Set(face.Title, Palette.Text);
                cardTags[i].Set(face.Tag, face.Frame == ArtLibrary.CardFrame.Counter ? CardFace.CounterColor : Palette.TextDim);
                cardFrames[i].color = Color.white;
            }
            FitTitle(i, face.Title, cardTitles[i].TextColor);
        }

        /// <summary>
        /// The 2x title on one line, or - when it would touch the right-hand tag (a reason like ALREADY YOUR WEAPON) - wrapped
        /// at a space onto two 1x lines: the first beside the tag, the second under it, both above the description. The icon
        /// is kept either way.
        /// </summary>
        void FitTitle(int i, string title, Color32 color)
        {
            var x = cardTitles[i].transform.localPosition.x;
            var y0 = CardArea(i).yMin;
            var tag = cardTags[i].Text;
            var tagLeft = string.IsNullOrEmpty(tag) ? float.PositiveInfinity : 117f - PixelFont.MeasureWidth(tag);
            var right = Mathf.Min(tagLeft - TitleTagGap, CardTextRight);   // the 2x title must clear the tag and the card edge
            string line1 = null, line2 = null;
            if (x + PixelFont.MeasureWidth(title) * 2 > right)
                for (var at = title.LastIndexOf(' '); at > 0; at = title.LastIndexOf(' ', at - 1))
                {
                    var a = title.Substring(0, at);
                    var b = title.Substring(at + 1);
                    if (x + PixelFont.MeasureWidth(a) + TitleTagGap > tagLeft || x + PixelFont.MeasureWidth(b) > CardTextRight) continue;
                    line1 = a;
                    line2 = b;
                    break;   // the longest first line that fits
                }
            var wrapped = line1 != null;
            cardTitles[i].Set(wrapped ? "" : title, color);
            cardTitleSmall1[i].transform.localPosition = new Vector3(x, y0 + SmallLine1Y, 0f);
            cardTitleSmall2[i].transform.localPosition = new Vector3(x, y0 + SmallLine2Y, 0f);
            cardTitleSmall1[i].Set(wrapped ? line1 : "", color);
            cardTitleSmall2[i].Set(wrapped ? line2 : "", color);
        }

        const float TitleTagGap = 2f;
        /// <summary>Rows inside a card, px above its bottom (13 px cells, 1 px apart): title x2 30..56, tag 43..56 (beside the
        /// title, on the row of the first wrapped line), wrapped title 43..56 and 30..43, details 16..29 and 2..15.</summary>
        public const float TitleY = 30f, TagY = 43f, SmallLine1Y = 43f, SmallLine2Y = 30f, Detail1Y = 16f, Detail2Y = 2f;

        /// <summary>True when card <paramref name="i"/>'s title is wrapped onto two small lines (tests).</summary>
        public bool TitleWrapped(int i) => cardTitleSmall1[i].Text.Length > 0;
        public PixelText CardTitleLine(int i, int line) => line == 1 ? cardTitleSmall1[i] : cardTitleSmall2[i];

        /// <summary>Frame, stripe, icon and text of card <paramref name="i"/> from a face (colors of the state come from StyleCard).</summary>
        void ApplyFace(int i, CardFace face)
        {
            faces[i] = face;
            var frame = art.Frame(face.Frame);
            cardFrames[i].sprite = frame;
            cardFrames[i].enabled = frame != null;
            if (frame != null)
            {
                // The dashed counter frame tiles its edges so the dashes keep their size; the others stretch.
                cardFrames[i].drawMode = face.Frame == ArtLibrary.CardFrame.Counter ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
                cardFrames[i].size = new Vector2(CardArea(i).width, CardArea(i).height);
            }
            cardStripes[i].color = face.Stripe;
            cardIcons[i].sprite = face.Icon;
            cardIcons[i].enabled = face.Icon != null;
            var x = face.Icon != null ? CardTextXWithIcon : CardTextX;
            var y0 = CardArea(i).yMin;
            cardTitles[i].transform.localPosition = new Vector3(x, y0 + TitleY, 0f);
            cardDetails[i].transform.localPosition = new Vector3(x, y0 + Detail1Y, 0f);
            cardDetails2[i].transform.localPosition = new Vector3(x, y0 + Detail2Y, 0f);
            cardDetails[i].SetHighlighted(face.Detail1, Palette.TextDim, CardFace.NumberColor);
            cardDetails2[i].SetHighlighted(face.Detail2, Palette.TextDim, CardFace.NumberColor);
        }

        /// <summary>Draw a card face in slot <paramref name="i"/> without a run card behind it (style previews: counter sample).</summary>
        public void ShowFace(int i, CardFace face)
        {
            cards[i].gameObject.SetActive(true);
            ApplyFace(i, face);
            cards[i].SetColors(Palette.Floor, face.Stripe);
            cardTitles[i].Set(face.Title, Palette.Text);
            cardTags[i].Set(face.Tag, face.Frame == ArtLibrary.CardFrame.Counter ? CardFace.CounterColor : Palette.TextDim);
            cardFrames[i].color = Color.white;
        }

        /// <summary>Why card <paramref name="index"/> cannot be picked now, or null if it can (or is already taken).</summary>
        public static string BlockReason(RunState run, int index)
        {
            if (run == null || index < 0 || index >= run.OfferCount) return null;
            if (run.IsPicked(index)) return null;
            if (run.CanPick(index)) return null;
            if (run.Over || run.FightLocked) return "FIGHT STARTED";
            var card = run.OfferCard(index);
            if (!run.StillApplies(card))
            {
                switch (card.Kind)
                {
                    case CardKind.Trait:
                        var level = run.Build.TraitLevel(card.Id);
                        if (card.Level == 1) return level > 0 ? "ALREADY HAVE IT" : "TRAITS FULL";
                        return level >= card.Level ? "ALREADY LV" + level : "NEEDS LV" + (card.Level - 1);
                    case CardKind.SwapWeapon: return "ALREADY YOUR WEAPON";
                    case CardKind.Heal: return "HP ALREADY FULL";
                    default: return "CANNOT USE NOW";
                }
            }
            var cost = run.NextPickCost;
            if (cost < 0) return "NO PICKS LEFT";
            if (run.Coins < cost) return "NOT ENOUGH COINS";
            return "CANNOT USE NOW";
        }

        /// <summary>Why REROLL is locked now, or null if it can be used.</summary>
        public static string RerollBlockReason(RunState run)
        {
            if (run == null || run.CanReroll) return null;
            if (run.Over || run.FightLocked) return "FIGHT STARTED";
            if (run.PicksTaken > 0) return "ONLY BEFORE FIRST PICK";
            if (run.Coins < run.NextRerollCost) return "NOT ENOUGH COINS";
            return "LOCKED";
        }

        /// <summary>
        /// Under an available REROLL after one was used: each reroll costs RerollCost again (RunState allows repeats before
        /// the first pick), so the line says so with the coins left. Empty before the first reroll.
        /// </summary>
        public static string RerollAgainText(RunState run) =>
            run == null || run.RerollCount == 0 ? "" : $"AGAIN {run.NextRerollCost}  COINS {run.Coins}";
        /// <summary>True when FIGHT would throw away the free pick (none taken yet and at least one card can be taken).</summary>
        public static bool ShouldConfirmSkip(RunState run)
        {
            if (run == null || run.Over || run.FightLocked || run.PicksTaken > 0 || run.NextPickCost != 0) return false;
            for (var i = 0; i < run.OfferCount; i++)
                if (run.CanPick(i)) return true;
            return false;
        }

        void OnCardClicked(int i)
        {
            if (ConfirmOpen) return;
            if (run != null && !run.CanPick(i))
            {
                cardBlink[i] = BlinkSeconds;   // taken or blocked: blink the card and its reason instead of doing nothing
                StyleCard(i);
                UiAudio.Play(AudioIds.UiDenied);
                return;
            }
            if (run != null && run.OfferCard(i).Kind == CardKind.SwapWeapon)
            {
                // A swap changes the weapon for the rest of the run: ask first. NO costs nothing.
                var to = WeaponNames.Of(run.OfferCard(i).Id);
                UiAudio.Play(AudioIds.UiClick);   // the card itself: card_pick comes on YES
                dialog.Open("SWAP TO " + to + "?", "KEEPS TRAITS AND STATS", CardArea(i), () => CardPicked?.Invoke(i));
                return;
            }
            CardPicked?.Invoke(i);
        }

        void OnRerollClicked()
        {
            if (ConfirmOpen) return;
            if (run != null && !run.CanReroll)
            {
                rerollBlink = BlinkSeconds;
                rerollReason.Set(RerollBlockReason(run) ?? "", Palette.Text);
                reroll.SetColors(Palette.Floor, Blocked);
                UiAudio.Play(AudioIds.UiDenied);
                return;
            }
            RerollRequested?.Invoke();
        }

        void OnFightClicked()
        {
            if (ConfirmOpen) return;
            if (ShouldConfirmSkip(run))
            {
                dialog.Open("SKIP FREE PICK?", "THE FREE CARD IS LOST", FightArea, () => FightRequested?.Invoke());
                return;
            }
            FightRequested?.Invoke();
        }

        void Update()
        {
            var dt = Time.unscaledDeltaTime;
            for (var i = 0; i < cardBlink.Length; i++)
            {
                if (cardBlink[i] <= 0f) continue;
                cardBlink[i] -= dt;
                if (cardBlink[i] <= 0f) StyleCard(i);
            }
            if (rerollBlink > 0f)
            {
                rerollBlink -= dt;
                if (rerollBlink <= 0f && run != null)
                {
                    var why = RerollBlockReason(run);
                    if (why != null) rerollReason.Set(why, Blocked); else rerollReason.Set(RerollAgainText(run), Palette.TextDim);
                    reroll.SetColors(Palette.Floor, why == null ? Palette.TextDim : Palette.HpBack);
                }
            }
        }

        /// <summary>Arena name plus a small to-scale sketch (floor, walls, obstacles) of the next fight's arena.</summary>
        void ShowArena(string arenaId)
        {
            arenaName.Set("ARENA " + ArenaRegistry.Get(arenaId).DisplayName, Palette.Text);
            if (arenaId == shownArena) return;
            shownArena = arenaId;
            for (var i = arenaRoot.childCount - 1; i >= 0; i--)
            {
                var child = arenaRoot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            var config = ArenaRegistry.Create(arenaId);
            var layout = config.Layout ?? ArenaLayout.Empty;
            var scale = ArenaPreviewWidth / ArenaTuning.WideWidth.ToFloat();
            var halfW = Mathf.Round(config.ArenaWidth.ToFloat() * scale * 0.5f);
            var halfH = Mathf.Round(config.ArenaHeight.ToFloat() * scale * 0.5f);
            // Same colors and floor tile as the fight (arena_colors.json, floor_<id>), the tile drawn at half size.
            var hasArt = art.TryArena(arenaId, out var look);
            var wall = hasArt ? look.WallColor : (Color32)Palette.Wall;
            Rect(wall, -halfW - 1, -halfH - 1, halfW + 1, halfH + 1, 3);
            Rect(hasArt ? look.FloorColor : (Color32)Palette.Floor, -halfW, -halfH, halfW, halfH, 4);
            if (hasArt && look.Floor != null)
            {
                var tiles = new GameObject("FloorTiles");
                tiles.transform.SetParent(arenaRoot, false);
                tiles.transform.localPosition = new Vector3(ArenaPreviewX, ArenaPreviewY, 0f);
                tiles.transform.localScale = new Vector3(PreviewTileScale, PreviewTileScale, 1f);
                var sr = tiles.AddComponent<SpriteRenderer>();
                sr.sprite = look.Floor;
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.size = new Vector2(2f * halfW / PreviewTileScale, 2f * halfH / PreviewTileScale);
                sr.sortingOrder = RunUi.Order + 4;
                previewFloor = sr;
            }
            if (layout.WallDamage > Fix.Zero)
            {
                // spike_tooth color: a 1 px band inside the walls, only along the corner spike zones (as in the fight; the
                // zones come from the sim's SpikeZones.Segments of the start rect, scaled to the sketch)
                var tooth = new Color32(255, 70, 50, 255);
                foreach (var z in SpikeZones.Segments(config.ArenaAt(0), layout.SpikeCorners))
                {
                    var from = Mathf.Round(z.From.ToFloat() * scale);
                    var to = Mathf.Round(z.To.ToFloat() * scale);
                    switch (z.Wall)
                    {
                        case ArenaWall.Top: Rect(tooth, from, halfH - 1, to, halfH, 5); break;
                        case ArenaWall.Bottom: Rect(tooth, from, -halfH, to, -halfH + 1, 5); break;
                        case ArenaWall.Left: Rect(tooth, -halfW, from, -halfW + 1, to, 5); break;
                        default: Rect(tooth, halfW - 1, from, halfW, to, 5); break;
                    }
                }
            }
            if (config.MinArenaWidth < config.ArenaWidth && arenaId == ArenaRegistry.Tight)
            {
                var mw = Mathf.Round(config.MinArenaWidth.ToFloat() * scale * 0.5f);
                var mh = Mathf.Round(config.MinArenaHeight.ToFloat() * scale * 0.5f);
                Rect(Palette.HpBack, -mw, -mh, mw, mh, 5);   // where the shrinking walls end
            }
            for (var i = 0; i < layout.ObstacleCount; i++)
            {
                var o = layout.Get(i);
                var r = Mathf.Max(1f, Mathf.Round(o.Radius.ToFloat() * scale));
                var color = o.Damage > Fix.Zero ? new Color32(210, 60, 60, 255) : o.Boost > Fix.Zero ? new Color32(240, 170, 60, 255) : new Color32(120, 130, 150, 255);
                var x = Mathf.Round(o.A.X.ToFloat() * scale);
                var y = Mathf.Round(o.A.Y.ToFloat() * scale);
                Rect(color, x - r, y - r, x + r, y + r, 6);
            }
        }

        void Rect(Color32 color, float left, float bottom, float right, float top, int orderOffset)
        {
            var sr = PixelRect.Create(arenaRoot, "R", art.Pixel, color, RunUi.Order + orderOffset);
            PixelRect.Set(sr, ArenaPreviewX + left, ArenaPreviewY + bottom, ArenaPreviewX + right, ArenaPreviewY + top);
        }

        void SetBall(SpriteRenderer icon, PixelText name, string weaponId, float scale)
        {
            // Same sprite as in the arena: the pre-drawn _38/_43/_48 ball for x1.2/x1.35/x1.5 (bosses: _48) at x1, else scaled.
            icon.sprite = art.BallForSize(weaponId, scale, out var preSized);
            var s = preSized ? 1f : scale;
            icon.transform.localScale = new Vector3(s, s, 1f);
            name.Set(RunUi.WeaponName(weaponId), Palette.Look(weaponId).Body);
        }

        static Color32 KindColor(CardKind k)
        {
            switch (k)
            {
                case CardKind.Trait: return new Color32(180, 120, 240, 255);
                case CardKind.Heal:
                case CardKind.MaxHp: return new Color32(110, 220, 120, 255);
                case CardKind.SwapWeapon: return Palette.Text;
                default: return Palette.WallWarning;
            }
        }

        public void Hide()
        {
            dialog?.Close();
            onScreen = false;
            gameObject.SetActive(false);
        }

        // ---- Automation hooks and read-back (tests, MCP playthroughs). They go through the same paths as a tap. ----
        public void PressCard(int i) => OnCardClicked(i);
        public void PressReroll() => OnRerollClicked();
        public void PressFight() => OnFightClicked();
        public void PressConfirm(bool yes) => dialog.Press(yes);
        public bool ConfirmOpen => dialog != null && dialog.IsOpen;
        /// <summary>Weapon name cells (tests): you and the enemy.</summary>
        public string YouNameText => youName.Text;
        public string EnemyNameText => enemyName.Text;
        /// <summary>The open dialog (tests): question, YES / NO areas.</summary>
        public ConfirmDialog Dialog => dialog;
        public PixelButton Card(int i) => cards[i];
        public string CardTitle(int i) => cardTitles[i].Text;
        public PixelText CardTag(int i) => cardTags[i];
        public SpriteRenderer CardFrameRenderer(int i) => cardFrames[i];
        public SpriteRenderer CardIcon(int i) => cardIcons[i];
        public SpriteRenderer CardStripe(int i) => cardStripes[i];
        public PixelText CardDetail(int i, int line) => line == 1 ? cardDetails[i] : cardDetails2[i];
        public PixelText CardTitleText(int i) => cardTitles[i];
        public string RerollReason => rerollReason.Text;
        public string YouStats => youStats.Text;
        public string EnemyStats => enemyStats.Text;
        public string ArenaLabel => arenaName.Text;
        public int ArenaPreviewPieces => arenaRoot.childCount;
        public SpriteRenderer EnemyIcon => enemyIcon;
        public SpriteRenderer YouIcon => youIcon;
    }
}
