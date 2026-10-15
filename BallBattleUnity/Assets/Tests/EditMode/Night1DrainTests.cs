using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    public partial class Night1ViewTests
    {
        sealed class DrainRow
        {
            public string Context, Opponent, Arena;
            public uint InputSeed, MatchSeed;
            public int Level, Ticks, ActiveTicks, ThreeStackWallTicks, ThreeStackActiveTicks, EligibleTicks, MissedEligibleTicks, PureFires, ViewFires, SkillLines, Played, Missing;
            public string Csv => string.Join(",", Context, Opponent, InputSeed, MatchSeed, Arena, Level, Ticks, ActiveTicks,
                ThreeStackWallTicks, ThreeStackActiveTicks, EligibleTicks, MissedEligibleTicks, PureFires, ViewFires, SkillLines, Played, Missing);
        }
        static SkillSpec DrainSpec(int level) => new SkillSpec(level, level == 1 ? SkillBranch.None : SkillBranch.B);
        static MatchSim VersusDrain(string opponent, uint seed, int level) => new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), seed, new[] {
            new BallLoadout(WeaponRegistry.Create("venom")) {SkillLevel=level, SkillBranch=DrainSpec(level).Branch},
            new BallLoadout(WeaponRegistry.Create(opponent)) {SkillLevel=level, SkillBranch=DrainSpec(level).Branch}
        });
        static RunState DrainRun(string opponent, uint seed)
        {
            var progress = new ProgressData {RunsPlayed=1};
            progress.Achievements.AddRange(new[]{UnlockRules.Boss1,UnlockRules.Boss2,UnlockRules.Boss3,UnlockRules.Win1,UnlockRules.Win3});
            var run = RunState.Start(seed, RunState.StartChoices(seed,UnlockRules.Weapons(progress))[0],progress);
            run.Pick(0); run.Build.WeaponId="venom";
            run.EnemyWeapons=new List<string>{opponent};
            return run;
        }
        static void MeasurePure(MatchSim match, DrainRow row)
        {
            while(match.Outcome==MatchOutcome.Ongoing)
            {
                var stacks=match.Balls[1].Status.PoisonStackCount>=3;
                if(stacks) row.ThreeStackWallTicks++;
                var active=match.HitstopRemaining==0;
                if(stacks && active) row.ThreeStackActiveTicks++;
                var eligible=stacks && active && match.ActiveTick+1>=match.Balls[0].Skill.ReadyAt;
                if(eligible) row.EligibleTicks++;
                match.Step();
                var fired=match.Events.Any(e=>e.Type==SimEventType.SkillFired && e.A==0);
                if(fired) row.PureFires++;
                if(eligible && !fired) row.MissedEligibleTicks++;
            }
            row.Ticks=match.Tick; row.ActiveTicks=match.ActiveTick;
        }
        static void SaveDrainSummary(IEnumerable<DrainRow> rows, string path)
        {
            var csv=new StringBuilder("context,opponent,matches,threeStackMatches,threeStackActiveTicks,totalActiveTicks,pureFires,viewFires,skillLines,played,missing,zeroFireMatches,eligibleMisses\n");
            foreach(var group in rows.GroupBy(r=>(r.Context,r.Opponent)))
                csv.AppendLine(string.Join(",",group.Key.Context,group.Key.Opponent,group.Count(),group.Count(r=>r.ThreeStackActiveTicks>0),
                    group.Sum(r=>r.ThreeStackActiveTicks),group.Sum(r=>r.ActiveTicks),group.Sum(r=>r.PureFires),group.Sum(r=>r.ViewFires),
                    group.Sum(r=>r.SkillLines),group.Sum(r=>r.Played),group.Sum(r=>r.Missing),group.Count(r=>r.PureFires==0),group.Sum(r=>r.MissedEligibleTicks)));
            File.WriteAllText(path,csv.ToString());
        }
        [Test]
        public void DiagnoseTwoHundredSporeFightsThroughPureSimAndBuildEquivalentView()
        {
            if(!Phase("C")) Assert.Ignore("Phase C diagnostic only");
            var rows=new List<DrainRow>(); var lines=new List<string>();
            AudioLog.ForceOn=true;
            var previousStackTrace=Application.GetStackTraceLogType(LogType.Log);
            Application.SetStackTraceLogType(LogType.Log,StackTraceLogType.None);
            try
            {
                foreach(var context in new[]{"Versus","Run"})
                for(var i=0;i<100;i++)
                {
                    var opponent=SkillDefinition.All[i%8].WeaponId;
                    var level=(i/8)%2==0 ? 1 : 3;
                    var seed=(uint)((context=="Versus" ? 220001 : 330001)+i);
                    var run=context=="Run" ? DrainRun(opponent,seed) : null;
                    var pure=run==null ? VersusDrain(opponent,seed,level) : run.CreateMatch(forceSkill:DrainSpec(level));
                    var row=new DrainRow{Context=context,Opponent=opponent,InputSeed=seed,MatchSeed=pure.Seed,Arena=run?.ArenaId??ArenaRegistry.Classic,Level=level};
                    MeasurePure(pure,row);
                    var fightRoot=new GameObject("DrainFight"); fightRoot.transform.SetParent(root.transform);
                    try
                    {
                        var arena=fightRoot.AddComponent<ArenaView>(); arena.Art=art; arena.AutoRestart=false;
                        var audio=AudioDirector.Create(fightRoot.transform,library,new MemorySaveStore());
                        arena.SimEventRaised+=e=>
                        {
                            audio.OnSimEvent(e,arena.Sim);
                            if(e.Type!=SimEventType.SkillFired || e.A!=0) return;
                            row.ViewFires++; var line=audio.LogLines.Last();
                            Assert.That(line,Does.StartWith("[Skill] "));
                            row.SkillLines++; if(line.EndsWith("result=played")) row.Played++; else row.Missing++;
                            lines.Add("context="+context+" seed="+seed+" "+line);
                        };
                        RunFlow flow=null;
                        if(run==null)
                        {
                            arena.ForceSkill=DrainSpec(level); arena.StartMatch("venom",opponent,seed);
                        }
                        else
                        {
                            var store=new MemorySaveStore(); store.Save(new SaveData{HasRun=true,Run=RunSave.From(run)});
                            flow=new RunFlow(store,0,0){ForceSkill=DrainSpec(level)};
                            flow.MatchPrepared+=m=>arena.StartMatch(m,"YOU","ENEMY");
                            flow.Open(); flow.Continue(); flow.Fight(); flow.Tick(.01f);
                            Assert.That(flow.Current,Is.EqualTo(RunFlow.State.Playing));
                        }
                        while(arena.Sim.Outcome==MatchOutcome.Ongoing) arena.Advance(1f/60f);
                        if(flow!=null){flow.OnMatchEnded(arena.Sim); Assert.That(flow.Current,Is.EqualTo(RunFlow.State.FightOver));}
                        Assert.That(arena.Sim.ComputeHash(),Is.EqualTo(pure.ComputeHash()),context+" seed="+seed+" final state parity");
                    }
                    finally {Object.DestroyImmediate(fightRoot);}
                    rows.Add(row);
                    // Keep evidence current if an unattended process is interrupted.
                    File.WriteAllLines(Path.Combine(Out,"phaseC-drain-matches.csv"),new[]{"context,opponent,inputSeed,matchSeed,arena,level,ticks,activeTicks,threeStackWallTicks,threeStackActiveTicks,eligibleTicks,eligibleMisses,pureFires,viewFires,skillLines,played,missing"}.Concat(rows.Select(r=>r.Csv)));
                    File.WriteAllLines(Path.Combine(Out,"phaseC-drain-skill-lines.txt"),lines);
                    if((i+1)%25==0) Debug.Log("[DrainDiagnosis] "+context+" matches="+(i+1));
                }
                SaveDrainSummary(rows,Path.Combine(Out,"phaseC-drain-summary.csv"));
                var distribution=new StringBuilder("context,level,matches,zeroFireMatches,threeStackMatches,totalThreeStackActiveTicks,totalActiveTicks,fires,averageFires\n");
                foreach(var group in rows.GroupBy(r=>(r.Context,r.Level)))
                    distribution.AppendLine(FormattableString.Invariant($"{group.Key.Context},{group.Key.Level},{group.Count()},{group.Count(r=>r.PureFires==0)},{group.Count(r=>r.ThreeStackActiveTicks>0)},{group.Sum(r=>r.ThreeStackActiveTicks)},{group.Sum(r=>r.ActiveTicks)},{group.Sum(r=>r.PureFires)},{group.Average(r=>r.PureFires):F6}"));
                File.WriteAllText(Path.Combine(Out,"phaseC-drain-distribution.csv"),distribution.ToString());
                Assert.That(rows.Count,Is.EqualTo(200));
                Assert.That(rows.Sum(r=>r.MissedEligibleTicks),Is.Zero,"condition + ready must fire on that active tick");
                Assert.That(rows.All(r=>r.PureFires==r.ViewFires && r.ViewFires==r.SkillLines),Is.True,"Sim/View/log fire parity");
            }
            finally {Application.SetStackTraceLogType(LogType.Log,previousStackTrace);}
        }
    }
}
