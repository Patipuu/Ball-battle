using System;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>The free, single card choice before fight one.</summary>
    public sealed class StartPickView : MonoBehaviour
    {
        public event Action<int> CardPicked;
        ArtLibrary art;
        PixelText heading;
        PixelText nextFight;
        PixelText weapon;
        SpriteRenderer weaponIcon;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];

        public static StartPickView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "StartPick", art);
            var view = root.gameObject.AddComponent<StartPickView>();
            view.art = art;
            view.heading = RunUi.Text(root, art, 0, 178, 2, initial: "CHOOSE YOUR START");
            RunUi.Text(root, art, 0, 145, 1, initial: "YOUR WEAPON", color: Palette.TextDim);
            view.weaponIcon = RunUi.Icon(root, new Vector2(0, 102), RunUi.Order + 3);
            view.weapon = RunUi.Text(root, art, 0, 70, 2);
            RunUi.Text(root, art, 0, 43, 1, initial: "FREE PICK 1 OF 3", color: Palette.WallWarning);
            for (var i = 0; i < view.cards.Length; i++)
            {
                var index = i;
                view.cards[i] = RunUi.Button(root, art, new Rect(-122, -8 - i * 43, 244, 37), " ", 1,
                    Palette.Floor, () => view.CardPicked?.Invoke(index));
            }
            RunUi.Text(root, art, 0, -168, 1, initial: "NO COINS  NO REROLL", color: Palette.TextDim);
            view.nextFight = RunUi.Text(root, art, 0, -181, 1, initial: "FIRST FIGHT STARTS AFTER THIS", color: Palette.TextDim);
            view.Hide();
            return view;
        }

        public void Show(RunState run)
        {
            gameObject.SetActive(true);
            heading.Set(run.IsRetry ? "CHOOSE YOUR RETRY" : "CHOOSE YOUR START", Palette.Text);
            nextFight.Set(run.IsRetry ? "RETRY FIGHT STARTS AFTER THIS" : "FIRST FIGHT STARTS AFTER THIS", Palette.TextDim);
            weapon.Set(RunUi.WeaponName(run.Build.WeaponId), Palette.Look(run.Build.WeaponId).Body);
            weaponIcon.sprite = art.Get(run.Build.WeaponId).Ball;
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(i < run.OfferCount);
                if (i < run.OfferCount) cards[i].SetText(CardText.Title(run.OfferCard(i)), Palette.Text);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
