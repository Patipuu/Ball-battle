using System.Collections.Generic;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Opponent of fight n, from the run seed and n only (never from player choices), so the preview is fair
    /// and a replay meets the same enemies. Enemies may use any weapon or trait, locked or not, from the pools
    /// the run snapshotted at start (new content added later never changes a saved run's enemies).
    /// </summary>
    public static class EnemyGenerator
    {
        /// <summary>Uses the full registries (for tests and tools). Runs use the pools they snapshotted at start.</summary>
        public static RunBuild Generate(uint runSeed, int fightIndex)
        {
            var weapons = new List<string>();
            foreach (var w in WeaponRegistry.All) weapons.Add(w.Id);
            var traits = new List<string>();
            foreach (var t in TraitRegistry.All) traits.Add(t.Id);
            return Generate(runSeed, fightIndex, weapons, traits);
        }

        public static RunBuild Generate(uint runSeed, int fightIndex, IReadOnlyList<string> weaponPool, IReadOnlyList<string> traitPool)
        {
            var rng = new SimRandom(RunTuning.Derive(runSeed, 2000 + fightIndex, 0));
            var stage = RunTuning.StageOf(fightIndex);
            var boss = RunTuning.IsBoss(fightIndex);

            var e = new RunBuild { WeaponId = weaponPool[(int)(rng.NextUInt() % (uint)weaponPool.Count)] };

            int traitCount;
            if (boss)
            {
                traitCount = RunTuning.BossTraits[stage];
                e.MaxHp = RunTuning.BossHp[stage];
                e.Radius = RunTuning.BossRadius;
            }
            else
            {
                var min = RunTuning.EnemyMinTraits[stage];
                var max = RunTuning.EnemyMaxTraits[stage];
                traitCount = min + (int)(rng.NextUInt() % (uint)(max - min + 1));
                e.MaxHp = RunTuning.PlayerStartHp * (1f + RunTuning.EnemyHpPct[stage]);
            }
            e.Hp = e.MaxHp;

            // Only traits that work on the rolled weapon (no Parry Master on a bladeless body, no banned combos).
            var eligible = new List<string>();
            foreach (var id in traitPool)
                if (TraitRegistry.IsEligible(id, e.WeaponId)) eligible.Add(id);
            if (traitCount > eligible.Count) traitCount = eligible.Count;
            var used = new bool[eligible.Count];
            for (var t = 0; t < traitCount; t++)
            {
                var i = (int)(rng.NextUInt() % (uint)eligible.Count);
                while (used[i]) i = (i + 1) % eligible.Count;
                used[i] = true;
                var level = rng.NextFloat01() < RunTuning.EnemyLevel2Chance[stage] ? 2 : 1;
                e.Traits.Add(new TraitSlot(eligible[i], level));
            }
            return e;
        }
    }
}
