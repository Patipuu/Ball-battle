using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Skills;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class SkillTests
    {
        static MatchSim Fight(string id, int level = 1, SkillBranch branch = SkillBranch.None)
        {
            var cfg = Stage.StillConfig(); cfg.HitHitstopTicks = cfg.ParryHitstopTicks = 0;
            var player = new BallLoadout(WeaponRegistry.Create(id)) { MaxHp = 9999, SkillLevel = level, SkillBranch = branch };
            var m = Stage.Build(cfg, (player, FT.V(-60, 0), 0f), (new BallLoadout(new HarmlessRule()) { MaxHp = 9999 }, FT.V(60, 0), 0f));
            return m;
        }
        static void Park(MatchSim m) => Stage.Park(m, FT.V(-60, 0), FT.V(60, 0));
        static void To(MatchSim m, int active) { while (m.ActiveTick < active) { Park(m); m.Step(); } }
        static void Satisfy(MatchSim m)
        {
            if (m.Balls[0].Weapon is FangRule fang) fang.SpinDegPerTick = WeaponTuning.FangMaxSpin;
            if (m.Balls[0].Weapon is RigRule rig) rig.OnHit(new HitContext(Fix.Zero, Fix.Zero));
            if (m.Balls[0].Weapon is VenomRule)
                for (var i = 0; i < 3; i++) m.ApplyPoison(m.Balls[1], m.Balls[0], Fix.One, 5000);
        }
        public static IEnumerable<TestCaseData> Clocks()
        {
            var ids = new[] { "blade", "fang", "pike", "brawler", "volley", "venom", "aegis", "rig" };
            var cooldowns = new[] { 600,720,540,600,720,600,720,720 };
            var durations = new[] { 0,180,120,90,0,240,120,0 };
            for (var i = 0; i < ids.Length; i++)
                foreach (var spec in new[] { new SkillSpec(1), new SkillSpec(2, SkillBranch.A), new SkillSpec(3, SkillBranch.A), new SkillSpec(2, SkillBranch.B), new SkillSpec(3, SkillBranch.B) })
                {
                    var cd = ids[i] == "volley" && spec.Branch == SkillBranch.A ? (spec.Level == 2 ? 600 : 480) : cooldowns[i];
                    var duration = durations[i];
                    if (ids[i] == "fang" && spec.Branch == SkillBranch.A) duration = spec.Level == 2 ? 283 : 386;   // BB-BAL1: per rank 90 -> 103
                    if (ids[i] == "pike" && spec.Branch == SkillBranch.B) duration = spec.Level == 2 ? 189 : 258;   // BB-BAL1: per rank 60 -> 69
                    if (ids[i] == "brawler" && spec.Branch == SkillBranch.B) duration = spec.Level == 2 ? 159 : 228;   // BB-BAL1: per rank 60 -> 69
                    if (ids[i] == "aegis" && spec.Branch == SkillBranch.A) duration = spec.Level == 2 ? 166 : 212;   // BB-BAL1: per rank 40 -> 46
                    yield return new TestCaseData(ids[i], spec.Level, spec.Branch, cd, duration);
                }
        }
        [TestCaseSource(nameof(Clocks))]
        public void AuthoredCooldownAndDurationFireOnActiveTick(string id, int level, SkillBranch branch, int cooldown, int duration)
        {
            var m = Fight(id, level, branch); Satisfy(m);
            var skill = m.Balls[0].Skill;
            Assert.That(skill.CooldownTicks, Is.EqualTo(cooldown));
            Assert.That(skill.DurationTicks, Is.EqualTo(duration));
            To(m, cooldown - 1); Assert.That(skill.FireCount, Is.Zero);
            m.ExtendHitstop(17);
            for (var i = 0; i < 17; i++) m.Step();
            Assert.That(m.ActiveTick, Is.EqualTo(cooldown - 1)); Assert.That(skill.FireCount, Is.Zero);
            Park(m); m.Step();
            var fired = m.Events.Single(e => e.Type == SimEventType.SkillFired);
            Assert.That((fired.ActiveTick, fired.SkillLevel, fired.SkillBranch), Is.EqualTo((cooldown, level, branch)));
            Assert.That(skill.ReadyAt, Is.EqualTo(cooldown * 2));
            if (duration == 0) return;
            To(m, cooldown + duration - 1); Assert.That(skill.Active, Is.True);
            m.ExtendHitstop(21); for (var i = 0; i < 21; i++) m.Step();
            Assert.That(skill.Active, Is.True);
            Park(m); m.Step();
            Assert.That(skill.Active, Is.False);
            Assert.That(m.Events.Any(e => e.Type == SimEventType.SkillEnded), Is.True);
        }
        [TestCase("fang",720)] [TestCase("venom",600)] [TestCase("rig",720)]
        public void ConditionalSkillWaitsWithoutMaximumWait(string id, int cooldown)
        {
            var m = Fight(id); To(m, cooldown + 1000);
            Assert.That(m.Balls[0].Skill.FireCount, Is.Zero);
            Satisfy(m); Park(m); m.Step();
            Assert.That(m.Balls[0].Skill.FireCount, Is.EqualTo(1));
            Assert.That(m.Balls[0].Skill.ReadyAt, Is.EqualTo(m.ActiveTick + cooldown));
        }
        [Test]
        public void AuthoredBranchAmountsAreExact()
        {
            var bladeA = Fight("blade",3,SkillBranch.A).Balls[0].Skill; var bladeB = Fight("blade",3,SkillBranch.B).Balls[0].Skill;
            Assert.That((bladeA.HitCharges,bladeA.HitBonus), Is.EqualTo((7,Fix.Half)));
            Assert.That((bladeB.HitCharges,bladeB.HitBonus), Is.EqualTo((3,Fix.Ratio(69,40))));   // BB-BAL1: 3/2 -> 69/40
            Assert.That(Fight("fang",2,SkillBranch.B).Balls[0].Skill.SpinGainMultiplier, Is.EqualTo(Fix.Ratio(69,40)));
            Assert.That(Fight("fang",3,SkillBranch.B).Balls[0].Skill.SpinGainMultiplier, Is.EqualTo(Fix.Ratio(23,10)));
            Assert.That(Fight("pike",2,SkillBranch.A).Balls[0].Skill.LengthBonus, Is.EqualTo(Fix.Ratio(69,120)));
            Assert.That(Fight("pike",3,SkillBranch.A).Balls[0].Skill.LengthBonus, Is.EqualTo(Fix.Ratio(92,120)));
            Assert.That(Fight("brawler",2,SkillBranch.A).Balls[0].Skill.BodyMultiplier, Is.EqualTo(Fix.Ratio(161,80)));
            Assert.That(Fight("brawler",3,SkillBranch.A).Balls[0].Skill.BodyMultiplier, Is.EqualTo(Fix.Ratio(23,10)));
            Assert.That(Fight("volley",2,SkillBranch.B).Balls[0].Skill.ArrowBonus, Is.EqualTo(Fix.Ratio(1,4)));
            Assert.That(Fight("volley",3,SkillBranch.B).Balls[0].Skill.ArrowBonus, Is.EqualTo(Fix.Half));
            Assert.That(Fight("venom",2,SkillBranch.A).Balls[0].Skill.DrainFraction, Is.EqualTo(Fix.Ratio(23,30)));
            Assert.That(Fight("venom",3,SkillBranch.A).Balls[0].Skill.DrainFraction, Is.EqualTo(Fix.Ratio(69,80)));
            Assert.That(Fight("venom",2,SkillBranch.B).Balls[0].Skill.PoisonExtraTicks, Is.EqualTo(69));
            Assert.That(Fight("venom",3,SkillBranch.B).Balls[0].Skill.PoisonExtraTicks, Is.EqualTo(138));
            Assert.That(Fight("aegis",2,SkillBranch.B).Balls[0].Skill.ParryReflect, Is.EqualTo(Fix.Ratio(23,30)));
            Assert.That(Fight("aegis",3,SkillBranch.B).Balls[0].Skill.ParryReflect, Is.EqualTo(Fix.Ratio(69,80)));
            Assert.That((Fight("rig",2,SkillBranch.A).Balls[0].Skill.VolleyCharges,Fight("rig",3,SkillBranch.A).Balls[0].Skill.VolleyCharges), Is.EqualTo((3,4)));
            Assert.That((Fight("rig",2,SkillBranch.B).Balls[0].Skill.DeployCount,Fight("rig",3,SkillBranch.B).Balls[0].Skill.DeployCount), Is.EqualTo((1,2)));
        }
        [Test]
        public void WhetstoneConsumesOnlyItsNextWeaponHits()
        {
            var m = Fight("blade"); To(m,600); var skill=m.Balls[0].Skill;
            Assert.That(skill.ModifyHit(4,DamageKind.Weapon),Is.EqualTo(Fix.Ratio(9,2)));
            skill.LandedHit(DamageKind.Projectile); Assert.That(skill.Charges,Is.EqualTo(3));
            for(var i=0;i<3;i++) skill.LandedHit(DamageKind.Weapon);
            Assert.That(skill.Active,Is.False); Assert.That(skill.ModifyHit(4,DamageKind.Weapon),Is.EqualTo((Fix)4));
        }
        [Test]
        public void PikeGrowthSurvivesLungeExpiryAndStillRespectsCap()
        {
            var m=Fight("pike"); To(m,540); var p=(PikeRule)m.Balls[0].Weapon;
            var before=p.BladeLength; p.OnHit(new HitContext(Fix.Zero,Fix.Zero));
            Assert.That(p.BladeLength,Is.GreaterThan(before));
            To(m,660); Assert.That(p.BladeLength,Is.EqualTo(WeaponTuning.PikeStartLength+WeaponTuning.PikeGrowthPerHit));
            for(var i=0;i<500;i++) p.OnHit(new HitContext(Fix.Zero,Fix.Zero));
            Assert.That(p.BladeLength,Is.EqualTo(WeaponTuning.PikeMaxLength));
        }
        [Test]
        public void SkillUpKeepsChosenBranchAndNeverOffersAtLevel3()
        {
            var build=new RunBuild{WeaponId="blade"}; new Card(CardKind.SkillUp).ApplyTo(build,SkillBranch.B);
            new Card(CardKind.SkillUp).ApplyTo(build,SkillBranch.A);
            Assert.That((build.SkillLevel,build.SkillBranch),Is.EqualTo((3,SkillBranch.B)));
            for(var i=0;i<100;i++) Assert.That(CardOffer.Generate(1,1,i,build,new[]{"blade","pike"},new string[0]).Any(c=>c.Kind==CardKind.SkillUp||c.Kind==CardKind.SwapWeapon),Is.False);
        }
        [Test]
        public void GreedyComparesBothBranchesDeterministically()
        {
            foreach(var definition in SkillDefinition.All)
            {
                var b=new RunBuild{WeaponId=definition.WeaponId}; var branch=SkillChoice.Choose(b);
                Assert.That(SkillChoice.Score(b,branch),Is.GreaterThanOrEqualTo(SkillChoice.Score(b,branch==SkillBranch.A?SkillBranch.B:SkillBranch.A)));
                Assert.That(SkillChoice.Choose(b),Is.EqualTo(branch));
            }
        }
        [Test]
        public void MirrorSkillsAreIndependentAndTraceCarriesActiveTick()
        {
            var m=new MatchSim(Stage.StillConfig(),1,new[]{WeaponRegistry.Create("blade"),WeaponRegistry.Create("blade")});
            To(m,600); var events=m.Events.Where(e=>e.Type==SimEventType.SkillFired).ToList();
            Assert.That(events.Count,Is.EqualTo(2)); Assert.That(m.Balls[0].Skill,Is.Not.SameAs(m.Balls[1].Skill));
            Assert.That(SimTrace.SkillLine(events[0]),Does.Contain("ActiveTick=600"));
            Assert.That(SimTrace.SkillLine(events[0]),Does.Contain("level=1 branch=None"));
        }
        [Test]
        public void OnlyMimicCopiesSkillAndTheForceOverrideIsTransient()
        {
            var progress=RunBot.BotProgress();
            var run=RunState.Start(21,RunState.StartChoices(21,UnlockRules.Weapons(progress))[0],progress);
            run.Build.SkillLevel=3; run.Build.SkillBranch=SkillBranch.B; run.FightIndex=7; run.BossIds[1]=BossIds.Mimic;
            var m=run.CreateMatch(); Assert.That((m.Balls[1].Skill.Level,m.Balls[1].Skill.Branch),Is.EqualTo((3,SkillBranch.B)));
            run.BossIds[1]=BossIds.Warden; Assert.That(run.CreateMatch().Balls[1].Skill,Is.Null);
            var forced=run.CreateMatch(null,new SkillSpec(2,SkillBranch.A));
            Assert.That((forced.Balls[0].Skill.Level,forced.Balls[0].Skill.Branch),Is.EqualTo((2,SkillBranch.A)));
            Assert.That((run.Build.SkillLevel,run.Build.SkillBranch),Is.EqualTo((3,SkillBranch.B)));
        }

        [Test]
        public void FrenzyPreservesGainAndFuryMultipliesIt()
        {
            var m=Fight("fang",3,SkillBranch.B); Satisfy(m); To(m,720);
            var fang=(FangRule)m.Balls[0].Weapon; fang.SpinDegPerTick=0; var gain=fang.SpinGain;
            fang.OnHit(new HitContext(Fix.Zero,Fix.Zero));
            Assert.That(fang.SpinGain,Is.EqualTo(gain)); Assert.That(fang.SpinDegPerTick,Is.EqualTo(FixMath.Min(WeaponTuning.FangMaxSpin,gain*Fix.Ratio(23,10))));   // BB-BAL1: fury L3 2 -> 2.3
            To(m,900); fang.OnHit(new HitContext(Fix.Zero,Fix.Zero));
            Assert.That(fang.SpinGain,Is.EqualTo(gain*WeaponTuning.FangSpinGainDecay));
        }
        [Test]
        public void ChargeSetsCurrentCapAndOnlyItsFirstBodyHitIsBoosted()
        {
            var m=Fight("brawler"); To(m,600); var b=m.Balls[0]; var skill=b.Skill;
            Assert.That(b.Vel.Length,Is.EqualTo(m.Config.MaxSpeed+b.Weapon.MaxSpeedBonus));
            Assert.That(skill.ModifyHit(4,DamageKind.Weapon),Is.EqualTo((Fix)4*Fix.Ratio(27,20)));   // BB-BAL1: x1.5 -> x1.35
            skill.LandedHit(DamageKind.Weapon); Assert.That(skill.Active,Is.True);
            Assert.That(skill.ModifyHit(4,DamageKind.Weapon),Is.EqualTo((Fix)4));
        }
        [TestCase(2)] [TestCase(3)]
        public void HeavyBarrageAddsDamageOnlyToTheExtraVolley(int level)
        {
            var m=Fight("volley",level,SkillBranch.B); To(m,720);
            var normal=((VolleyRule)m.Balls[0].Weapon).ArrowDamage;
            Assert.That(Enumerable.Range(0,ProjectilePool.Capacity).Any(i=>m.Projectiles.Get(i).Active&&m.Projectiles.Get(i).Damage==normal+Fix.Ratio(level-1,4)),Is.True);
            Assert.That(m.Events.Count(e=>e.Type==SimEventType.SkillFired),Is.EqualTo(1));
        }
        [Test]
        public void DrainHealsOnlyItsOwnersShareAndRotExtendsAppliedPoison()
        {
            var m=Fight("venom",3,SkillBranch.B);
            m.Balls[0].Hp=1000;
            m.ApplyPoison(m.Balls[1],m.Balls[0],1,5000); m.ApplyPoison(m.Balls[1],m.Balls[0],1,5000);
            m.ApplyPoison(m.Balls[1],m.Balls[1],1,5000);
            To(m,599); var before=m.Balls[0].Hp; Park(m); m.Step();
            Assert.That(m.Balls[0].Hp-before,Is.EqualTo(Fix.One),"half of the owner's 2 damage, not half of all 3");
            m.ApplyPoison(m.Balls[1],m.Balls[0],1,120);
            Assert.That(m.Balls[1].Status.Poison[3].TicksLeft,Is.EqualTo(300));   // BB-BAL1: 120 + 138 = 258, rounded up to whole seconds
        }
        [Test]
        public void OverclockHalvesExactlyItsNextTwoTurretIntervals()
        {
            var m=Fight("rig"); Satisfy(m); To(m,720);
            var shots=new List<int>(); var end=720+WeaponTuning.RigFireIntervalTicks*2;
            while(m.ActiveTick<end){Park(m);m.Step();if(m.Events.Any(e=>e.Type==SimEventType.ProjectileFired))shots.Add(m.ActiveTick);}
            var half=WeaponTuning.RigFireIntervalTicks/2;
            Assert.That(shots.Take(3),Is.EqualTo(new[]{720+half-1,720+half*2-1,720+half*2-1+WeaponTuning.RigFireIntervalTicks}));
            Assert.That(m.Balls[0].Skill.Active,Is.False);
        }
        [Test]
        public void ReflectedProjectileCannotReflectTwiceOrTriggerOnHitHooks()
        {
            var cfg=Stage.StillConfig();
            var venom=WeaponRegistry.Create("venom"); venom.SpinDegPerTick=Fix.Zero;
            var m=Stage.Build(cfg,(new BallLoadout(new FixedBladeRule(0,25,0)),FT.V(-60,0),0f),
                (new BallLoadout(venom),FT.V(0,0),180f));
            var spec=new ProjectileSpec{Damage=7,Radius=2,Hits=1,LifetimeTicks=100};
            m.FireProjectile(m.Balls[0],FT.V(-40,0),FT.V(6,0),spec);
            var reflections=0; var reflectedHit=false;
            for(var i=0;i<80&&m.Outcome==MatchOutcome.Ongoing;i++)
            {
                m.Step(); reflections+=m.Events.Count(e=>e.Type==SimEventType.ProjectileDeflected);
                reflectedHit|=m.Events.Any(e=>e.Type==SimEventType.ProjectileHit&&e.Reflected&&e.A==1&&e.B==0);
            }
            Assert.That(reflections,Is.EqualTo(1)); Assert.That(reflectedHit,Is.True);
            Assert.That(m.Balls[0].Status.PoisonStackCount,Is.Zero,"Venom on-hit never runs for the reflected shot");
            Assert.That(m.Balls[0].LastAttacker,Is.EqualTo(1));
        }
    }
}
