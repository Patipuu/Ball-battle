using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Tools;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    [Explicit("BB-10a authored skill qualification (no tuning sweep)"), Category("Slow"), NonParallelizable]
    public class Bb10aDiagnostics
    {
        sealed class BotRow
        {
            public bool Won;
            public int Picks, SkillPicks;
            public readonly Dictionary<string,int> Branches = new Dictionary<string,int>();
            public readonly List<(string boss,bool retry,bool won)> Fights = new List<(string,bool,bool)>();
        }
        [Test]
        public void SkillCheckRun()
        {
            var folder=Environment.GetEnvironmentVariable("BB10A_OUT");
            if(string.IsNullOrEmpty(folder)) throw new ArgumentException("Set BB10A_OUT");
            var a=RoundRobin.Run(new RoundRobin.Options{Seeds=50,Arena="all"});
            File.WriteAllText(Path.Combine(folder,"rr-a.csv"),a.MainCsv);
            File.WriteAllText(Path.Combine(folder,"rr-a-weapons.csv"),a.WeaponCsv);
            File.WriteAllText(Path.Combine(folder,"rr-a-arenas.csv"),a.ArenaCsv);
            var skill=new StringBuilder("weapon,appearances,skillFires,averageFiresPerFight\n");
            foreach(var data in SkillDefinition.All)
            {
                long n=0,fires=0;
                foreach(var pair in a.Pairs)
                {
                    if(pair.WeaponA==data.WeaponId){n+=pair.Matches;fires+=pair.SkillFiresA;}
                    if(pair.WeaponB==data.WeaponId){n+=pair.Matches;fires+=pair.SkillFiresB;}
                }
                skill.AppendLine(FormattableString.Invariant($"{data.WeaponId},{n},{fires},{fires/(double)n:F6}"));
            }
            File.WriteAllText(Path.Combine(folder,"skill-fires.csv"),skill.ToString());
            var timeouts=new StringBuilder("arena,matches,timeCaps,timeoutPct\n");
            var worst=new StringBuilder("arena,weaponA,weaponB,winRateA\n");
            foreach(var arena in ArenaRegistry.All)
            {
                var pairs=a.Pairs.Where(p=>p.Arena==arena.Id).ToList(); var n=pairs.Sum(p=>p.Matches);var caps=pairs.Sum(p=>p.TimeCaps);
                timeouts.AppendLine(FormattableString.Invariant($"{arena.Id},{n},{caps},{100.0*caps/n:F6}"));
                var pair=pairs.Where(p=>p.WeaponA!=p.WeaponB).OrderByDescending(p=>Math.Abs((double)p.WinsA/p.Matches-0.5)).First();
                worst.AppendLine(FormattableString.Invariant($"{arena.Id},{pair.WeaponA},{pair.WeaponB},{100.0*pair.WinsA/pair.Matches:F3}"));
            }
            File.WriteAllText(Path.Combine(folder,"timeouts.csv"),timeouts.ToString());
            File.WriteAllText(Path.Combine(folder,"worst-pairs.csv"),worst.ToString());
            foreach(var policy in new[]{RunBot.Policy.Greedy,RunBot.Policy.Random})
            {
                var rows=new BotRow[500];
                Parallel.For(0,500,i=>
                {
                    var row=rows[i]=new BotRow();
                    RunBot.PickObserved=(run,card,branch)=>
                    {
                        row.Picks++;
                        if(card.Kind!=CardKind.SkillUp)return;
                        row.SkillPicks++;
                        if(run.Build.SkillLevel!=1)return;
                        var key=run.Build.WeaponId+","+branch;
                        row.Branches[key]=row.Branches.TryGetValue(key,out var count)?count+1:1;
                    };
                    try { row.Won=RunBot.Play((uint)(i+1),policy,(run,m)=>
                        { if(run.CurrentBossId!=null)row.Fights.Add((run.CurrentBossId,run.IsRetry,m.WinnerIndex==0)); }).Won; }
                    finally { RunBot.PickObserved=null; }
                });
                var sb=new StringBuilder("bot,runs,wins,winPct,totalPicks,skillUpPicks,skillUpPickPct\n");
                var picks=rows.Sum(r=>r.Picks);var skillPicks=rows.Sum(r=>r.SkillPicks);var won=rows.Count(r=>r.Won);
                sb.AppendLine(FormattableString.Invariant($"{policy},500,{won},{won/5.0:F3},{picks},{skillPicks},{100.0*skillPicks/picks:F3}"));
                File.WriteAllText(Path.Combine(folder,policy+"-run.csv"),sb.ToString());
                var fights=rows.SelectMany(r=>r.Fights).ToList();
                var bosses=new StringBuilder("boss,met,firstWins,beatenWithRetry,firstWinPct,retryWinPct\n");
                foreach(var id in BossIds.All)
                {
                    var f=fights.Where(x=>x.boss==id).ToList();var met=f.Count(x=>!x.retry);var first=f.Count(x=>!x.retry&&x.won);var beaten=f.Count(x=>x.won);
                    bosses.AppendLine(FormattableString.Invariant($"{id},{met},{first},{beaten},{(met==0?double.NaN:100.0*first/met):F3},{(met==0?double.NaN:100.0*beaten/met):F3}"));
                }
                File.WriteAllText(Path.Combine(folder,policy+"-bosses.csv"),bosses.ToString());
                var branches=new StringBuilder("weapon,A,B\n");
                foreach(var data in SkillDefinition.All)
                {
                    int Count(string branch)=>rows.Sum(r=>r.Branches.TryGetValue(data.WeaponId+","+branch,out var v)?v:0);
                    branches.AppendLine($"{data.WeaponId},{Count("A")},{Count("B")}");
                }
                File.WriteAllText(Path.Combine(folder,policy+"-branches.csv"),branches.ToString());
                TestContext.Progress.WriteLine(sb.ToString());
            }
            var b=RoundRobin.Run(new RoundRobin.Options{Seeds=50,Arena="all"});
            File.WriteAllText(Path.Combine(folder,"rr-b.csv"),b.MainCsv);
            File.WriteAllText(Path.Combine(folder,"rr-b-weapons.csv"),b.WeaponCsv);
            File.WriteAllText(Path.Combine(folder,"rr-b-arenas.csv"),b.ArenaCsv);
            Assert.That(b.MainCsv,Is.EqualTo(a.MainCsv)); Assert.That(b.WeaponCsv,Is.EqualTo(a.WeaponCsv)); Assert.That(b.ArenaCsv,Is.EqualTo(a.ArenaCsv));
        }
    }
}
