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
        PixelButton reroll;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];
        readonly PixelText[] cardTitles = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] cardDetails = new PixelText[RunTuning.OfferSize];

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
                cardTitles[i] = PixelText.Create(cards[i].transform, "Title", art.Font, new Vector2(-117, area.yMin + 20), PixelText.Align.Left, 2, cards[i].IconOrder + 1);
                cardDetails[i] = PixelText.Create(cards[i].transform, "Detail", art.Font, new Vector2(-117, area.yMin + 7), PixelText.Align.Left, 1, cards[i].IconOrder + 1);
            }

            reroll = RunUi.Button(root, art, new Rect(-125, -130, 110, 30), "REROLL", 1, Palette.TextDim, () => RerollRequested?.Invoke());
            RunUi.Button(root, art, new Rect(-5, -130, 130, 30), "FIGHT!", 3, Palette.WallWarning, () => FightRequested?.Invoke());
            RunUi.Text(root, art, 0, -150, 1, initial: "ESC: MENU   THE RUN IS SAVED", color: Palette.TextDim);
        }

        static readonly float DefaultRadius = new MatchConfig().BallRadius;

        public void Show(RunState run)
        {
            gameObject.SetActive(true);
            var boss = run.IsBossFight;
            header.Set($"FIGHT {run.FightIndex + 1}/{RunTuning.Fights}" + (boss ? "  BOSS" : ""), boss ? Palette.WallWarning : Palette.Text);
            purse.Set($"LIVES {run.Lives}   COINS {run.Coins}", Palette.Text);

            var enemy = run.Enemy;
            enemyLabel.Set(boss ? "GIANT BOSS" : "ENEMY", boss ? Palette.WallWarning : Palette.TextDim);
            SetBall(enemyIcon, enemyName, enemy.WeaponId, enemy.Radius > 0f ? enemy.Radius / DefaultRadius : 1f);
            enemyTraits.Set(CardText.Traits(enemy), Palette.Text);
            enemyStats.Set($"HP {(int)enemy.MaxHp}", Palette.TextDim);

            SetBall(youIcon, youName, run.Build.WeaponId, 1f);
            youTraits.Set(CardText.Traits(run.Build), Palette.Text);
            youStats.Set(CardText.Stats(run.Build), Palette.TextDim);

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
            }

            reroll.SetText($"REROLL {RunTuning.RerollCost}", run.CanReroll ? Palette.Text : Palette.HpBack);
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
