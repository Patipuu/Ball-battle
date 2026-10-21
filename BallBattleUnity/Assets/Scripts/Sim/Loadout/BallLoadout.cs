using System.Collections.Generic;

namespace BallBattle.Sim
{
    /// <summary>Flat stat bonuses from run cards. 0 = none. Fractions: 0.1 = +10%.</summary>
    public struct StatBonus
    {
        /// <summary>Multiplies every outgoing weapon/projectile hit: damage * (1 + DamagePct). BB-BAL3: also SPORE's own poison dps and
        /// MIRROR's reflects.</summary>
        public Fix DamagePct;
        /// <summary>Multiplies the ball's speed cap: (MaxSpeed + weapon bonus) * (1 + SpeedPct).</summary>
        public Fix SpeedPct;

        public ulong HashInto(ulong h) => SimHash.Mix(SimHash.Mix(h, DamagePct), SpeedPct);
    }

    /// <summary>
    /// Everything one ball brings into a match: weapon, traits, stat bonus, HP and size.
    /// Weapon and trait instances belong to one ball in one match, so a loadout is single-use: the run layer
    /// rebuilds it (from ids/levels) for every fight. MaxHp/Radius &lt;= 0 = MatchConfig defaults (StartHp,
    /// BallRadius); Hp &lt; 0 = full HP; Hp = 0 is rejected (a ball cannot start a fight dead).
    /// </summary>
    public sealed class BallLoadout
    {
        public WeaponRule Weapon;
        public readonly List<TraitRule> Traits = new List<TraitRule>(4);
        public StatBonus Bonus;
        public Fix MaxHp = -1;
        /// <summary>HP at match start (Run mode carries HP between fights). Clamped to MaxHp.</summary>
        public Fix Hp = -1;
        public Fix Radius = -1;
        /// <summary>BB-04 boss mechanic (Run boss fights only); null = none. Single-use like weapons and traits.</summary>
        public Boss.BossRule Boss;
        public int SkillLevel = 1;
        public Skills.SkillBranch SkillBranch;
        /// <summary>TAP-1: this ball accepts MatchSim.Tap (the Run player). False = never (enemies, Versus).</summary>
        public bool TapEnabled;
        /// <summary>Run enemies: the sim presses this ball's active skill by itself (needs TapEnabled), so a bot EMBER lays the same fire
        /// trail as the player's. False = the old automatic skill rule.</summary>
        public bool AutoSkill;
        /// <summary>AutoSkill timing: first cast at this ActiveTick, then this many ticks after the skill is ready again.</summary>
        public int BotFirstTick = 240, BotRepeatTicks = 120;
        /// <summary>BB-12g: the Run player's skill tree (in-match nodes); null = none (Versus, enemies): exactly the old rules.</summary>
        public TreeLoadout Tree;

        public BallLoadout(WeaponRule weapon) => Weapon = weapon;

        public BallLoadout With(params TraitRule[] traits)
        {
            Traits.AddRange(traits);
            return this;
        }
    }
}
