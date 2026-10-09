using System;
using System.Linq;
using System.Threading.Tasks;
using BallBattle.Sim.Run;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Tries alternative run tunings and prints bot win rates (diagnostic, not a gate).</summary>
    public class RunDifficultySweep
    {
        static void Set(float[] dst, params float[] v) { for (var i = 0; i < v.Length; i++) dst[i] = v[i]; }
        static void Set(int[] dst, params int[] v) { for (var i = 0; i < v.Length; i++) dst[i] = v[i]; }

        static float[] bossHp, enemyHp, level2;
        static int[] bossTraits, minTraits, maxTraits;
        static float winHeal, dmgCard, maxHpCard;

        static void Snapshot()
        {
            bossHp = (float[])RunTuning.BossHp.Clone(); enemyHp = (float[])RunTuning.EnemyHpPct.Clone();
            level2 = (float[])RunTuning.EnemyLevel2Chance.Clone(); bossTraits = (int[])RunTuning.BossTraits.Clone();
            minTraits = (int[])RunTuning.EnemyMinTraits.Clone(); maxTraits = (int[])RunTuning.EnemyMaxTraits.Clone();
            winHeal = RunTuning.WinHealPct; dmgCard = RunTuning.DamageCardPct; maxHpCard = RunTuning.MaxHpCardBonus;
        }

        /// <summary>Back to the shipped values (captured when the sweep starts).</summary>
        static void Baseline()
        {
            Set(RunTuning.BossHp, bossHp); Set(RunTuning.EnemyHpPct, enemyHp); Set(RunTuning.EnemyLevel2Chance, level2);
            Set(RunTuning.BossTraits, bossTraits); Set(RunTuning.EnemyMinTraits, minTraits); Set(RunTuning.EnemyMaxTraits, maxTraits);
            RunTuning.WinHealPct = winHeal; RunTuning.DamageCardPct = dmgCard; RunTuning.MaxHpCardBonus = maxHpCard;
        }

        [Test, Category("Diagnostic"), Explicit("long: dotnet test --filter Name=Sweep")]
        public void Sweep()
        {
            var configs = new (string name, Action apply)[]
            {
                ("A shipped values", () => { }),
                ("B softer enemies (shipped after the first pass = A)", () =>
                {
                    Set(RunTuning.BossHp, 160f, 220f, 300f); Set(RunTuning.BossTraits, 0, 1, 2);
                    Set(RunTuning.EnemyMinTraits, 0, 0, 1); Set(RunTuning.EnemyMaxTraits, 0, 1, 2);
                    Set(RunTuning.EnemyHpPct, 0f, 0f, 0.1f); RunTuning.WinHealPct = 0.3f;
                }),
                ("C B + strong cards", () =>
                {
                    Set(RunTuning.BossHp, 160f, 220f, 300f); Set(RunTuning.BossTraits, 0, 1, 2);
                    Set(RunTuning.EnemyMinTraits, 0, 0, 1); Set(RunTuning.EnemyMaxTraits, 0, 1, 2);
                    Set(RunTuning.EnemyHpPct, 0f, 0f, 0.1f); RunTuning.WinHealPct = 0.3f;
                    RunTuning.DamageCardPct = 0.25f; RunTuning.MaxHpCardBonus = 25f;
                }),
                ("D C + weaker bosses", () =>
                {
                    Set(RunTuning.BossHp, 130f, 180f, 240f); Set(RunTuning.BossTraits, 0, 1, 2);
                    Set(RunTuning.EnemyMinTraits, 0, 0, 1); Set(RunTuning.EnemyMaxTraits, 0, 1, 2);
                    Set(RunTuning.EnemyHpPct, 0f, 0f, 0.1f); RunTuning.WinHealPct = 0.3f;
                    RunTuning.DamageCardPct = 0.25f; RunTuning.MaxHpCardBonus = 25f;
                }),
                ("E D + boss traits 1/1/2", () =>
                {
                    Set(RunTuning.BossHp, 130f, 180f, 240f); Set(RunTuning.BossTraits, 1, 1, 2);
                    Set(RunTuning.EnemyMinTraits, 0, 0, 1); Set(RunTuning.EnemyMaxTraits, 0, 1, 2);
                    Set(RunTuning.EnemyHpPct, 0f, 0f, 0.1f); RunTuning.WinHealPct = 0.3f;
                    RunTuning.DamageCardPct = 0.25f; RunTuning.MaxHpCardBonus = 25f;
                }),
                ("F D + bosses 110/150/200", () =>
                {
                    Set(RunTuning.BossHp, 110f, 150f, 200f); Set(RunTuning.BossTraits, 0, 1, 2);
                    Set(RunTuning.EnemyMinTraits, 0, 0, 1); Set(RunTuning.EnemyMaxTraits, 0, 1, 2);
                    Set(RunTuning.EnemyHpPct, 0f, 0f, 0.1f); RunTuning.WinHealPct = 0.3f;
                    RunTuning.DamageCardPct = 0.25f; RunTuning.MaxHpCardBonus = 25f;
                }),
            };

            const int runs = 600;
            Snapshot();
            try
            {
                foreach (var (name, apply) in configs)
                {
                    Baseline();
                    apply();
                    var line = name;
                    foreach (var policy in new[] { RunBot.Policy.Random, RunBot.Policy.Greedy })
                    {
                        var results = new RunBot.Result[runs];
                        Parallel.For(0, runs, i => results[i] = RunBot.Play((uint)(i + 1), policy));
                        line += $" | {policy} win {results.Count(r => r.Won) * 100.0 / runs:0.0}% fights {results.Average(r => r.FightsWon):0.00} boss1 {results.Count(r => r.BossesDefeated >= 1) * 100.0 / runs:0}%";
                    }
                    TestContext.Out.WriteLine(line);
                }
            }
            finally
            {
                Baseline();
            }
        }
    }
}
