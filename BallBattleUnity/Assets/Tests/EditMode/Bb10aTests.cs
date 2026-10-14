using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    public class Bb10aTests
    {
        GameObject root; ArtLibrary art;
        [SetUp] public void SetUp() { root=new GameObject("Bb10a"); art=AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath); }
        [TearDown] public void TearDown() => Object.DestroyImmediate(root);
        [TestCase("1",1,SkillBranch.None)] [TestCase("2a",2,SkillBranch.A)] [TestCase("2b",2,SkillBranch.B)]
        [TestCase("3a",3,SkillBranch.A)] [TestCase("3b",3,SkillBranch.B)]
        public void ForceSkillParsing(string value,int level,SkillBranch branch)
        {
            var spec=DebugOptions.ParseForceSkill(new[]{"-bbForceSkill",value}).Value;
            Assert.That((spec.Level,spec.Branch),Is.EqualTo((level,branch)));
            Assert.That(DebugOptions.ParseForceSkill(new string[0]),Is.Null);
        }
        [Test]
        public void UnknownForceSkillWarnsOnceAndIgnores()
        {
            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning,"[BallBattle] -bbForceSkill: expected 1, 2a, 2b, 3a or 3b; ignored");
            Assert.That(DebugOptions.ParseForceSkill(new[]{"-bbForceSkill","4c","-bbForceSkill","bad"}),Is.Null);
        }
        [Test]
        public void ForceSkillRunIsPlayerOnlyAndNeverSerialized()
        {
            var store=new MemorySaveStore(); var flow=new RunFlow(store){ForceSkill=new SkillSpec(3,SkillBranch.B)};
            flow.Open(); flow.NewRun(21); flow.ChooseWeapon(flow.StartChoices[0]);
            Assert.That(flow.Run.OfferCards.All(c=>c.Kind!=CardKind.SkillUp),Is.True);
            flow.PickStart(0); flow.Fight(); var m=flow.CurrentMatch;
            Assert.That((m.Balls[0].Skill.Level,m.Balls[0].Skill.Branch),Is.EqualTo((3,SkillBranch.B)));
            Assert.That((m.Balls[1].Skill.Level,m.Balls[1].Skill.Branch),Is.EqualTo((1,SkillBranch.None)));
            var saved=store.Load().Run.ToRun();
            Assert.That((saved.Build.SkillLevel,saved.Build.SkillBranch),Is.EqualTo((1,SkillBranch.None)));
            Assert.That(saved.SkillOverride,Is.Null); StringAssert.DoesNotContain("SkillOverride",store.Json);
            Assert.That(saved.History,Is.Empty);
        }
        [Test]
        public void ForceSkillVersusAffectsBothBalls()
        {
            var arena=root.AddComponent<ArenaView>(); arena.Art=art; arena.AutoRestart=false; arena.ForceSkill=new SkillSpec(2,SkillBranch.A);
            arena.StartMatch("blade","rig",3);
            foreach(var ball in arena.Sim.Balls) Assert.That((ball.Skill.Level,ball.Skill.Branch),Is.EqualTo((2,SkillBranch.A)));
        }
        [Test]
        public void ResumedForceSkillOffersUseTransientLevelAndSaveNormalLevel()
        {
            var p=new ProgressData(); var r=RunState.Start(21,RunState.StartChoices(21,UnlockRules.Weapons(p))[0],p);
            r.OfferCards.Clear(); r.OfferCards.Add(OfferSlot.From(new Card(CardKind.SkillUp,r.Build.WeaponId,2)));
            r.SkillOverride=new SkillSpec(2,SkillBranch.B);
            Assert.That(r.OfferCard(0).Level,Is.EqualTo(3)); Assert.That(r.CanPick(0),Is.True);
            var saved=RunSave.From(r).ToRun(); Assert.That(saved.OfferCard(0).Level,Is.EqualTo(2));
            Assert.That(saved.SkillOverride,Is.Null);
            r.SkillOverride=new SkillSpec(3,SkillBranch.B); Assert.That(r.OfferCard(0).Kind,Is.Not.EqualTo(CardKind.SkillUp));
        }
        [Test]
        public void V3SaveKeepsHistoryAndMigratesOnlyPendingSwap()
        {
            var p=new ProgressData(); var run=RunState.Start(21,RunState.StartChoices(21,UnlockRules.Weapons(p))[0],p);
            run.OfferCards.Clear(); run.OfferCards.Add(OfferSlot.From(new Card(CardKind.SwapWeapon,"pike")));
            run.OfferCards.Add(OfferSlot.From(new Card(CardKind.SwapWeapon,"volley"))); run.PickedMask=1;
            run.History.Add(new FightRecord{FightIndex=0,Won=true,EnemyWeapon="fang",HpAfter=80});
            var data=new SaveData{Version=3,HasRun=true,Run=RunSave.From(run)};
            SaveMigration.Upgrade(data);
            Assert.That((data.Version,data.HasRun),Is.EqualTo((4,true)));
            Assert.That(data.Run.History.Count,Is.EqualTo(1)); Assert.That(data.Run.History[0].EnemyWeapon,Is.EqualTo("fang"));
            Assert.That(data.Run.OfferCards[0].Kind,Is.EqualTo(CardKind.SwapWeapon));
            Assert.That(data.Run.OfferCards[1].Kind,Is.EqualTo(CardKind.SkillUp));
            Assert.That(data.Run.ToRun().Build.SkillLevel,Is.EqualTo(1));
        }
        [Test]
        public void SkillSaveRoundTripKeepsBranchAndLevel()
        {
            var p=new ProgressData(); var r=RunState.Start(21,RunState.StartChoices(21,UnlockRules.Weapons(p))[0],p);
            r.Build.SkillLevel=3; r.Build.SkillBranch=SkillBranch.B;
            var store=new MemorySaveStore(); store.Save(new SaveData{HasRun=true,Run=RunSave.From(r)});
            var back=store.Load().Run.ToRun(); Assert.That((back.Build.SkillLevel,back.Build.SkillBranch),Is.EqualTo((3,SkillBranch.B)));
        }
        [Test]
        public void SkillUpBranchButtonsReachLevel2ThenKeepBranchAtLevel3()
        {
            var p=new ProgressData(); var r=RunState.Start(21,RunState.StartChoices(21,UnlockRules.Weapons(p))[0],p);
            r.StartPickPending=false; r.Coins=100; r.OfferCards.Clear(); r.OfferCards.Add(OfferSlot.From(new Card(CardKind.SkillUp,r.Build.WeaponId,2)));
            var view=PrepareView.Create(root.transform,art); view.SkillPicked+=(i,b)=>r.Pick(i,b); view.Show(r);
            view.GetComponentsInChildren<PixelButton>().Single(b=>b.name=="SkillB").SimulateClick();
            Assert.That((r.Build.SkillLevel,r.Build.SkillBranch),Is.EqualTo((2,SkillBranch.B)));
            r.OfferCards.Add(OfferSlot.From(new Card(CardKind.SkillUp,r.Build.WeaponId,3))); view.Show(r);
            view.GetComponentsInChildren<PixelButton>().Single(b=>b.name=="SkillA").SimulateClick();
            Assert.That((r.Build.SkillLevel,r.Build.SkillBranch),Is.EqualTo((3,SkillBranch.B)));
        }
        [Test]
        public void RolesBarsCodexAndPickTextFit()
        {
            var pick=PickWeaponView.Create(root.transform,art); pick.Show(new[]{"blade","aegis","volley"});
            Assert.That(pick.GetComponentsInChildren<WeaponProfileView>().Select(v=>v.RoleText),Is.EqualTo(new[]{"BRUISER","GUARDIAN","RANGER"}));
            Bb0102Tests.AssertTextFitsAndNoOverlap(pick.transform);
            pick.Hide(); var menu=MenuView.Create(root.transform,art); menu.ShowVersus();
            Assert.That(menu.GetComponentsInChildren<WeaponProfileView>().Length,Is.EqualTo(2));
            Bb0102Tests.AssertTextFitsAndNoOverlap(menu.transform);
            menu.gameObject.SetActive(false); var codex=SkillCodexView.Create(root.transform,art); codex.Show();
            Bb0102Tests.AssertTextFitsAndNoOverlap(codex.transform);
        }
        [Test]
        public void SkillAudioIdsExistAndMissingClipPlaysNothing()
        {
            var empty=ScriptableObject.CreateInstance<SfxLibrary>();
            try
            {
                var director=AudioDirector.Create(root.transform,empty,new MemorySaveStore());
                Assert.That(AudioIds.Cues,Is.SupersetOf(AudioIds.SkillCues));
                foreach(var data in SkillDefinition.All)
                {
                    Assert.That(AudioIds.SkillCues,Does.Contain(data.AudioId));
                    director.OnSimEvent(new SimEvent{Type=SimEventType.SkillFired,SkillId=data.Id,A=0,Tick=600},null);
                }
                Assert.That(director.Played,Is.Empty);
            }
            finally { Object.DestroyImmediate(empty); }
        }
    }
}
