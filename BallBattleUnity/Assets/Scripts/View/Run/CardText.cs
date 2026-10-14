using System.Collections.Generic;
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
        /// <summary>
        /// Widest card text in pixels at scale 1 (BB-FONT1: was a character count): from the text column (x -86, right of
        /// the icon) to 2 px inside the card's right edge (x 123). A title wider than half of it wraps onto two 1x lines.
        /// </summary>
        public const int MaxTitleWidth = 209;
        public const int MaxDetailWidth = 209;

        public static string Title(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitRegistry.Get(c.Id).DisplayName + (c.Level > 1 ? " LV2" : "");
                case CardKind.DamageUp: return $"DAMAGE +{Pct(RunTuning.DamageCardPct.ToFloat())}%";
                case CardKind.SpeedUp: return $"SPEED CAP +{Pct(RunTuning.SpeedCardPct.ToFloat())}%";
                case CardKind.SwapWeapon: return "SWAP TO " + WeaponNames.Of(c.Id);
                case CardKind.SkillUp: return "SKILL UP";
                case CardKind.Heal: return $"HEAL {Pct(RunTuning.HealCardPct.ToFloat())}%";
                case CardKind.MaxHp: return $"MAX HP +{FixMath.FloorToInt(RunTuning.MaxHpCardBonus)}";
                default: return c.Kind.ToString().ToUpperInvariant();
            }
        }

        /// <summary>First detail line.</summary>
        public static string Detail(Card c)
        {
            switch (c.Kind)
            {
                case CardKind.Trait: return TraitLine1(c.Id, c.Level);
                case CardKind.DamageUp: return $"MELEE BODY AND SHOT HITS +{Pct(RunTuning.DamageCardPct.ToFloat())}%";
                case CardKind.SpeedUp: return $"TOP SPEED +{Pct(RunTuning.SpeedCardPct.ToFloat())}%. START SPEED SAME";
                case CardKind.SwapWeapon: return "NEW WEAPON. KEEPS TRAITS AND STATS";
                case CardKind.SkillUp: return c.Level == 2 ? "CHOOSE YOUR SKILL BRANCH" : "STRENGTHEN YOUR BRANCH TO LV 3";
                case CardKind.Heal: return $"HEAL {Pct(RunTuning.HealCardPct.ToFloat())}% OF MAX HP NOW";
                case CardKind.MaxHp:
                    var hp = FixMath.FloorToInt(RunTuning.MaxHpCardBonus);
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
                case CardKind.SkillUp: return "ONE SKILL. AUTO TRIGGER";
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
                    return $"+{Pct(TraitTuning.HeavyMaxHpPct[i].ToFloat())}% MAX HP  SIZE X{Num(TraitTuning.HeavyRadiusScale[i].ToFloat())}  PUSH -{Pct(TraitTuning.HeavyKnockbackResist[i].ToFloat())}%";
                case VampireTrait.TraitId:
                    return $"HEAL {Pct(TraitTuning.VampireHealPct[i].ToFloat())}% OF HIT DAMAGE";
                case SpikyTrait.TraitId:
                    return $"BODY BUMP: {Num(TraitTuning.SpikyContactDamage[i].ToFloat())} DMG. ONCE PER {Secs(TraitTuning.SpikyCooldownTicks)}S";
                case ThornsTrait.TraitId:
                    return $"RETURN {Pct(TraitTuning.ThornsReflectPct[i].ToFloat())}% OF MELEE DMG TAKEN";
                case SecondWindTrait.TraitId:
                    // Old wording on purpose until the playtest trigger reports (no heal at 9/135, LV1 healing 28) are explained.
                    // Planned text once the code is confirmed: "ONCE: AT 30% HP HEAL 20" / "OR SURVIVE A KO AT 20 HP".
                    return level > 1 ? "UPGRADE: STRONGER" : TraitRegistry.Get(id).Blurb;
                case GlassCannonTrait.TraitId:
                    return $"HITS X{Num(TraitTuning.GlassCannonDamageMul[i].ToFloat())} DMG  MAX HP -{Num(TraitTuning.GlassCannonHpLoss[i].ToFloat())}";
                case ParryMasterTrait.TraitId:
                    var every = TraitTuning.ParryMasterEvery[i];
                    return (every <= 1 ? "EVERY PARRY" : $"EVERY {Ordinal(every)} PARRY") + " GROWS YOUR WEAPON";
                case BubbleTrait.TraitId:
                    return $"SHIELD EVERY {Secs(TraitTuning.BubbleIntervalTicks[i])}S. HOLDS 1";
                case TwinBladeTrait.TraitId:
                    return $"2ND BLADE BEHIND  {Pct(TraitTuning.TwinBladeScale[i].ToFloat())}% LONG";   // "blade" = the weapon's edge here, not the weapon id
                case PoisonTipTrait.TraitId:
                    return $"HITS POISON {Num(TraitTuning.PoisonTipDps[i].ToFloat())}/S FOR {TraitTuning.PoisonTipSeconds[i]}S";
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
                case GlassCannonTrait.TraitId: return $"MELEE AND SHOTS. MAX HP AT LEAST {Num(TraitTuning.GlassCannonMinHp.ToFloat())}";
                case ParryMasterTrait.TraitId: return "NOT FOR " + WeaponNames.Of(BrawlerRule.WeaponId);   // the only bladeless weapon
                case BubbleTrait.TraitId: return "BLOCKS 1 HIT OR SHOT. NOT POISON";
                case TwinBladeTrait.TraitId: return $"ALL MELEE HITS X{Num(TraitTuning.TwinBladeDamageMul[i].ToFloat())} DMG";
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

        /// <summary>
        /// <see cref="Traits"/> broken between traits into lines at most <paramref name="maxWidth"/> px wide (scale 1), never
        /// inside a trait name. Three traits always fit two lines of the Prepare / result boxes.
        /// </summary>
        public static List<string> TraitLines(RunBuild b, int maxWidth)
        {
            if (b.Traits.Count == 0) return new List<string> { Traits(b) };
            return TextWrap.Items(TraitNames(b), "", " ", maxWidth);
        }

        /// <summary>One entry per trait, as <see cref="Traits"/> writes it ("SECOND WIND 2"). Empty without traits.</summary>
        public static List<string> TraitNames(RunBuild b)
        {
            var names = new List<string>();
            foreach (var t in b.Traits) names.Add(TraitRegistry.Get(t.Id).DisplayName + (t.Level > 1 ? " " + t.Level : ""));
            return names;
        }

        public static string Stats(RunBuild b) => Stats(b, b.Hp.ToFloat(), b.MaxHp.ToFloat());

        /// <summary>Build stats with the HP the fight will really start with (after Heavy, Glass Cannon...).</summary>
        public static string Stats(RunBuild b, float hp, float maxHp)
        {
            var s = "HP " + HpText.Pair(hp, maxHp);
            if (b.DamagePct.ToFloat() > 0f) s += $"  DMG +{Pct(b.DamagePct.ToFloat())}%";
            if (b.SpeedPct.ToFloat() > 0f) s += $"  SPD +{Pct(b.SpeedPct.ToFloat())}%";
            return s;
        }

        static int Pct(float f) => (int)System.Math.Round(f * 100f);
        static string Num(float f) => f.ToString("0.##", CultureInfo.InvariantCulture);
        static string Secs(int ticks) => Num((float)ticks / MatchConfig.TicksPerSecond);
        static string Ordinal(int n) => n + (n == 2 ? "ND" : n == 3 ? "RD" : "TH");
    }
}
