using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using BallBattle.Sim;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace BallBattle.Tests
{
    public class RunFlowTests
    {
        static MatchSim lastMatch;

        static RunFlow Open(MemorySaveStore store)
        {
            var f = new RunFlow(store, 2f, 2f);
            f.MatchPrepared += m => lastMatch = m;
            f.Open();
            return f;
        }

        static void PlayFight(RunFlow f)
        {
            f.Fight();
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Countdown));
            f.Tick(2.1f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Playing));
            while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
            f.OnMatchEnded(lastMatch);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightOver));
            f.Tick(2.1f);
        }

        [Test]
        public void NewRunPickWeaponFightAndResume()
        {
            var store = new MemorySaveStore();
            var f = Open(store);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Title));
            Assert.That(f.HasSavedRun, Is.False);

            f.NewRun(42);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.PickWeapon));
            Assert.That(f.StartChoices.Count, Is.EqualTo(3));
            f.ChooseWeapon(f.StartChoices[1]);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Prepare));

            f.Pick(0);
            var buildAfterPick = f.Run.Build.WeaponId + string.Join(",", f.Run.Build.Traits.Select(t => t.Id + t.Level)) + f.Run.Build.MaxHp;
            PlayFight(f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Prepare));
            Assert.That(f.Run.FightIndex, Is.EqualTo(1));
            var hp = f.Run.Build.Hp;
            var coins = f.Run.Coins;
            var offer = Enumerable.Range(0, f.Run.OfferCount).Select(f.Run.OfferCard).ToArray();

            // Quit and come back: same run, same HP, same cards on the table.
            f.Close();
            var resumed = Open(store);
            Assert.That(resumed.HasSavedRun, Is.True);
            resumed.Continue();
            Assert.That(resumed.Current, Is.EqualTo(RunFlow.State.Prepare));
            Assert.That(resumed.Run.FightIndex, Is.EqualTo(1));
            Assert.That(resumed.Run.Build.Hp, Is.EqualTo(hp));
            Assert.That(resumed.Run.Coins, Is.EqualTo(coins));
            Assert.That(Enumerable.Range(0, resumed.Run.OfferCount).Select(resumed.Run.OfferCard), Is.EqualTo(offer));
            Assert.That(resumed.Run.History.Count, Is.EqualTo(1));
            Assert.That(resumed.Run.Build.WeaponId + string.Join(",", resumed.Run.Build.Traits.Select(t => t.Id + t.Level)) + resumed.Run.Build.MaxHp,
                        Is.EqualTo(buildAfterPick));
        }

        [Test]
        public void FullRunEndsInSummaryAndClearsTheSavedRun()
        {
            var store = new MemorySaveStore();
            var f = Open(store);
            f.NewRun(7);
            f.ChooseWeapon(f.StartChoices[0]);
            var guard = 0;
            while (f.Current == RunFlow.State.Prepare && guard++ < 20)
            {
                if (f.Run.CanPick(0)) f.Pick(0);
                PlayFight(f);
            }

            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Summary));
            Assert.That(f.Run.Over, Is.True);
            Assert.That(f.Progress.RunsPlayed, Is.EqualTo(1));
            var reopened = Open(store);
            Assert.That(reopened.HasSavedRun, Is.False);
            Assert.That(reopened.Progress.RunsPlayed, Is.EqualTo(1), "progress is saved");
        }

        [Test]
        public void StaleInputIsIgnored()
        {
            var f = Open(new MemorySaveStore());
            f.Pick(0);
            f.Fight();
            f.ChooseWeapon("blade");
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Title));
            f.NewRun(3);
            f.ChooseWeapon("not-a-choice");
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.PickWeapon));
        }

        [Test]
        public void SaveRoundTripKeepsEveryRunField()
        {
            var progress = new ProgressData();
            progress.Achievements.Add(UnlockRules.Boss1);
            var run = RunState.Start(99, RunState.StartChoices(99, UnlockRules.Weapons(progress))[0], progress);
            run.Coins = 9;
            run.Pick(1);
            run.Build.Traits.Add(new TraitSlot("vampire", 2));
            run.Build.Hp = 63.27f; // fractional on purpose: JSON must round-trip floats exactly

            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = run });
            var back = store.Load();

            Assert.That(back.Progress.Achievements, Is.EqualTo(progress.Achievements));
            Assert.That(back.HasRun, Is.True);
            Assert.That(back.Run.Seed, Is.EqualTo(run.Seed));
            Assert.That(back.Run.PickedMask, Is.EqualTo(run.PickedMask));
            Assert.That(back.Run.Build.Traits.Select(t => t.Id + t.Level), Is.EqualTo(run.Build.Traits.Select(t => t.Id + t.Level)));
            Assert.That(Enumerable.Range(0, back.Run.OfferCount).Select(back.Run.OfferCard), Is.EqualTo(Enumerable.Range(0, run.OfferCount).Select(run.OfferCard)));
            Assert.That(StepAndHash(back.Run.CreateMatch()), Is.EqualTo(StepAndHash(run.CreateMatch())), "restored run plays the same fight");
        }

        static ulong StepAndHash(MatchSim m)
        {
            for (var i = 0; i < 600; i++) m.Step();
            return m.ComputeHash();
        }

        [Test]
        public void QuittingAfterSeeingTheResultReplaysTheSameFight()
        {
            var store = new MemorySaveStore();
            var f = Open(store);
            f.NewRun(11);
            f.ChooseWeapon(f.StartChoices[0]);
            var coins = f.Run.Coins;
            f.Fight();
            f.Tick(2.1f);
            var seen = lastMatch;
            while (seen.Outcome == MatchOutcome.Ongoing) seen.Step();
            f.OnMatchEnded(seen);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightOver));
            f.Close(); // quit before the result is applied

            var resumed = Open(store);
            resumed.Continue();
            Assert.That(resumed.Current, Is.EqualTo(RunFlow.State.Countdown), "straight back into the fight, no Prepare screen");
            Assert.That(resumed.Run.History.Count, Is.EqualTo(0));
            Assert.That(resumed.Run.Coins, Is.EqualTo(coins));
            Assert.That(resumed.Run.NextPickCost, Is.EqualTo(-1), "locked run: no picks");
            Assert.That(resumed.Run.CanReroll, Is.False);
            var replay = lastMatch;
            while (replay.Outcome == MatchOutcome.Ongoing) replay.Step();
            Assert.That(replay.ComputeHash(), Is.EqualTo(seen.ComputeHash()), "same build, same seed, same result");
        }

        [Test]
        public void InvalidSavedRunIsDroppedButProgressKept()
        {
            var progress = new ProgressData();
            progress.Achievements.Add(UnlockRules.Boss1);
            var run = RunState.Start(5, RunState.StartChoices(5, UnlockRules.Weapons(progress))[0], progress);
            run.Build.WeaponId = "removed-weapon";
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = run });

            var f = Open(store);
            Assert.That(f.HasSavedRun, Is.False);
            Assert.That(f.Progress.Achievements, Is.EqualTo(progress.Achievements));
        }

        [Test]
        public void OlderSaveVersionKeepsProgressAndDropsTheRun()
        {
            var dir = Path.Combine(Path.GetTempPath(), "bb-save-test-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try
            {
                var progress = new ProgressData();
                progress.Achievements.Add(UnlockRules.Boss1);
                var old = new SaveData { Version = SaveData.CurrentVersion - 1, Progress = progress, HasRun = true };
                var file = new FileSaveStore(Path.Combine(dir, "save.json"));
                File.WriteAllText(file.Path, JsonUtility.ToJson(old));

                var loaded = file.Load();
                Assert.That(loaded.Version, Is.EqualTo(SaveData.CurrentVersion));
                Assert.That(loaded.HasRun, Is.False);
                Assert.That(loaded.Progress.Achievements, Is.EqualTo(progress.Achievements));
            }
            finally { Directory.Delete(dir, true); }
        }

        [Test]
        public void FailedSaveIsLoggedAndNeverThrows()
        {
            var blocker = Path.Combine(Path.GetTempPath(), "bb-save-blocker-" + System.Guid.NewGuid().ToString("N"));
            File.WriteAllText(blocker, "a file where a folder should be");
            try
            {
                var file = new FileSaveStore(Path.Combine(blocker, "save.json"));
                LogAssert.Expect(LogType.Error, new Regex("Could not save"));
                Assert.DoesNotThrow(() => file.Save(new SaveData()));
            }
            finally { File.Delete(blocker); }
        }
    }
}
