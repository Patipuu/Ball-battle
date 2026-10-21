using System;
using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>The free card before fight one, or the single free card before a retry.</summary>
    public sealed class StartPickView : MonoBehaviour
    {
        public event Action<int> CardPicked;
        public event Action<int, BallBattle.Sim.Skills.SkillBranch> SkillPicked;
        readonly SkillBranchButtons[] skillButtons = new SkillBranchButtons[RunTuning.OfferSize];
        RunState shownRun;
        ArtLibrary art;
        PixelText title1, title2, nextFight, weapon, noReroll;
        /// <summary>BB-FIX4: CHOOSE YOUR RETRY of a boss fight: the boss name and its Prepare hint (BossText.NameAndHint), under the footer.</summary>
        readonly PixelText[] bossHint = new PixelText[3];
        static readonly float[] BossHintY = { -117f, -130f, -143f };   // cells -117..-104, -130..-117, -143..-130: the footer band ends -97

        /// <summary>Footer hint "NO REROLL ON START PICK" (tests).</summary>
        public PixelText NoRerollText => noReroll;
        SpriteRenderer weaponIcon;
        readonly PixelButton[] cards = new PixelButton[RunTuning.OfferSize];
        readonly SpriteRenderer[] frames = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] stripes = new SpriteRenderer[RunTuning.OfferSize];
        readonly SpriteRenderer[] icons = new SpriteRenderer[RunTuning.OfferSize];
        readonly PixelText[] names = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] detail1 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] detail2 = new PixelText[RunTuning.OfferSize];
        readonly PixelText[] rarity = new PixelText[RunTuning.OfferSize];

        /// <summary>BB-FONT1: 70 px cards (title x2 26 + two detail rows + rarity row, 13 px cells), 6 px apart.</summary>
        public static Rect CardArea(int i) => new Rect(-125, 92 - i * 76, 250, 70);
        /// <summary>Card text starts here (1 px right of the 2x icon; the longest detail ends 1 px inside the card).</summary>
        public const float TextX = -86f;

        public static StartPickView Create(Transform parent, ArtLibrary art)
        {
            var root = RunUi.Root(parent, "StartPick", art);
            var view = root.gameObject.AddComponent<StartPickView>();
            view.art = art;
            view.title1 = RunUi.Text(root, art, -110, 212, 2, PixelText.Align.Left);   // x3 no longer fits the top: x2, cells 212..238
            view.title2 = RunUi.Text(root, art, -110, 186, 2, PixelText.Align.Left);   // 186..212
            RunUi.Text(root, art, -110, 171, 1, PixelText.Align.Left, "FREE PICK", Palette.TextDim);
            PixelRect.Set(PixelRect.Create(root, "HeadingRule", art.Pixel, Palette.Wall, RunUi.Order + 3), -110, 166, 110, 167);
            view.weaponIcon = RunUi.Icon(root, new Vector2(98, 202), RunUi.Order + 3);
            view.weapon = RunUi.Text(root, art, 98, 178, 1);
            for (var i = 0; i < view.cards.Length; i++)
            {
                var index = i;
                var area = CardArea(i);
                view.cards[i] = RunUi.Button(root, art, area, " ", 1, Palette.Wall, () =>
                {
                    if (view.shownRun?.OfferCard(index).Kind == CardKind.SkillUp && view.shownRun.EffectiveSkillLevel == 1) return;
                    view.CardPicked?.Invoke(index);
                });
                view.skillButtons[i] = new SkillBranchButtons(view.cards[i].transform, art, area, branch => view.SkillPicked?.Invoke(index, branch));
                view.cards[i].ClickCue = null;   // a taken card plays card_pick (RunFlow.CardTaken)
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
                iconGo.transform.localPosition = new Vector3(-101, area.yMax - 30, 0);   // 28 px icon ends at -87, text starts -86
                iconGo.transform.localScale = new Vector3(2, 2, 1);
                view.icons[i] = iconGo.AddComponent<SpriteRenderer>();
                view.icons[i].sortingOrder = view.cards[i].IconOrder + 1;
                var order = view.cards[i].IconOrder + 2;
                // Cells from the card top: title x2 2..28, details 28..41 and 41..54, rarity 55..68.
                view.names[i] = PixelText.Create(card, "Title", art.Font, new Vector2(TextX, area.yMax - 28), PixelText.Align.Left, 2, order);
                view.detail1[i] = PixelText.Create(card, "Detail1", art.Font, new Vector2(TextX, area.yMax - 41), PixelText.Align.Left, 1, order);
                view.detail2[i] = PixelText.Create(card, "Detail2", art.Font, new Vector2(TextX, area.yMax - 54), PixelText.Align.Left, 1, order);
                view.rarity[i] = PixelText.Create(card, "Rarity", art.Font, new Vector2(TextX, area.yMin + 2), PixelText.Align.Left, 1, order);
            }
            view.noReroll = RunUi.Text(root, art, -119, -79, 1, PixelText.Align.Left, "", Palette.TextDim);
            view.nextFight = RunUi.Text(root, art, -119, -93, 1, PixelText.Align.Left, "", Palette.TextDim);
            for (var i = 0; i < view.bossHint.Length; i++) view.bossHint[i] = RunUi.Text(root, art, 0, BossHintY[i], 1);
            view.Hide();
            return view;
        }

        public void Show(RunState run)
        {
            shownRun = run;
            gameObject.SetActive(true);
            title1.Set("CHOOSE", Palette.WallWarning);
            title2.Set(run.IsRetry ? "YOUR RETRY" : "YOUR START", Palette.WallWarning);
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
                var branchButtonsOn = skillButtons[i].Show(run, i);
                // BB-FIX4: with the branch buttons on, the 2x icon sits 1 px above their top edge (it used to dip 4 px under it).
                icons[i].transform.localPosition = new Vector3(-101, branchButtonsOn ? CardArea(i).yMin + SkillBranchButtons.Top + 1 + 14 : CardArea(i).yMax - 30, 0);
                if (branchButtonsOn)
                {
                    rarity[i].Set("", Palette.TextDim);
                    detail1[i].Set("", Palette.TextDim);
                    detail2[i].Set("", Palette.TextDim);
                }
            }

            var hint = BossHint(run);
            for (var i = 0; i < bossHint.Length; i++)
                bossHint[i].Set(i < hint.Count ? hint[i] : "", i == 0 ? Palette.WallWarning : Palette.Text);   // Prepare: name gold, hint white
        }

        /// <summary>BB-FIX4: boss name + hint lines on this screen: only on the retry pick of a boss fight; empty otherwise.</summary>
        public static System.Collections.Generic.List<string> BossHint(RunState run) =>
            run != null && run.IsRetry ? BossText.NameAndHint(run.CurrentBossId) : new System.Collections.Generic.List<string>();
        /// <summary>The boss hint lines as drawn (tests).</summary>
        public PixelText BossHintLine(int i) => bossHint[i];

        public void Hide() => gameObject.SetActive(false);
    }
}
