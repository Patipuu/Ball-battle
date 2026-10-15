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

        /// <summary>Prepare → FIGHT → result. <paramref name="started"/>: the fight already started (retry pick starts it).</summary>
        static void PlayFight(RunFlow f, bool started = false)
        {
            if (!started) f.Fight();
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Countdown));
            f.Tick(2.1f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Playing));
            while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
            f.OnMatchEnded(lastMatch);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightOver));
            f.Tick(2.1f);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.FightResult), "result screen after every fight");
            f.NextAfterResult();
        }

        [Test]
        public void FirstFightLossOpensFreeRetryPickThenOnlySecondLossEndsRun()
        {
            var f = Open(new MemorySaveStore());
            f.NewRun(42);
            f.ChooseWeapon(f.StartChoices[0]);
            f.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.DamageUp));
            f.PickStart(0);
            Assert.That(f.Run.CanReroll, Is.True, "start card does not lock Prepare reroll");
            var arenaSeed = f.Run.ArenaSeed;
            var firstSeed = f.Run.FightSeed;
            f.Fight();
            f.Tick(2.1f);
            lastMatch.DealDamage(null, lastMatch.Balls[0], TFix.X(30000), DamageKind.Hazard);
            while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
            f.OnMatchEnded(lastMatch);
            f.Tick(2.1f);
            f.NextAfterResult();
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.StartPick));
            Assert.That(f.Run.FightIndex, Is.Zero);
            Assert.That(f.Run.Build.Hp.ToFloat(), Is.EqualTo(f.Run.Build.MaxHp.ToFloat() * 0.6f).Within(0.001f));
            Assert.That(f.Run.CanReroll, Is.False);
            Assert.That(f.Run.NextPickCost, Is.Zero);
            Assert.That(f.Run.OfferCount, Is.EqualTo(3));
            Assert.That(f.Run.ArenaSeed, Is.EqualTo(arenaSeed));
            Assert.That(f.Run.FightSeed, Is.Not.EqualTo(firstSeed));
            f.Run.OfferCards[0] = OfferSlot.From(new Card(CardKind.DamageUp));
            f.PickStart(0);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Countdown));
            Assert.That(f.Run.CanReroll, Is.False);
            f.Tick(2.1f);
            lastMatch.DealDamage(null, lastMatch.Balls[0], TFix.X(30000), DamageKind.Hazard);
            while (lastMatch.Outcome == MatchOutcome.Ongoing) lastMatch.Step();
            f.OnMatchEnded(lastMatch);
            f.Tick(2.1f);
            f.NextAfterResult();
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Summary));
            Assert.That(f.Run.History.Select(h => h.Retry), Is.EqualTo(new[] { false, true }));
        }

        static uint FirstWinningSeed()
        {
            for (uint seed = 1; seed <= 200; seed++)
            {
                var progress = new ProgressData();
                var choices = RunState.StartChoices(seed, UnlockRules.Weapons(progress));
                var run = RunState.Start(seed, choices[1], progress);
                run.Pick(0);
                run.Pick(0);
                var match = run.CreateMatch();
                while (match.Outcome == MatchOutcome.Ongoing) match.Step();
                if (match.WinnerIndex == 0) return seed;
            }
            Assert.Fail("No first-fight winning seed found in setup seeds 1..200");
            return 0;
        }

        [TestCase(false)]
        [TestCase(true)]
        public void NewRunPickWeaponFightAndResume(bool needsWinningFight)
        {
            var store = new MemorySaveStore();
            var f = Open(store);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Title));
            Assert.That(f.HasSavedRun, Is.False);

            f.NewRun(needsWinningFight ? FirstWinningSeed() : 42);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.PickWeapon));
            Assert.That(f.StartChoices.Count, Is.EqualTo(3));
            f.ChooseWeapon(f.StartChoices[1]);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.StartPick));
            f.PickStart(0);
            Assert.That(f.Current, Is.EqualTo(RunFlow.State.Prepare));

            f.Pick(0);
            var buildAfterPick = f.Run.Build.WeaponId + string.Join(",", f.Run.Build.Traits.Select(t => t.Id + t.Level)) + f.Run.Build.MaxHp;
            PlayFight(f);
            var won = f.Run.History[0].Won;
            if (needsWinningFight) Assert.That(won, Is.True, "setup searched for the first winning seed");
            var expectedScreen = won ? RunFlow.State.Prepare : RunFlow.State.StartPick;
            var expectedFight = won ? 1 : 0;
            Assert.That(f.Current, Is.EqualTo(expectedScreen), "win rewards the next fight; loss opens free retry pick");
            Assert.That(f.Run.FightIndex, Is.EqualTo(expectedFight));
            Assert.That(f.Run.IsRetry, Is.EqualTo(!won));
            var hp = f.Run.Build.Hp;
            var coins = f.Run.Coins;
            var offer = Enumerable.Range(0, f.Run.OfferCount).Select(f.Run.OfferCard).ToArray();

            // Quit and come back: same run, same HP, same cards on the table.
            f.Close();
            var resumed = Open(store);
            Assert.That(resumed.HasSavedRun, Is.True);
            resumed.Continue();
            Assert.That(resumed.Current, Is.EqualTo(expectedScreen));
            Assert.That(resumed.Run.FightIndex, Is.EqualTo(expectedFight));
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
            f.PickStart(0);
            var guard = 0;
            // BB-FIX1a: also play through a retry (lost fight → free pick → same fight), whatever the seed's outcomes are.
            while ((f.Current == RunFlow.State.Prepare || f.Current == RunFlow.State.StartPick) && guard++ < 20)
            {
                if (f.Current == RunFlow.State.StartPick)
                {
                    f.PickStart(0);   // retry pick starts the fight
                    PlayFight(f, started: true);
                    continue;
                }
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
            run.Build.Hp = TFix.X(63.27f); // fractional on purpose: JSON must round-trip floats exactly

            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = RunSave.From(run) });
            var back = store.Load();

            Assert.That(back.Progress.Achievements, Is.EqualTo(progress.Achievements));
            Assert.That(back.HasRun, Is.True);
            Assert.That(back.Run.ToRun().Seed, Is.EqualTo(run.Seed));
            Assert.That(back.Run.ToRun().PickedMask, Is.EqualTo(run.PickedMask));
            Assert.That(back.Run.ToRun().Build.Traits.Select(t => t.Id + t.Level), Is.EqualTo(run.Build.Traits.Select(t => t.Id + t.Level)));
            Assert.That(Enumerable.Range(0, back.Run.ToRun().OfferCount).Select(back.Run.ToRun().OfferCard), Is.EqualTo(Enumerable.Range(0, run.OfferCount).Select(run.OfferCard)));
            Assert.That(StepAndHash(back.Run.ToRun().CreateMatch()), Is.EqualTo(StepAndHash(run.CreateMatch())), "restored run plays the same fight");
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
            f.PickStart(0);
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
            store.Save(new SaveData { Progress = progress, HasRun = true, Run = RunSave.From(run) });

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
                progress.Achievements.Add(UnlockRules.Boss3);
                progress.RunsPlayed = 9;
                progress.RunsWon = 2;
                var old = new SaveData { Version = 2, Progress = progress, HasRun = true }; // v3 is migrated in BB-10a
                var file = new FileSaveStore(Path.Combine(dir, "save.json"));
                File.WriteAllText(file.Path, JsonUtility.ToJson(old));

                var loaded = file.Load();
                Assert.That(loaded.Version, Is.EqualTo(SaveData.CurrentVersion));
                Assert.That(loaded.HasRun, Is.False);
                Assert.That(loaded.Progress.Achievements, Is.EqualTo(progress.Achievements));
                Assert.That(loaded.Progress.RunsPlayed, Is.EqualTo(9));
                Assert.That(loaded.Progress.RunsWon, Is.EqualTo(2));
                Assert.That(UnlockRules.Weapons(loaded.Progress), Does.Contain("venom"));
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
