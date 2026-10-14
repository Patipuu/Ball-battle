using System;
using System.Collections.Generic;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    [Serializable]
    public sealed class TraitSlot
    {
        public string Id;
        public int Level = 1;

        public TraitSlot() { }
        public TraitSlot(string id, int level) { Id = id; Level = level; }
    }

    /// <summary>
    /// A ball described by ids and numbers (serializable, reusable). ToLoadout() makes fresh weapon/trait
    /// instances for one match. Used for the player (carried between fights) and for generated enemies.
    /// </summary>
    [Serializable]
    public sealed class RunBuild
    {
        public string WeaponId;
        public List<TraitSlot> Traits = new List<TraitSlot>();
        public Fix DamagePct;
        public Fix SpeedPct;
        public Fix MaxHp = RunTuning.PlayerStartHp;
        public Fix Hp = RunTuning.PlayerStartHp;
        /// <summary>&lt;= 0 = default ball size.</summary>
        public Fix Radius;
        /// <summary>BB-04: boss mechanic of a generated boss (Boss.BossIds); null for the player and normal enemies.</summary>
        public string BossId;

        /// <summary>HP is kept in whole points (what the screen shows), never below 1 while alive.</summary>
        public void SetHp(Fix hp) => Hp = FixMath.Max(Fix.One, FixMath.Min(MaxHp, FixMath.RoundToEven(hp)));

        /// <summary>Missing at least one whole HP point (so a heal card is worth offering).</summary>
        public bool Hurt => MaxHp - Hp >= Fix.One;

        public int TraitLevel(string id)
        {
            foreach (var t in Traits)
                if (t.Id == id) return t.Level;
            return 0;
        }

        public BallLoadout ToLoadout()
        {
            var l = new BallLoadout(WeaponRegistry.Create(WeaponId))
            {
                Bonus = new StatBonus { DamagePct = DamagePct, SpeedPct = SpeedPct },
                MaxHp = MaxHp,
                Hp = Hp,
                Radius = Radius
            };
            foreach (var t in Traits) l.Traits.Add(TraitRegistry.Create(t.Id, t.Level));
            if (!string.IsNullOrEmpty(BossId)) l.Boss = Boss.BossRegistry.Create(BossId);
            return l;
        }

        public RunBuild Clone()
        {
            var c = (RunBuild)MemberwiseClone();
            c.Traits = new List<TraitSlot>();
            foreach (var t in Traits) c.Traits.Add(new TraitSlot(t.Id, t.Level));
            return c;
        }
    }
}
