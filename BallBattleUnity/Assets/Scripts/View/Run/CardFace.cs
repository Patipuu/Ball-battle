using BallBattle.Sim.Run;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// How one card looks (art v1, docs/design/art-ref/art4_cards_v2.png, art4_counter_v3.png): 9-slice frame by rarity,
    /// 4 px trait-colored stripe on the left, the trait icon at 2x left of the title, numbers in the description in gold.
    /// Built from a Card; the counter style (dashed frame, "1 FIGHT", "NOT A TRAIT SLOT") is ready for v4 cards.
    /// </summary>
    public sealed class CardFace
    {
        /// <summary>Numbers in card descriptions.</summary>
        public static readonly Color32 NumberColor = new Color32(250, 210, 70, 255);
        /// <summary>Counter cards: the dashed frame's teal.</summary>
        public static readonly Color32 CounterColor = new Color32(110, 200, 190, 255);
        public const string CounterTag = "1 FIGHT";
        public const string CounterNote = "NOT A TRAIT SLOT";

        public ArtLibrary.CardFrame Frame;
        public Color32 Stripe;
        public Sprite Icon;
        public string Title, Detail1, Detail2;
        /// <summary>Right-hand tag when the card can be taken ("1 FIGHT" on counters); empty otherwise.</summary>
        public string Tag = "";

        /// <summary>
        /// Rarity until cards carry one (card-pool v2): trait upgrades (LV2) are rare, new traits uncommon, every other
        /// card common.
        /// </summary>
        public static ArtLibrary.CardFrame RarityOf(Card c) =>
            c.Kind == CardKind.Trait ? (c.Level > 1 ? ArtLibrary.CardFrame.Rare : ArtLibrary.CardFrame.Uncommon) : ArtLibrary.CardFrame.Common;

        public static CardFace For(Card c, ArtLibrary art) => new CardFace
        {
            Frame = RarityOf(c),
            Stripe = c.Kind == CardKind.Trait ? TraitBadgeView.ColorOf(c.Id) : KindColor(c.Kind),
            Icon = art != null ? art.CardIcon(c) : null,   // traits and the stat cards (DAMAGE, SPEED CAP, MAX HP)
            Title = CardText.Title(c),
            Detail1 = CardText.Detail(c),
            Detail2 = CardText.Detail2(c),
        };

        /// <summary>The counter card style with ANTIDOTE's text (card-pool v2, B3). No logic behind it yet.</summary>
        public static CardFace CounterSample(ArtLibrary art) => new CardFace
        {
            Frame = ArtLibrary.CardFrame.Counter,
            Stripe = CounterColor,
            Icon = art != null ? art.CardAntidote : null,
            Title = "ANTIDOTE",
            Detail1 = "POISON DMG TAKEN -60%",
            Detail2 = CounterNote,
            Tag = CounterTag,
        };

        public static Color32 KindColor(CardKind k)
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
    }
}
