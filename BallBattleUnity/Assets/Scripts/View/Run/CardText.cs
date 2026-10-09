using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.View
{
    /// <summary>Card and build wording for the 3x5 font (A-Z 0-9 space . - : / % + ! ?).</summary>
    public static class CardText
    {
        public static string Title(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitRegistry.Get(c.Id).DisplayName + (c.Level > 1 ? " LV2" : "");
                case CardKind.DamageUp: return $"DAMAGE +{Pct(RunTuning.DamageCardPct)}%";
                case CardKind.SpeedUp: return $"SPEED +{Pct(RunTuning.SpeedCardPct)}%";
                case CardKind.SwapWeapon: return "SWAP TO " + WeaponRegistry.Get(c.Id).DisplayName;
                case CardKind.Heal: return $"HEAL {Pct(RunTuning.HealCardPct)}%";
                case CardKind.MaxHp: return $"MAX HP +{(int)RunTuning.MaxHpCardBonus}";
                default: return c.Kind.ToString().ToUpperInvariant();
            }
        }

        public static string Detail(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return c.Level > 1 ? "UPGRADE: STRONGER" : TraitRegistry.Get(c.Id).Blurb;
                case CardKind.DamageUp: return "ALL HITS DEAL MORE";
                case CardKind.SpeedUp: return "HIGHER SPEED CAP";
                case CardKind.SwapWeapon: return "KEEPS TRAITS AND STATS";
                case CardKind.Heal: return "NOW. HP CARRIES OVER";
                case CardKind.MaxHp: return "AND HEAL THE SAME";
                default: return "";
            }
        }

        public static string Traits(RunBuild b)
        {
            if (b.Traits.Count == 0) return "NO TRAITS";
            var s = "";
            foreach (var t in b.Traits)
            {
                if (s.Length > 0) s += " ";
                s += TraitRegistry.Get(t.Id).DisplayName + (t.Level > 1 ? "2" : "");
            }
            return s;
        }

        public static string Stats(RunBuild b)
        {
            var s = $"HP {(int)System.Math.Ceiling(b.Hp)}/{(int)b.MaxHp}";
            if (b.DamagePct > 0f) s += $"  DMG +{Pct(b.DamagePct)}%";
            if (b.SpeedPct > 0f) s += $"  SPD +{Pct(b.SpeedPct)}%";
            return s;
        }

        static int Pct(float f) => (int)System.Math.Round(f * 100f);
    }
}
