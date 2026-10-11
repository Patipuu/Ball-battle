using System;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Before each fight: enemy preview, your build, 3 cards (first pick free, one more for coins), REROLL, FIGHT.
    /// Redrawn from RunState on every change (rare: a tap), so building strings here is fine.
    /// </summary>
    public sealed class PrepareView : MonoBehaviour
    {
        const float CardHeight = 36f;
        static readonly float[] CardBottoms = { 0f, -42f, -84f };

        public event Action<int> CardPicked;
        public event Action RerollRequested, FightRequested;

        ArtLibrary art;
        PixelText header, purse, pickHint, enemyLabel, enemyName, enemyTraits, enemyStats, youName, youTraits, youStats;
        SpriteRenderer enemyIcon, youIcon;
        PixelText arenaName;
        Transform arenaRoot;
        string shownArena;
        PixelButton reroll;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];
        readonly PixelText[] cardTitles = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardDetails = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardDetails2 = new PixelText[RunTuning.OfferSize];

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
                var area = new Rect(-125, CardBottoms[i], 250, CardHeight);
                cards[i] = RunUi.Button(root, art, area, null, 1, Palette.Wall, () => CardPicked?.Invoke(index));
                cardTitles[i] = PixelText.Create(cards[i].transform, "Title", art.Font, new Vector2(-117, area.yMin + 22), PixelText.Align.Left, 2, cards[i].IconOrder + 1);
                cardDetails[i] = PixelText.Create(cards[i].transform, "Detail", art.Font, new Vector2(-117, area.yMin + 12), PixelText.Align.Left, 1, cards[i].IconOrder + 1);
                cardDetails2[i] = PixelText.Create(cards[i].transform, "Detail2", art.Font, new Vector2(-117, area.yMin + 4), PixelText.Align.Left, 1, cards[i].IconOrder + 1);
            }

            reroll = RunUi.Button(root, art, new Rect(-125, -130, 110, 30), "REROLL", 1, Palette.TextDim, () => RerollRequested?.Invoke());
            RunUi.Button(root, art, new Rect(-5, -130, 130, 30), "FIGHT!", 3, Palette.WallWarning, () => FightRequested?.Invoke());
            RunUi.Text(root, art, 0, -150, 1, initial: "ESC: MENU   THE RUN IS SAVED", color: Palette.TextDim);
        }

        static readonly float DefaultRadius = new MatchConfig().BallRadius;
        const float ArenaPreviewX = 92f;
        const float ArenaPreviewY = 160f;
        const float ArenaPreviewWidth = 46f;   // the widest arena (260) maps to this many pixels

        public void Show(RunState run)
        {
            gameObject.SetActive(true);
            var boss = run.IsBossFight;
            header.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (boss ? "  BOSS" : ""), boss ? Palette.WallWarning : Palette.Text);
            purse.Set($"LIVES {run.Lives}   COINS {run.Coins}", Palette.Text);

            // Numbers exactly as the fight starts: the same match RunFlow will create, spawn hooks (Heavy, Glass Cannon) applied.
            var preview = run.Over ? null : run.CreateMatch();
            var you = preview?.Balls[0];
            var foe = preview?.Balls[1];

            var enemy = run.Enemy;
            enemyLabel.Set(boss ? "GIANT BOSS" : "ENEMY", boss ? Palette.WallWarning : Palette.TextDim);
            SetBall(enemyIcon, enemyName, enemy.WeaponId, foe != null ? foe.Radius / DefaultRadius : (enemy.Radius > 0f ? enemy.Radius / DefaultRadius : 1f));
            enemyTraits.Set(CardText.Traits(enemy), Palette.Text);
            enemyStats.Set($"HP {(int)Math.Ceiling(foe != null ? foe.MaxHp : enemy.MaxHp)}", Palette.TextDim);

            SetBall(youIcon, youName, run.Build.WeaponId, you != null ? you.Radius / DefaultRadius : 1f);
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
                var taken = run.IsPicked(i);
                var can = run.CanPick(i);
                var accent = taken ? Palette.HpLost : can ? KindColor(card.Kind) : Palette.HpBack;
                cards[i].SetColors(Palette.Floor, accent);
                cardTitles[i].Set(CardText.Title(card), taken || !can ? Palette.TextDim : Palette.Text);
                cardDetails[i].Set(taken ? "TAKEN" : CardText.Detail(card), Palette.TextDim);
                cardDetails2[i].Set(taken ? "" : CardText.Detail2(card), Palette.TextDim);
            }

            reroll.SetText($"REROLL {RunTuning.RerollCost}", run.CanReroll ? Palette.Text : Palette.HpBack);
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

        public void Hide() => gameObject.SetActive(false);

        /// <summary>Automation hooks (tests, MCP playthroughs).</summary>
        public void PressCard(int i) => CardPicked?.Invoke(i);
        public void PressFight() => FightRequested?.Invoke();
    }
}
