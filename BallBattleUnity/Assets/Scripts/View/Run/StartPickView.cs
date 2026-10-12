using System;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>The free card before fight one, or the single free card before a retry.</summary>
    public sealed class StartPickView : MonoBehaviour
    {
        public event Action<int> CardPicked;
        ArtLibrary art;
        PixelText title1, title2, nextFight, weapon;
        SpriteRenderer weaponIcon;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];
        readonly SpriteRenderer[] frames = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] stripes = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] icons = new SpriteRenderer[RunTuning.OfferSize];
        readonly PixelText[] names = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] detail1 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] detail2 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] rarity = new PixelText[RunTuning.OfferSize];

        public static Rect CardArea(int i) => new Rect(-125, 80 - i * 76, 250, 66);

        public static StartPickView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "StartPick", art);
            var view = root.gameObject.AddComponent<StartPickView>();
            view.art = art;
            view.title1 = RunUi.Text(root, art, -110, 206, 3, PixelText.Align.Left);
            view.title2 = RunUi.Text(root, art, -110, 184, 3, PixelText.Align.Left);
            RunUi.Text(root, art, -110, 168, 1, PixelText.Align.Left, "FREE PICK 1 OF 3", Palette.TextDim);
            PixelRect.Set(PixelRect.Create(root, "HeadingRule", art.Pixel, Palette.Wall, RunUi.Order + 3), -110, 159, 110, 160);
            view.weaponIcon = RunUi.Icon(root, new Vector2(98, 202), RunUi.Order + 3);
            view.weapon = RunUi.Text(root, art, 98, 178, 1);
            for (var i = 0; i < view.cards.Length; i++)
            {
                var index = i;
                var area = CardArea(i);
                view.cards[i] = RunUi.Button(root, art, area, " ", 1, Palette.Wall, () => view.CardPicked?.Invoke(index));
                var card = view.cards[i].transform;
                card.Find("Label").gameObject.SetActive(false); // PixelButton retains its accessible title; the card face draws it.
                var frameGo = new GameObject("Frame");
                frameGo.transform.SetParent(card, false);
                frameGo.transform.localPosition = new Vector3(area.center.x, area.center.y, 0);
                view.frames[i] = frameGo.AddComponent<SpriteRenderer>();
                view.frames[i].sortingOrder = view.cards[i].IconOrder;
                view.stripes[i] = PixelRect.Create(card, "Stripe", art.Pixel, Palette.Text, view.cards[i].IconOrder + 1);
                PixelRect.Set(view.stripes[i], area.xMin + 2, area.yMin + 2, area.xMin + 6, area.yMax - 2);
                var iconGo = new GameObject("Icon");
                iconGo.transform.SetParent(card, false);
                iconGo.transform.localPosition = new Vector3(-100, area.yMax - 21, 0);
                iconGo.transform.localScale = new Vector3(2, 2, 1);
                view.icons[i] = iconGo.AddComponent<SpriteRenderer>();
                view.icons[i].sortingOrder = view.cards[i].IconOrder + 1;
                var order = view.cards[i].IconOrder + 2;
                view.names[i] = PixelText.Create(card, "Title", art.Font, new Vector2(-83, area.yMax - 17), PixelText.Align.Left, 2, order);
                view.detail1[i] = PixelText.Create(card, "Detail1", art.Font, new Vector2(-83, area.yMax - 31), PixelText.Align.Left, 1, order);
                view.detail2[i] = PixelText.Create(card, "Detail2", art.Font, new Vector2(-83, area.yMax - 40), PixelText.Align.Left, 1, order);
                view.rarity[i] = PixelText.Create(card, "Rarity", art.Font, new Vector2(-83, area.yMin + 7), PixelText.Align.Left, 1, order);
                PixelText.Create(card, "TapToPick", art.Font, new Vector2(35, area.yMin + 7), PixelText.Align.Left, 1, order).Set("TAP TO PICK", Palette.TextDim);
            }
            PixelRect.Set(PixelRect.Create(root, "FooterBand", art.Pixel, RunUi.Panel, RunUi.Order + 2), -127, -122, 127, -99);
            RunUi.Text(root, art, -119, -107, 1, PixelText.Align.Left, "NO COINS  NO REROLL", Palette.TextDim);
            view.nextFight = RunUi.Text(root, art, -119, -116, 1, PixelText.Align.Left, "YOUR FIRST FIGHT STARTS AFTER THIS", Palette.TextDim);
            view.Hide();
            return view;
        }

        public void Show(RunState run)
        {
            gameObject.SetActive(true);
            title1.Set("CHOOSE", Palette.WallWarning);
            title2.Set(run.IsRetry ? "YOUR RETRY" : "YOUR START", Palette.WallWarning);
            nextFight.Set(run.IsRetry ? "YOUR RETRY FIGHT STARTS AFTER THIS" : "YOUR FIRST FIGHT STARTS AFTER THIS", Palette.TextDim);
            weapon.Set(RunUi.WeaponName(run.Build.WeaponId), Palette.Look(run.Build.WeaponId).Body);
            weaponIcon.sprite = art.Get(run.Build.WeaponId).Ball;
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i].gameObject.SetActive(i < run.OfferCount);
                if (i >= run.OfferCount) continue;
                var face = CardFace.For(run.OfferCard(i), art);
                cards[i].SetColors(RunUi.Panel, face.Stripe);
                frames[i].sprite = art.Frame(face.Frame);
                frames[i].enabled = frames[i].sprite != null;
                if (frames[i].enabled)
                {
                    frames[i].drawMode = SpriteDrawMode.Sliced;
                    frames[i].size = CardArea(i).size;
                }
                stripes[i].color = face.Stripe;
                icons[i].sprite = face.Icon;
                icons[i].enabled = face.Icon != null;
                names[i].Set(face.Title, Palette.Text);
                cards[i].SetText(face.Title, Palette.Text);
                detail1[i].SetHighlighted(face.Detail1, Palette.Text, CardFace.NumberColor);
                detail2[i].SetHighlighted(face.Detail2, Palette.Text, CardFace.NumberColor);
                rarity[i].Set(face.Frame.ToString().ToUpperInvariant(), Palette.TextDim);
            }
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
