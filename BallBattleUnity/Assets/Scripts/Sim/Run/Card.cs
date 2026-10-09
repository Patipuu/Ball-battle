using System;

namespace BallBattle.Sim.Run
{
    public enum CardKind : byte
    {
        /// <summary>New trait at level 1, or an owned trait to level 2.</summary>
        Trait,
        DamageUp,
        SpeedUp,
        /// <summary>Replace the weapon (Id = new weapon).</summary>
        SwapWeapon,
        /// <summary>Heal RunTuning.HealCardPct of max HP.</summary>
        Heal,
        /// <summary>+RunTuning.MaxHpCardBonus max HP (and HP).</summary>
        MaxHp
    }

    /// <summary>One choice offered before a fight.</summary>
    public readonly struct Card : IEquatable<Card>
    {
        public readonly CardKind Kind;
        /// <summary>Trait or weapon id; null for stat/heal cards.</summary>
        public readonly string Id;
        /// <summary>Trait level after taking the card (1 = new, 2 = upgrade).</summary>
        public readonly int Level;

        public Card(CardKind kind, string id = null, int level = 0)
        {
            Kind = kind;
            Id = id;
            Level = level;
        }

        /// <summary>Applies the card to a build (assumes it was offered for this build).</summary>
        public void ApplyTo(RunBuild b)
        {
            switch (Kind)
            {
                case CardKind.Trait:
                    foreach (var t in b.Traits)
                        if (t.Id == Id) { t.Level = Level; return; }
                    b.Traits.Add(new TraitSlot(Id, Level));
                    break;
                case CardKind.DamageUp: b.DamagePct += RunTuning.DamageCardPct; break;
                case CardKind.SpeedUp: b.SpeedPct += RunTuning.SpeedCardPct; break;
                case CardKind.SwapWeapon: b.WeaponId = Id; break;
                case CardKind.Heal: b.SetHp(b.Hp + b.MaxHp * RunTuning.HealCardPct); break;
                case CardKind.MaxHp:
                    b.MaxHp += RunTuning.MaxHpCardBonus;
                    b.Hp += RunTuning.MaxHpCardBonus;
                    break;
            }
        }

        public bool Equals(Card o) => Kind == o.Kind && Id == o.Id && Level == o.Level;
        public override bool Equals(object obj) => obj is Card c && Equals(c);
        public override int GetHashCode() => ((int)Kind * 397) ^ (Id?.GetHashCode() ?? 0) ^ (Level << 8);
        public override string ToString() => Id == null ? Kind.ToString() : $"{Kind}:{Id}:{Level}";
    }
}
