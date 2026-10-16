using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    /// <summary>Run-only measurements. Event damage is counted once; no diagnostic changes any match state.</summary>
    [Explicit("BB-6f 1000-run qualification and WARDEN source diagnostics"), Category("Slow"), NonParallelizable]
    public class Bb6fDiagnostics
    {
        sealed class Fight
        {
            public string Arena;
            public bool Won, Cap;
            public int Side, Ceiling, PinStreak, MaxPinStreak, Rebounds, LastSurface, LastSurfaceActive = -100;
            public int LastActive = -1;
            public long SpikesRaw, ObstaclesRaw, CombatRaw, OtherWallsRaw;
            public int MinCorridorRaw = int.MaxValue;
        }

        [Test]
        public void MeasureRunTargetsAndWardenDiagnostics()
        {
            var output = Environment.GetEnvironmentVariable("BB6F_OUT");
            if (string.IsNullOrEmpty(output)) throw new ArgumentException("Set BB6F_OUT to an existing output directory");
            var boss = BossSweep.Measure(1000, 100);
            var text = BossSweep.Markdown(boss);
            File.WriteAllText(Path.Combine(output, "boss.md"), text);
            TestContext.Progress.WriteLine(text);

            var fights = new List<Fight>[1000];
            Parallel.For(0, fights.Length, i =>
            {
                var list = fights[i] = new List<Fight>();
                var current = new Fight();
                RunBot.Play((uint)(i + 1), RunBot.Policy.Greedy, (run, m) =>
                {
                    if (run.CurrentBossId != BossIds.Warden) return;
                    current.Arena = run.ArenaId;
                    current.Won = m.WinnerIndex == 0;
                    current.Cap = m.EndReason == MatchEndReason.TimeCap;
                    list.Add(current);
                    current = new Fight();
                }, (run, m) =>
                {
                    if (run.CurrentBossId != BossIds.Warden) return;
                    foreach (var e in m.Events)
                    {
                        var damage = e.Value.Raw;
                        if (damage <= 0) continue;
                        if (e.Type == SimEventType.WallBounce && e.A == 0)
                        {
                            if ((e.Variant & 128) != 0)
                            {
                                current.SpikesRaw += damage;
                                if ((e.Variant & 3) != 0) current.Side++; else current.Ceiling++;
                            }
                            else current.OtherWallsRaw += damage;
                        }
                        else if (e.Type == SimEventType.ObstacleHit && e.A == 0) current.ObstaclesRaw += damage;
                        else if ((e.Type == SimEventType.Hit || e.Type == SimEventType.ProjectileHit || e.Type == SimEventType.Damage) && e.B == 0)
                            current.CombatRaw += damage;
                        else if (e.Type == SimEventType.StatusTick && e.A == 0) current.CombatRaw += damage;
                    }
                    if (run.ArenaId != ArenaRegistry.Pillar || current.LastActive == m.ActiveTick) return;
                    current.LastActive = m.ActiveTick;
                    var ball = m.Balls[0];
                    var obstacle = m.Layout.Get(0);
                    var sideDistance = FixMath.Min(ball.Pos.X - m.Arena.Left, m.Arena.Right - ball.Pos.X);
                    var pillarDistance = (ball.Pos - obstacle.A).Length - obstacle.Radius;
                    var corridor = FixMath.Min(obstacle.A.X - m.Arena.Left, m.Arena.Right - obstacle.A.X) - obstacle.Radius - ball.Radius * 2;
                    if (corridor.Raw < current.MinCorridorRaw) current.MinCorridorRaw = corridor.Raw;
                    var confined = m.ObstacleActive(0) && sideDistance <= ball.Radius + 2 && pillarDistance <= ball.Radius + 2;
                    current.PinStreak = confined ? current.PinStreak + 1 : 0;
                    current.MaxPinStreak = Math.Max(current.MaxPinStreak, current.PinStreak);
                    foreach (var e in m.Events)
                    {
                        if (e.A != 0) continue;
                        var surface = e.Type == SimEventType.ObstacleHit ? 1 : e.Type == SimEventType.WallBounce ? 2 : 0;
                        if (surface == 0) continue;
                        if (current.LastSurface != 0 && surface != current.LastSurface && m.ActiveTick - current.LastSurfaceActive <= 10)
                            current.Rebounds++;
                        current.LastSurface = surface;
                        current.LastSurfaceActive = m.ActiveTick;
                    }
                });
            });
            var all = fights.SelectMany(f => f).ToList();
            var csv = new StringBuilder("arena,fights,wins,winPct,sideHitsPerFight,ceilingHitsPerFight,timeCapPct\n");
            foreach (var arena in ArenaRegistry.All)
            {
                var rows = all.Where(f => f.Arena == arena.Id).ToList();
                var n = rows.Count;
                csv.AppendLine(FormattableString.Invariant($"{arena.Id},{n},{rows.Count(f => f.Won)},{Pct(rows.Count(f => f.Won), n):F3},{Average(rows.Sum(f => f.Side), n):F3},{Average(rows.Sum(f => f.Ceiling), n):F3},{Pct(rows.Count(f => f.Cap), n):F3}"));
            }
            File.WriteAllText(Path.Combine(output, "warden-arenas.csv"), csv.ToString());
            TestContext.Progress.WriteLine(csv.ToString());
            var pillar = all.Where(f => f.Arena == ArenaRegistry.Pillar).ToList();
            var count = pillar.Count;
            var pillarCsv = "fights,wardenSpikeDamagePerFight,obstacleDamagePerFight,bossCombatDamagePerFight,normalWallDamagePerFight,pinnedFights,maxConsecutiveConfinedActiveTicks,rapidAlternatingRebounds,minCorridorClearancePx\n" +
                FormattableString.Invariant($"{count},{Average(pillar.Sum(f => f.SpikesRaw), count) / 65536:F6},{Average(pillar.Sum(f => f.ObstaclesRaw), count) / 65536:F6},{Average(pillar.Sum(f => f.CombatRaw), count) / 65536:F6},{Average(pillar.Sum(f => f.OtherWallsRaw), count) / 65536:F6},{pillar.Count(f => f.MaxPinStreak >= 30)},{(count == 0 ? 0 : pillar.Max(f => f.MaxPinStreak))},{pillar.Sum(f => f.Rebounds)},{(count == 0 ? double.NaN : pillar.Min(f => f.MinCorridorRaw) / 65536.0):F6}\n");
            File.WriteAllText(Path.Combine(output, "pillar-damage.csv"), pillarCsv);
            TestContext.Progress.WriteLine(pillarCsv);
        }

        static double Pct(long value, int n) => n == 0 ? double.NaN : 100.0 * value / n;
        static double Average(long value, int n) => n == 0 ? double.NaN : (double)value / n;
    }
}
