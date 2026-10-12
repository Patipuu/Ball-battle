using System.Globalization;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.View
{
    /// <summary>
    /// Card and build wording for the 3x5 font (A-Z 0-9 space . - : / % + ! ?).
    /// Card limits (docs/design/card-pool-v2.md, section 0): title at most 18 chars, two detail lines of at most 44 chars.
    /// Numbers are read from the tuning tables so the text cannot drift from what the sim does.
    /// </summary>
    public static class CardText
    {
        public const int MaxTitleChars = 18;
        public const int MaxDetailChars = 44;

        public static string Title(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitRegistry.Get(c.Id).DisplayName + (c.Level > 1 ? " LV2" : "");
                case CardKind.DamageUp: return $"DAMAGE +{Pct(RunTuning.DamageCardPct)}%";
                case CardKind.SpeedUp: return $"SPEED CAP +{Pct(RunTuning.SpeedCardPct)}%";
                case CardKind.SwapWeapon: return "SWAP TO " + WeaponNames.Of(c.Id);
                case CardKind.Heal: return $"HEAL {Pct(RunTuning.HealCardPct)}%";
                case CardKind.MaxHp: return $"MAX HP +{(int)RunTuning.MaxHpCardBonus}";
                default: return c.Kind.ToString().ToUpperInvariant();
            }
        }

        /// <summary>First detail line.</summary>
        public static string Detail(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitLine1(c.Id, c.Level);
                case CardKind.DamageUp: return $"MELEE BODY AND SHOT HITS +{Pct(RunTuning.DamageCardPct)}%";
                case CardKind.SpeedUp: return $"TOP SPEED +{Pct(RunTuning.SpeedCardPct)}%. START SPEED SAME";
                case CardKind.SwapWeapon: return "NEW WEAPON. KEEPS TRAITS AND STATS";
                case CardKind.Heal: return $"HEAL {Pct(RunTuning.HealCardPct)}% OF MAX HP NOW";
                case CardKind.MaxHp:
                    var hp = (int)RunTuning.MaxHpCardBonus;
                    return $"+{hp} MAX HP AND +{hp} HP NOW";
                default: return "";
            }
        }

        /// <summary>Second detail line ("" = none).</summary>
        public static string Detail2(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitLine2(c.Id, c.Level);
                case CardKind.DamageUp: return "NOT POISON SPIKES OR THORNS";
                case CardKind.SpeedUp: return WeaponNames.Of(BrawlerRule.WeaponId) + ": MORE SPEED IS MORE DMG";
                case CardKind.SwapWeapon: return "ONLY OFFERED IF ALL TRAITS STILL WORK";
                case CardKind.Heal: return "HP CARRIES TO NEXT FIGHTS";
                default: return "";
            }
        }

        static string TraitLine1(string id, int level)
        {
            var i = level > 1 ? 1 : 0;
            switch (id)
            {
                case HeavyTrait.TraitId:
                    return $"+{Pct(TraitTuning.HeavyMaxHpPct[i])}% MAX HP  SIZE X{Num(TraitTuning.HeavyRadiusScale[i])}  PUSH -{Pct(TraitTuning.HeavyKnockbackResist[i])}%";
                case VampireTrait.TraitId:
                    return $"HEAL {Pct(TraitTuning.VampireHealPct[i])}% OF HIT DAMAGE";
                case SpikyTrait.TraitId:
                    return $"BODY BUMP: {Num(TraitTuning.SpikyContactDamage[i])} DMG. ONCE PER {Secs(TraitTuning.SpikyCooldownTicks)}S";
                case ThornsTrait.TraitId:
                    return $"RETURN {Pct(TraitTuning.ThornsReflectPct[i])}% OF MELEE DMG TAKEN";
                case SecondWindTrait.TraitId:
                    // Old wording on purpose until the playtest trigger reports (no heal at 9/135, LV1 healing 28) are explained.
                    // Planned text once the code is confirmed: "ONCE: AT 30% HP HEAL 20" / "OR SURVIVE A KO AT 20 HP".
                    return level > 1 ? "UPGRADE: STRONGER" : TraitRegistry.Get(id).Blurb;
                case GlassCannonTrait.TraitId:
                    return $"HITS X{Num(TraitTuning.GlassCannonDamageMul[i])} DMG  MAX HP -{Num(TraitTuning.GlassCannonHpLoss[i])}";
                case ParryMasterTrait.TraitId:
                    var every = TraitTuning.ParryMasterEvery[i];
                    return (every <= 1 ? "EVERY PARRY" : $"EVERY {Ordinal(every)} PARRY") + " GROWS YOUR WEAPON";
                case BubbleTrait.TraitId:
                    return $"SHIELD EVERY {Secs(TraitTuning.BubbleIntervalTicks[i])}S. HOLDS 1";
                case TwinBladeTrait.TraitId:
                    return $"2ND BLADE BEHIND  {Pct(TraitTuning.TwinBladeScale[i])}% LONG";   // "blade" = the weapon's edge here, not the weapon id
                case PoisonTipTrait.TraitId:
                    return $"HITS POISON {Num(TraitTuning.PoisonTipDps[i])}/S FOR {TraitTuning.PoisonTipSeconds[i]}S";
                default:
                    return level > 1 ? "UPGRADE: STRONGER" : TraitRegistry.Get(id).Blurb;
            }
        }

        static string TraitLine2(string id, int level)
        {
            var i = level > 1 ? 1 : 0;
            switch (id)
            {
                case HeavyTrait.TraitId: return level > 1 ? "NEVER BIGGER THAN A BOSS" : "BIGGER BODY IS EASIER TO HIT";
                case VampireTrait.TraitId: return "MELEE AND SHOTS. NOT POISON";
                case SpikyTrait.TraitId: return "IGNORES SHIELDS";
                case ThornsTrait.TraitId: return "NOT SHOTS OR POISON";
                case SecondWindTrait.TraitId: return "";
                case GlassCannonTrait.TraitId: return $"MELEE AND SHOTS. MAX HP AT LEAST {Num(TraitTuning.GlassCannonMinHp)}";
                case ParryMasterTrait.TraitId: return "NOT FOR " + WeaponNames.Of(BrawlerRule.WeaponId);   // the only bladeless weapon
                case BubbleTrait.TraitId: return "BLOCKS 1 HIT OR SHOT. NOT POISON";
                case TwinBladeTrait.TraitId: return $"ALL MELEE HITS X{Num(TraitTuning.TwinBladeDamageMul[i])} DMG";
                case PoisonTipTrait.TraitId: return $"SKIPS IF TARGET HAS {TraitTuning.PoisonTipMaxStacks[i]} POISON";
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
                s += TraitRegistry.Get(t.Id).DisplayName + (t.Level > 1 ? " " + t.Level : "");   // "SECOND WIND 2"
            }
            return s;
        }

        public static string Stats(RunBuild b) => Stats(b, b.Hp, b.MaxHp);

        /// <summary>Build stats with the HP the fight will really start with (after Heavy, Glass Cannon...).</summary>
        public static string Stats(RunBuild b, float hp, float maxHp)
        {
            var s = "HP " + HpText.Pair(hp, maxHp);
            if (b.DamagePct > 0f) s += $"  DMG +{Pct(b.DamagePct)}%";
            if (b.SpeedPct > 0f) s += $"  SPD +{Pct(b.SpeedPct)}%";
            return s;
        }

        static int Pct(float f) => (int)System.Math.Round(f * 100f);
        static string Num(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);
        static string Secs(int ticks) => Num((float)ticks / MatchConfig.TicksPerSecond);
        static string Ordinal(int n) => n + (n == 2 ? "ND" : n == 3 ? "RD" : "TH");
    }
}
