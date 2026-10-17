using System.Linq;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;

namespace BallBattle.Tests
{
    /// <summary>BB-12a: save v5 (ball levels, gold, chests), migration with welcome chests, chests awarded by the Run flow.</summary>
    public class BbMeta1Tests
    {
        const string V4WithRun =
            "{\"Version\":4,\"Progress\":{\"Achievements\":[\"boss1\",\"win1\"],\"RunsPlayed\":5,\"RunsWon\":2,\"BestFight\":8},\"HasRun\":true," +
            "\"Run\":{\"SeedBits\":5,\"FightIndex\":2,\"Coins\":7,\"Build\":{\"WeaponId\":\"blade\",\"MaxHp\":100.0,\"Hp\":50.0,\"SkillLevel\":1}," +
            "\"History\":[{\"FightIndex\":0,\"Won\":true,\"EnemyWeapon\":\"fang\",\"HpAfter\":80.0},{\"FightIndex\":1,\"Won\":true,\"EnemyWeapon\":\"pike\",\"HpAfter\":50.0}]}}";

        [Test]
        public void V4SaveMigratesWithWelcomeChestsAndKeepsTheRun()
        {
            var d = new MemorySaveStore { Json = V4WithRun }.Load();
            Assert.That(d.Version, Is.EqualTo(SaveData.CurrentVersion).And.EqualTo(6));
            Assert.That(d.HasRun, Is.True, "the run in progress survives v4 -> v5");
            Assert.That(d.Run.History.Count, Is.EqualTo(2));
            Assert.That(d.Progress.PendingChests, Is.EqualTo(new[] { ChestTier.Silver, ChestTier.Silver }), "one per run won");
            Assert.That(d.Progress.Gold, Is.Zero);
            Assert.That(d.Progress.Balls, Is.Empty);
            Assert.That(MetaRules.LevelOf(d.Progress, "blade"), Is.EqualTo(1));
            Assert.That(d.Progress.Achievements, Is.EqualTo(new[] { "boss1", "win1" }));
        }

        [Test]
        public void MigrationRunsOnceAndNewSavesGetNoWelcomeChests()
        {
            var store = new MemorySaveStore { Json = V4WithRun };
            var first = store.Load();
            store.Save(first);
            var again = store.Load();
            Assert.That(again.Progress.PendingChests.Count, Is.EqualTo(2), "loading a v5 save never grants them again");
            Assert.That(new MemorySaveStore().Load().Progress.PendingChests, Is.Empty);
        }

        [Test]
        public void BallLevelsGoldAndChestsSurviveSaveAndLoad()
        {
            var p = new ProgressData { RunsWon = 1 };
            var chest = MetaRules.Open(p, ChestTier.Gold, "fang", 31);
            MetaRules.Ball(p, "fang").Shards += 10;
            p.Gold += 100;
            Assert.That(MetaRules.Upgrade(p, "fang"), Is.True);
            p.PendingChests.Add(ChestTier.Wood);
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = p });
            var back = store.Load().Progress;
            Assert.That(MetaRules.LevelOf(back, "fang"), Is.EqualTo(2));
            Assert.That(back.Balls.Select(b => b.Id + b.Level + ":" + b.Shards), Is.EqualTo(p.Balls.Select(b => b.Id + b.Level + ":" + b.Shards)));
            Assert.That((back.Gold, back.ChestsOpened), Is.EqualTo((p.Gold, 1)));
            Assert.That(back.PendingChests, Is.EqualTo(new[] { ChestTier.Wood }));
            Assert.That(chest.TotalGold, Is.EqualTo(MetaTuning.ChestGold[2] + chest.ConvertedGold));
        }

        [Test]
        public void AbandoningASavedRunAwardsItsChestOnce()
        {
            var progress = new ProgressData();
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(progress))[0], progress);
            run.History.Add(new FightRecord { FightIndex = 0, Won = true, EnemyWeapon = "fang", HpAfter = run.Build.Hp });
            var played = run.Build.WeaponId;
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = RunSave.From(run) });
            var flow = new RunFlow(store);
            flow.Open();
            Assert.That(store.Load().HasRun, Is.True, "the saved run is valid");
            flow.NewRun(9);
            flow.ChooseWeapon(flow.StartChoices[0]);
            Assert.That(flow.LastChest, Is.Not.Null);
            Assert.That((flow.LastChest.Tier, flow.LastChest.PlayedBall), Is.EqualTo((ChestTier.Wood, played)), "1 fight won, boss 1 not reached");
            var saved = store.Load().Progress;
            Assert.That(saved.ChestsOpened, Is.EqualTo(1));
            Assert.That(saved.Gold, Is.EqualTo(flow.LastChest.TotalGold));
            Assert.That(MetaRules.Ball(saved, played).Shards, Is.GreaterThanOrEqualTo(MetaTuning.PlayedShards[0]));
        }

        [Test]
        public void AbandonedRunWithoutFightsGivesNothing()
        {
            Assert.That(RunFlow.AwardAbandoned(new ProgressData(), new RunSave()), Is.Null);
        }
    }
}
