using System.Collections.Generic;

namespace BallBattle.Sim
{
    /// <summary>Flat stat bonuses from run cards. 0 = none. Fractions: 0.1 = +10%.</summary>
    public struct StatBonus
    {
        /// <summary>Multiplies every outgoing weapon/projectile hit: damage * (1 + DamagePct).</summary>
        public float DamagePct;
        /// <summary>Multiplies the ball's speed cap: (MaxSpeed + weapon bonus) * (1 + SpeedPct).</summary>
        public float SpeedPct;

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
        public float MaxHp = -1f;
        /// <summary>HP at match start (Run mode carries HP between fights). Clamped to MaxHp.</summary>
        public float Hp = -1f;
        public float Radius = -1f;

        public BallLoadout(WeaponRule weapon) => Weapon = weapon;

        public BallLoadout With(params TraitRule[] traits)
        {
            Traits.AddRange(traits);
            return this;
        }
    }
}
