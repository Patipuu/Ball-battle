using System;
using System.IO;
using System.Text;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>
    /// Grid searches over WeaponTuning, each weapon measured against Blade (the reference).
    /// Writes a table per sweep to BB_REPORT (or test output). Restores tuning after each sweep.
    /// Run one: dotnet test --filter "FullyQualifiedName~BalanceSweep.Brawler"
    /// </summary>
    [Explicit, Category("Diagnostic"), NonParallelizable]
    public class BalanceSweep
    {
        const int Seeds = 100;
        readonly StringBuilder log = new StringBuilder();

        void Row(string label, string opponent, string id)
        {
            var s = MatchupReport.Play(id, opponent, Seeds);
            log.AppendLine($"| {label} | {s.WinPctA:0.0} | {s.DrawPct:0.0} | {s.MedianSeconds:0.0} | {s.CapPct:0.0} |");
        }

        void Header(string title)
        {
            log.AppendLine($"\n### {title} (vs blade, {Seeds} seeds/side)\n| params | win % | draw % | median s | cap % |\n|---|---|---|---|---|");
        }

        [TearDown]
        public void Flush()
        {
            TestContext.Out.WriteLine(log.ToString());
            var path = Environment.GetEnvironmentVariable("BB_REPORT");
            if (!string.IsNullOrEmpty(path)) File.AppendAllText(path, log.ToString());
            log.Clear();
        }

        [Test]
        public void Brawler()
        {
            var s0 = WeaponTuning.BrawlerStartDamagePerSpeed;
            var g0 = WeaponTuning.BrawlerDamagePerSpeedPerHit;
            Header("brawler: start dmg/speed, +per hit");
            try
            {
                foreach (var s in new[] { 0.1f, 0.15f, 0.2f, 0.25f, 0.3f })
                foreach (var g in new[] { 0f, 0.01f, 0.02f, 0.04f })
                {
                    WeaponTuning.BrawlerStartDamagePerSpeed = s;
                    WeaponTuning.BrawlerDamagePerSpeedPerHit = g;
                    Row($"s={s} g={g}", "blade", "brawler");
                }
            }
            finally
            {
                WeaponTuning.BrawlerStartDamagePerSpeed = s0;
                WeaponTuning.BrawlerDamagePerSpeedPerHit = g0;
            }
        }

        [Test]
        public void Pike()
        {
            var gr0 = WeaponTuning.PikeGrowthPerHit;
            var sp0 = WeaponTuning.PikeSpin;
            Header("pike: growth per hit, spin");
            try
            {
                foreach (var gr in new[] { 0.5f, 0.75f, 1f, 1.25f })
                foreach (var sp in new[] { 4f, 5f, 6f })
                {
                    WeaponTuning.PikeGrowthPerHit = gr;
                    WeaponTuning.PikeSpin = sp;
                    Row($"grow={gr} spin={sp}", "blade", "pike");
                }
            }
            finally
            {
                WeaponTuning.PikeGrowthPerHit = gr0;
                WeaponTuning.PikeSpin = sp0;
            }
        }

        [Test]
        public void Fang()
        {
            var cd0 = WeaponTuning.FangHitCooldownTicks;
            var kb0 = WeaponTuning.FangKnockbackScale;
            var d0 = WeaponTuning.FangDamagePerHit;
            Header("fang: damage per hit, hit cooldown, knockback scale");
            try
            {
                foreach (var d in new[] { 0.1f, 0.2f, 0.3f, 0.5f })
                foreach (var cd in new[] { 3, 6 })
                foreach (var kb in new[] { 0.3f, 1f })
                {
                    WeaponTuning.FangDamagePerHit = d;
                    WeaponTuning.FangHitCooldownTicks = cd;
                    WeaponTuning.FangKnockbackScale = kb;
                    Row($"d={d} cd={cd} kb={kb}", "blade", "fang");
                }
            }
            finally
            {
                WeaponTuning.FangHitCooldownTicks = cd0;
                WeaponTuning.FangKnockbackScale = kb0;
                WeaponTuning.FangDamagePerHit = d0;
            }
        }

        [Test]
        public void FangLength()
        {
            var l0 = WeaponTuning.FangLength;
            var d0 = WeaponTuning.FangDamagePerHit;
            var cd0 = WeaponTuning.FangHitCooldownTicks;
            var kb0 = WeaponTuning.FangKnockbackScale;
            Header("fang: length, damage per hit (cd 3, kb 0.3)");
            try
            {
                WeaponTuning.FangHitCooldownTicks = 3;
                WeaponTuning.FangKnockbackScale = 0.3f;
                foreach (var l in new[] { 12f, 16f, 20f })
                foreach (var d in new[] { 0.2f, 0.35f, 0.5f })
                {
                    WeaponTuning.FangLength = l;
                    WeaponTuning.FangDamagePerHit = d;
                    Row($"len={l} d={d}", "blade", "fang");
                }
            }
            finally
            {
                WeaponTuning.FangLength = l0;
                WeaponTuning.FangDamagePerHit = d0;
                WeaponTuning.FangHitCooldownTicks = cd0;
                WeaponTuning.FangKnockbackScale = kb0;
            }
        }
    }
}