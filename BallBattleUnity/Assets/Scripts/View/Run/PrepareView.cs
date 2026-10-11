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
        public const float CardHeight = 36f;
        static readonly float[] CardBottoms = { 0f, -42f, -84f };
        public static Rect CardArea(int i) => new Rect(-125, CardBottoms[i], 250, CardHeight);
        public static readonly Rect RerollArea = new Rect(-125, -130, 110, 30);
        public static readonly Rect FightArea = new Rect(-5, -130, 130, 30);
        public static readonly Rect ConfirmPanel = new Rect(-112, -66, 224, 92);
        public static readonly Rect ConfirmYesArea = new Rect(-102, -58, 92, 28);
        public static readonly Rect ConfirmNoArea = new Rect(10, -58, 92, 28);

        const float BlinkSeconds = 0.45f;
        static readonly Color32 TakenFill = new Color32(30, 56, 40, 255);
        static readonly Color32 TakenAccent = new Color32(110, 220, 120, 255);
        static readonly Color32 Blocked = Palette.Hot;

        public event Action<int> CardPicked;
        public event Action RerollRequested, FightRequested;

        ArtLibrary art;
        PixelText header, purse, pickHint, enemyLabel, enemyName, enemyTraits, enemyStats, youName, youTraits, youStats;
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
        readonly float[] cardBlink = new float[RunTuning.OfferSize];
        float rerollBlink;
        Transform confirmRoot;
        RunState run;
        /// <summary>Taps are ignored while the SKIP FREE PICK dialog covers the cards.</summary>
        bool TapSwallowed => ConfirmOpen;
        bool onScreen;

        /// <summary>On entering the screen every tap is dropped for this long (the tap that closed the fight result).</summary>
        public const float EntryInputBlockSeconds = 0.3f;

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
            header = RunUi.Text(root, art, 0, 210, 2);
            purse = RunUi.Text(root, art, 0, 196, 1);

            RunUi.Box(root, art, -125, 126, 125, 188);
            enemyLabel = RunUi.Text(root, art, -119, 178, 1, PixelText.Align.Left);
            enemyIcon = RunUi.Icon(root, new Vector2(-100, 150), RunUi.Order + 3);
            enemyName = RunUi.Text(root, art, -74, 160, 2, PixelText.Align.Left);
            enemyTraits = RunUi.Text(root, art, -74, 148, 1, PixelText.Align.Left);
            enemyStats = RunUi.Text(root, art, -74, 136, 1, PixelText.Align.Left);
            arenaName = RunUi.Text(root, art, ArenaPreviewX, 129, 1);
            arenaRoot = new GameObject("ArenaPreview").transform;
            arenaRoot.SetParent(root, false);

            RunUi.Box(root, art, -125, 58, 125, 120);
            RunUi.Text(root, art, -119, 110, 1, PixelText.Align.Left, "YOU", Palette.TextDim);
            youIcon = RunUi.Icon(root, new Vector2(-100, 82), RunUi.Order + 3);
            youName = RunUi.Text(root, art, -74, 92, 2, PixelText.Align.Left);
            youTraits = RunUi.Text(root, art, -74, 80, 1, PixelText.Align.Left);
            youStats = RunUi.Text(root, art, -74, 68, 1, PixelText.Align.Left);

            pickHint = RunUi.Text(root, art, 0, 44, 1);
            for (var i = 0; i < cards.Length; i++)
            {
                var index = i;
                var area = CardArea(i);
                cards[i] = RunUi.Button(root, art, area, null, 1, Palette.Wall, () => { if (!TapSwallowed) OnCardClicked(index); });
                var order = cards[i].IconOrder + 1;
                cardTitles[i] = PixelText.Create(cards[i].transform, "Title", art.Font, new Vector2(-117, area.yMin + 22), PixelText.Align.Left, 2, order);
                cardTags[i] = PixelText.Create(cards[i].transform, "Tag", art.Font, new Vector2(117, area.yMin + 25), PixelText.Align.Right, 1, order);
                cardDetails[i] = PixelText.Create(cards[i].transform, "Detail", art.Font, new Vector2(-117, area.yMin + 12), PixelText.Align.Left, 1, order);
                cardDetails2[i] = PixelText.Create(cards[i].transform, "Detail2", art.Font, new Vector2(-117, area.yMin + 4), PixelText.Align.Left, 1, order);
            }

            reroll = RunUi.Button(root, art, RerollArea, $"REROLL {RunTuning.RerollCost}", 1, Palette.TextDim, () => { if (!TapSwallowed) OnRerollClicked(); });
            rerollReason = PixelText.Create(reroll.transform, "Reason", art.Font, new Vector2(RerollArea.center.x, RerollArea.yMin + 3), PixelText.Align.Center, 1, reroll.IconOrder + 1);
            RunUi.Button(root, art, FightArea, "FIGHT!", 3, Palette.WallWarning, () => { if (!TapSwallowed) OnFightClicked(); });
            RunUi.Text(root, art, 0, -150, 1, initial: "ESC: MENU   THE RUN IS SAVED", color: Palette.TextDim);

            BuildConfirm(root);
        }

        void BuildConfirm(Transform root)
        {
            const int o = RunUi.Order + 20;   // over the cards and their text
            confirmRoot = new GameObject("ConfirmSkip").transform;
            confirmRoot.SetParent(root, false);
            var p = ConfirmPanel;
            PixelRect.Set(PixelRect.Create(confirmRoot, "Shade", art.Pixel, new Color32(16, 16, 26, 200), o), -135, -240, 135, 240);
            PixelRect.Set(PixelRect.Create(confirmRoot, "Border", art.Pixel, Palette.WallWarning, o + 1), p.xMin, p.yMin, p.xMax, p.yMax);
            PixelRect.Set(PixelRect.Create(confirmRoot, "Fill", art.Pixel, RunUi.Panel, o + 2), p.xMin + 1, p.yMin + 1, p.xMax - 1, p.yMax - 1);
            PixelText.Create(confirmRoot, "Question", art.Font, new Vector2(0, p.yMax - 22), PixelText.Align.Center, 2, o + 4).Set("SKIP FREE PICK?", Palette.WallWarning);
            PixelText.Create(confirmRoot, "Info", art.Font, new Vector2(0, p.yMax - 34), PixelText.Align.Center, 1, o + 4).Set("THE FREE CARD IS LOST", Palette.TextDim);
            PixelButton.Create(confirmRoot, "Yes", art, ConfirmYesArea, "YES", 2, Palette.Floor, Palette.WallWarning, Palette.WallWarning, o + 3).Clicked += () => Confirm(true);
            PixelButton.Create(confirmRoot, "No", art, ConfirmNoArea, "NO", 2, Palette.Floor, Palette.Text, Palette.Text, o + 3).Clicked += () => Confirm(false);
            confirmRoot.gameObject.SetActive(false);
        }

        /// <summary>Sprite radius the preview icons are scaled from (the sim radius 24 of a boss = x1.5).</summary>
        float IconNativeRadius => art != null && art.BallFlash != null ? art.BallFlash.rect.width / art.BallFlash.pixelsPerUnit * 0.5f : 16f;
        const float ArenaPreviewX = 92f;
        const float ArenaPreviewY = 160f;
        const float ArenaPreviewWidth = 46f;   // the widest arena (260) maps to this many pixels

        public void Show(RunState state)
        {
            run = state;
            var entering = !onScreen || !gameObject.activeSelf;
            onScreen = true;
            gameObject.SetActive(true);
            if (entering) OnEnter();
            var boss = run.IsBossFight;
            header.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (boss ? "  BOSS" : ""), boss ? Palette.WallWarning : Palette.Text);
            purse.Set($"LIVES {run.Lives}   COINS {run.Coins}", Palette.Text);

            // Numbers exactly as the fight starts: the same match RunFlow will create, spawn hooks (Heavy, Glass Cannon) applied.
            var preview = run.Over ? null : run.CreateMatch();
            var you = preview?.Balls[0];
            var foe = preview?.Balls[1];

            var enemy = run.Enemy;
            enemyLabel.Set(boss ? "GIANT BOSS" : "ENEMY", boss ? Palette.WallWarning : Palette.TextDim);
            SetBall(enemyIcon, enemyName, enemy.WeaponId, (foe != null ? foe.Radius : (enemy.Radius > 0f ? enemy.Radius : IconNativeRadius)) / IconNativeRadius);
            enemyTraits.Set(CardText.Traits(enemy), Palette.Text);
            enemyStats.Set(EnemyHpText(foe != null ? foe.Hp : enemy.MaxHp, foe != null ? foe.MaxHp : enemy.MaxHp), Palette.TextDim);

            SetBall(youIcon, youName, run.Build.WeaponId, (you != null ? you.Radius : IconNativeRadius) / IconNativeRadius);
            youTraits.Set(CardText.Traits(run.Build), Palette.Text);
            youStats.Set(you != null ? CardText.Stats(run.Build, you.Hp, you.MaxHp) : CardText.Stats(run.Build), Palette.TextDim);
            ShowArena(run.ArenaId);

            var cost = run.NextPickCost;
            pickHint.Set(cost < 0 ? "NO MORE PICKS - FIGHT!" : cost == 0 ? "PICK ONE CARD - FREE" : $"BUY ONE MORE: {cost} COINS",
                         cost > 0 && run.Coins < cost ? Palette.TextDim : Palette.WallWarning);

            for (var i = 0; i < cards.Length; i++)
            {
                var has = i < run.OfferCount;
                cards[i].gameObject.SetActive(has);
                if (!has) continue;
                var card = run.OfferCard(i);
                cardDetails[i].Set(CardText.Detail(card), Palette.TextDim);
                cardDetails2[i].Set(CardText.Detail2(card), Palette.TextDim);
                StyleCard(i);
            }

            var rerollWhy = RerollBlockReason(run);
            reroll.SetText($"REROLL {RunTuning.RerollCost}", rerollWhy == null ? Palette.Text : Palette.HpBack);
            reroll.SetColors(Palette.Floor, rerollWhy == null ? Palette.TextDim : Palette.HpBack);
            rerollReason.Set(rerollWhy ?? "", Blocked);
            if (confirmRoot.gameObject.activeSelf && !ShouldConfirmSkip(run)) confirmRoot.gameObject.SetActive(false);
        }

        /// <summary>Enemy HP line: the HP the fight really starts with (Heavy, Glass Cannon, boss HP all applied).</summary>
        public static string EnemyHpText(float hp, float maxHp)
        {
            var h = (int)Math.Ceiling(hp);
            var m = (int)Math.Ceiling(maxHp);
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
            foreach (var b in GetComponentsInChildren<PixelButton>(false)) if (!b.transform.IsChildOf(confirmRoot)) b.NoteShown();
        }

        /// <summary>Card look: available (kind color), taken (green fill + TAKEN), blocked (grey + reason in red).</summary>
        void StyleCard(int i)
        {
            if (run == null || i >= run.OfferCount) return;
            var card = run.OfferCard(i);
            var taken = run.IsPicked(i);
            var why = taken ? null : BlockReason(run, i);
            var blink = cardBlink[i] > 0f;
            if (taken)
            {
                cards[i].SetColors(TakenFill, blink ? Palette.Text : TakenAccent);
                cardTitles[i].Set(CardText.Title(card), TakenAccent);
                cardTags[i].Set("TAKEN", blink ? Palette.Text : TakenAccent);
            }
            else if (why != null)
            {
                cards[i].SetColors(Palette.Floor, blink ? Blocked : Palette.HpBack);
                cardTitles[i].Set(CardText.Title(card), Palette.TextDim);
                cardTags[i].Set(why, blink ? Palette.Text : Blocked);
            }
            else
            {
                cards[i].SetColors(Palette.Floor, KindColor(card.Kind));
                cardTitles[i].Set(CardText.Title(card), Palette.Text);
                cardTags[i].Set("", Palette.TextDim);
            }
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
            if (run.Coins < RunTuning.RerollCost) return "NOT ENOUGH COINS";
            return "LOCKED";
        }

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
                return;
            }
            RerollRequested?.Invoke();
        }

        void OnFightClicked()
        {
            if (ConfirmOpen) return;
            if (ShouldConfirmSkip(run))
            {
                confirmRoot.gameObject.SetActive(true);
                PixelButton.NoteShownAll(confirmRoot);   // YES/NO only take taps that start after the dialog opened
                return;
            }
            FightRequested?.Invoke();
        }

        void Confirm(bool yes)
        {
            if (!ConfirmOpen) return;
            confirmRoot.gameObject.SetActive(false);
            NoteShownUnderDialog();   // the tap on YES/NO must not go on to a card under it
            if (yes) FightRequested?.Invoke();
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
                    rerollReason.Set(why ?? "", Blocked);
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
            var scale = ArenaPreviewWidth / ArenaTuning.WideWidth;
            var halfW = Mathf.Round(config.ArenaWidth * scale * 0.5f);
            var halfH = Mathf.Round(config.ArenaHeight * scale * 0.5f);
            var wall = layout.WallDamage > 0f ? new Color32(210, 60, 60, 255) : Palette.Wall;
            Rect(wall, -halfW - 1, -halfH - 1, halfW + 1, halfH + 1, 3);
            Rect(Palette.Floor, -halfW, -halfH, halfW, halfH, 4);
            if (config.MinArenaWidth < config.ArenaWidth && arenaId == ArenaRegistry.Tight)
            {
                var mw = Mathf.Round(config.MinArenaWidth * scale * 0.5f);
                var mh = Mathf.Round(config.MinArenaHeight * scale * 0.5f);
                Rect(Palette.HpBack, -mw, -mh, mw, mh, 5);   // where the shrinking walls end
            }
            for (var i = 0; i < layout.ObstacleCount; i++)
            {
                var o = layout.Get(i);
                var r = Mathf.Max(1f, Mathf.Round(o.Radius * scale));
                var color = o.Damage > 0f ? new Color32(210, 60, 60, 255) : o.Boost > 0f ? new Color32(240, 170, 60, 255) : new Color32(120, 130, 150, 255);
                var x = Mathf.Round(o.A.X * scale);
                var y = Mathf.Round(o.A.Y * scale);
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
            icon.sprite = art.Get(weaponId).Ball;
            icon.transform.localScale = new Vector3(scale, scale, 1f);
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
            if (confirmRoot != null) confirmRoot.gameObject.SetActive(false);
            onScreen = false;
            gameObject.SetActive(false);
        }

        // ---- Automation hooks and read-back (tests, MCP playthroughs). They go through the same paths as a tap. ----
        public void PressCard(int i) => OnCardClicked(i);
        public void PressReroll() => OnRerollClicked();
        public void PressFight() => OnFightClicked();
        public void PressConfirm(bool yes) => Confirm(yes);
        public bool ConfirmOpen => confirmRoot != null && confirmRoot.gameObject.activeSelf;
        public PixelButton Card(int i) => cards[i];
        public string CardTitle(int i) => cardTitles[i].Text;
        public PixelText CardTag(int i) => cardTags[i];
        public string RerollReason => rerollReason.Text;
        public string YouStats => youStats.Text;
        public string EnemyStats => enemyStats.Text;
        public string ArenaLabel => arenaName.Text;
        public int ArenaPreviewPieces => arenaRoot.childCount;
        public SpriteRenderer EnemyIcon => enemyIcon;
        public SpriteRenderer YouIcon => youIcon;
    }
}
