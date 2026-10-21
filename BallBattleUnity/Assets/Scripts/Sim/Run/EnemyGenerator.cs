using System.Collections.Generic;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Traits;
using BallBattle.Sim.Weapons;

namespace BallBattle.Sim.Run
{
    /// <summary>
    /// Opponent of fight n, from the run seed and n only (never from player choices), so the preview is fair
    /// and a replay meets the same enemies. Enemies may use any weapon or trait, locked or not, from the pools
    /// the run snapshotted at start (new content added later never changes a saved run's enemies).
    /// BB-04: a boss fight takes its boss id (RunState.BossFor); the boss decides HP and trait count (BossTuning) and its
    /// mechanic is attached through RunBuild.BossId. MIMIC borrows the player's weapon in RunState.Enemy.
    /// Normal fights draw exactly as before.
    /// </summary>
    public static class EnemyGenerator
    {
        /// <summary>Uses the full registries (for tests and tools). Runs use the pools they snapshotted at start.
        /// A boss slot without <paramref name="bossId"/> gets the slot's first boss (SHELL KING / MIMIC).</summary>
        public static RunBuild Generate(uint runSeed, int fightIndex, string bossId = null)
        {
            var weapons = new List<string>();
            foreach (var w in WeaponRegistry.All) weapons.Add(w.Id);
            var traits = new List<string>();
            foreach (var t in TraitRegistry.All) if (!t.PlayerOnly) traits.Add(t.Id);
            if (bossId == null) bossId = DefaultBoss(fightIndex);
            return Generate(runSeed, fightIndex, weapons, traits, bossId);
        }

        /// <summary>First boss of the slot at <paramref name="fightIndex"/>, or null for a normal fight.</summary>
        public static string DefaultBoss(int fightIndex)
        {
            for (var s = 0; s < RunTuning.BossFights.Length && s < BossIds.Slots.Length; s++)
                if (RunTuning.BossFights[s] == fightIndex) return BossIds.Slots[s][0];
            return null;
        }

        /// <summary><paramref name="bossId"/> null = a normal enemy for that stage; otherwise that boss (any fight index: -bbForceBoss).</summary>
        public static RunBuild Generate(uint runSeed, int fightIndex, IReadOnlyList<string> weaponPool, IReadOnlyList<string> traitPool, string bossId = null,
            ChapterDef chapter = null, int heat = 0)
        {
            var rng = new SimRandom(RunTuning.Derive(runSeed, 2000 + fightIndex, 0));
            var stage = RunTuning.StageOf(fightIndex);
            var boss = !string.IsNullOrEmpty(bossId);

            var e = new RunBuild { WeaponId = boss && bossId != BossIds.Mimic
                ? BossCoreRule.WeaponId : weaponPool[(int)(rng.NextUInt() % (uint)weaponPool.Count)] };

            int traitCount;
            if (boss)
            {
                traitCount = BossTraitCount(bossId);
                e.MaxHp = BossHp(bossId) * (Fix.One + (chapter?.HpPct ?? Fix.Zero) + HeatRules.HpPct(heat) + HeatRules.BossHpPct(heat));
                e.DamagePct = (chapter?.DamagePct ?? Fix.Zero) + HeatRules.BossDamagePct(heat);
                e.Radius = RunTuning.BossRadius;
                e.BossId = bossId;
                // Boss traits roll like the slot's fight (Warden = fight 8 odds), whatever fight a forced boss lands in.
                stage = RunTuning.StageOf(RunTuning.BossFights[BossIds.SlotOf(bossId)]);
            }
            else
            {
                var min = RunTuning.EnemyMinTraits[stage];
                var max = RunTuning.EnemyMaxTraits[stage];
                traitCount = min + (int)(rng.NextUInt() % (uint)(max - min + 1)) + HeatRules.ExtraTraits(heat);
                e.MaxHp = BallPower.BaseHp(e.WeaponId) * (Fix.One + RunTuning.EnemyHpPct[stage] + (chapter?.HpPct ?? Fix.Zero) + HeatRules.HpPct(heat));
                e.DamagePct = RunTuning.EnemyDamagePct[stage] + (chapter?.DamagePct ?? Fix.Zero);
            }
            e.Hp = e.MaxHp;

            // Only traits that work on the rolled weapon (no Parry Master on a bladeless body, no banned combos).
            var eligible = new List<string>();
            if (traitCount > 0)
                foreach (var id in traitPool)
                    if (TraitRegistry.IsEligible(id, e.WeaponId)) eligible.Add(id);
            if (traitCount > eligible.Count) traitCount = eligible.Count;
            var used = new bool[eligible.Count];
            for (var t = 0; t < traitCount; t++)
            {
                var i = (int)(rng.NextUInt() % (uint)eligible.Count);
                while (used[i]) i = (i + 1) % eligible.Count;
                used[i] = true;
                var level = rng.NextFix01() < RunTuning.EnemyLevel2Chance[stage] ? 2 : 1;
                e.Traits.Add(new TraitSlot(eligible[i], level));
            }
            return e;
        }

        public static Fix BossHp(string bossId)
        {
            switch (bossId)
            {
                case BossIds.ShellKing: return BossTuning.ShellKingHp;
                case BossIds.Magnet: return BossTuning.MagnetHp;
                case BossIds.Mimic: return BossTuning.MimicHp;
                case BossIds.Warden: return BossTuning.WardenHp;
                case BossIds.Hunter: return BossTuning.HunterHp;
                case BossIds.Bell: return BossTuning.BellHp;
                case BossIds.Ember: return BossTuning.EmberHp;
                case BossIds.Prism: return BossTuning.PrismHp;
                default: throw new System.ArgumentException($"Unknown boss '{bossId}'", nameof(bossId));
            }
        }

        /// <summary>Random traits a boss rolls (MIMIC: 0 here, it copies the player's in RunState.Enemy).</summary>
        public static int BossTraitCount(string bossId)
        {
            switch (bossId)
            {
                default: return 0;
            }
        }
    }
}
