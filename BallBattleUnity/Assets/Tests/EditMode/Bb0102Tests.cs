using System.Collections.Generic;
using System.Linq;
using BallBattle.Sim.Run;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BallBattle.Tests
{
    /// <summary>BB-01/02: Run entry and rules from RunTuning, next unlock goal, summary milestones, layout fit, screenshots.</summary>
    public class Bb0102Tests
    {
        const float HalfW = TextWrap.ScreenWidth / 2f;
        const float HalfH = TextWrap.ScreenHeight / 2f;

        GameObject root;
        ArtLibrary art;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>("Assets/Art/ArtLibrary.asset");
            Assert.That(art, Is.Not.Null, "run BallBattle/Build Scenes");
            root = new GameObject("Bb0102Root");
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(root);

        static ProgressData P(int bestFight = 0, int runsWon = 0, params string[] achievements)
        {
            var p = new ProgressData { BestFight = bestFight, RunsWon = runsWon, RunsPlayed = runsWon };
            p.Achievements.AddRange(achievements);
            return p;
        }

        static void AssertGoal(NextGoalInfo g, string id, int current, int max)
        {
            Assert.That(g.AllDone, Is.False);
            Assert.That(g.GoalId, Is.EqualTo(id));
            Assert.That(g.Current, Is.EqualTo(current), "current");
            Assert.That(g.Max, Is.EqualTo(max), "max");
        }

        // ---- B. NextGoal ----

        [Test]
        public void NextGoal1_NewSave_BeatShellKing_0of4()
        {
            var g = UnlockRules.NextGoal(new ProgressData());
            AssertGoal(g, UnlockRules.Boss1, 0, 4);
            Assert.That(g.RewardIds, Is.EqualTo(new[] { "glass-cannon" }));
        }

        [Test]
        public void NextGoal2_LostAtShellKing_3of4_NeverFullBar() =>
            AssertGoal(UnlockRules.NextGoal(P(bestFight: 4)), UnlockRules.Boss1, 3, 4);

        [Test]
        public void NextGoal3_HasBoss1_BestFight5_WinARun_4of8()
        {
            var g = UnlockRules.NextGoal(P(5, 0, UnlockRules.Boss1));
            AssertGoal(g, UnlockRules.Win1, 4, 8);
            Assert.That(g.RewardIds, Is.EquivalentTo(new[] { "volley", "poison-tip", "aegis", "venom", "bubble", "parry-master" }), "boss2 + win1 rewards merged");
        }

        [Test]
        public void NextGoal4_HasWin1_RunsWon1_Win3_1of3()
        {
            var g = UnlockRules.NextGoal(P(8, 1, UnlockRules.Boss1, UnlockRules.Win1));
            AssertGoal(g, UnlockRules.Win3, 1, 3);
            Assert.That(g.RewardIds, Is.EquivalentTo(new[] { "rig", "twin-blade" }));
        }

        [Test]
        public void NextGoal5_OldSaveWithBoss3_Win3_1of3() =>
            AssertGoal(UnlockRules.NextGoal(P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Boss3, UnlockRules.Win1)), UnlockRules.Win3, 1, 3);

        [Test]
        public void NextGoal6_HasWin3_AllDone()
        {
            var g = UnlockRules.NextGoal(P(8, 3, UnlockRules.Boss1, UnlockRules.Win1, UnlockRules.Win3));
            Assert.That(g.AllDone, Is.True);
            Assert.That(g.RewardIds, Is.Empty);
        }

        [Test]
        public void Boss2AloneReachesWinARun() =>
            AssertGoal(UnlockRules.NextGoal(P(8, 0, UnlockRules.Boss1, UnlockRules.Boss2)), UnlockRules.Win3, 0, 3);

        // ---- A. Rules from RunTuning ----

        [Test]
        public void RulesLinesComeFromRunTuning()
        {
            Assert.That(RunStrings.RulesSummary, Is.EqualTo("8 FIGHTS  2 BOSSES  1 RETRY"));
            Assert.That(RunStrings.BossAt, Is.EqualTo("BOSS AT FIGHT 4 AND 8"));
            Assert.That(RunStrings.LoseOnce, Is.EqualTo("LOSE ONCE: FREE PICK  RETRY AT 60% HP"));
            Assert.That(RunStrings.LoseAgain, Is.EqualTo("LOSE AGAIN: RUN OVER"));
        }

        [Test]
        public void ContinueLineShowsFightAndRetriesLeft()
        {
            var run = new RunState { FightIndex = 2, RetryCount = 0 };
            Assert.That(RunStrings.ContinueLine(run), Is.EqualTo("FIGHT 3/8  RETRY 1"));
            run.RetryCount = 1;
            Assert.That(RunStrings.ContinueLine(run), Is.EqualTo("FIGHT 3/8  RETRY 0"));
        }

        [Test]
        public void MenuMainButtonSaysContinueWithFightAndRetry()
        {
            var menu = MenuView.Create(root.transform, art);
            Assert.That(menu.RunButtonText, Is.EqualTo("RUN"));
            menu.SetRunInfo(new ProgressData(), new RunState { FightIndex = 2 });
            Assert.That(menu.RunButtonText, Is.EqualTo("CONTINUE RUN|FIGHT 3/8  RETRY 1"));
            var asked = new List<string>();
            menu.RunRequested += () => asked.Add("run");
            menu.ContinueRequested += () => asked.Add("continue");
            menu.PressRun();
            menu.SetRunInfo(new ProgressData(), null);
            menu.PressRun();
            Assert.That(asked, Is.EqualTo(new[] { "continue", "run" }));
        }

        // ---- F. Summary ----

        [Test]
        public void WinningTheRunIsOneWinARunMilestone()
        {
            var run = WonRun(retryAt: 0);
            run.Achievements.AddRange(new[] { UnlockRules.Boss2, UnlockRules.Win1 });
            var after = P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1);
            var reached = RunSummaryView.Reached(RunSummaryView.ProgressBefore(after, run), after);
            Assert.That(reached.Select(m => m.GoalId), Is.EqualTo(new[] { UnlockRules.Win1 }));
            Assert.That(reached[0].RewardIds, Has.Count.EqualTo(6));
        }

        [Test]
        public void RetryUsedAtTheFirstLostFight()
        {
            Assert.That(RunSummaryView.RetryUsedAtFight(WonRun(retryAt: 0)), Is.EqualTo(0));
            Assert.That(RunSummaryView.RetryUsedAtFight(WonRun(retryAt: 3)), Is.EqualTo(3));
            Assert.That(RunSummaryView.RetryUsedAtFight(LostRun()), Is.EqualTo(6));
        }

        [Test]
        public void SummaryShowsBossesOutOfBossFightsAndNextGoal()
        {
            var v = RunSummaryView.Create(root.transform, art);
            var run = WonRun(retryAt: 3);
            run.Achievements.AddRange(new[] { UnlockRules.Boss2, UnlockRules.Win1 });
            v.Show(run, P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1));
            Assert.That(v.BossesText.Text, Is.EqualTo("BOSSES 2/2"));
            Assert.That(v.RetryText.Text, Is.EqualTo("RETRY USED AT FIGHT 3"));
            Assert.That(v.BoxText[0], Is.EqualTo("WIN A RUN"));
            Assert.That(v.BoxText.Count(l => l == "WIN A RUN" || l == "BEAT SHELL KING"), Is.EqualTo(1));
            Assert.That(v.Goal.Info.GoalId, Is.EqualTo(UnlockRules.Win3));
        }
        static RunState WonRun(int retryAt)
        {
            var run = RunState.Start(77, RunState.StartChoices(77, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
            run.Pick(0);
            for (var i = 0; i < RunTuning.Fights; i++)
            {
                if (i + 1 == retryAt) run.History.Add(new FightRecord { FightIndex = i, Won = false });
                run.History.Add(new FightRecord { FightIndex = i, Won = true, Retry = i + 1 == retryAt });
            }
            run.RetryCount = retryAt > 0 ? 1 : 0;
            run.FightIndex = RunTuning.Fights;
            run.Over = run.Won = true;
            return run;
        }

        static RunState LostRun()
        {
            var run = RunState.Start(78, RunState.StartChoices(78, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
            run.Pick(0);
            for (var i = 0; i < 5; i++) run.History.Add(new FightRecord { FightIndex = i, Won = true });
            run.History.Add(new FightRecord { FightIndex = 5, Won = false });
            run.History.Add(new FightRecord { FightIndex = 5, Won = false, Retry = true });
            run.FightIndex = 5;
            run.RetryCount = 1;
            run.Over = true;
            run.Achievements.Add(UnlockRules.Boss1);
            return run;
        }

        static RunState SavedRun(int fightIndex, int retryCount)
        {
            var run = RunState.Start(79, RunState.StartChoices(79, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
            run.Pick(0);
            run.FightIndex = fightIndex;
            run.RetryCount = retryCount;
            return run;
        }

        static RunFlow FlowWithSave(ProgressData progress, RunState saved)
        {
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = progress, HasRun = saved != null, Run = saved ?? new RunState() });
            var flow = new RunFlow(store);
            flow.Open();
            return flow;
        }

        /// <summary>Every non-empty text inside 270x480 and no two texts overlapping.</summary>
        static void AssertTextFitsAndNoOverlap(Transform screen)
        {
            var texts = screen.GetComponentsInChildren<PixelText>(false).Where(t => t.Text.Trim().Length > 0).ToList();
            Assert.That(texts, Is.Not.Empty, "nothing drawn");
            foreach (var t in texts)
            {
                var b = t.WorldBounds;
                Assert.That(b.xMin >= -HalfW && b.xMax <= HalfW && b.yMin >= -HalfH && b.yMax <= HalfH, Is.True, $"'{t.Text}' off screen {b}");
            }
            for (var i = 0; i < texts.Count; i++)
                for (var j = i + 1; j < texts.Count; j++)
                    Assert.That(texts[i].WorldBounds.Overlaps(texts[j].WorldBounds), Is.False, $"'{texts[i].Text}' overlaps '{texts[j].Text}'");
        }

        [Test]
        public void ScreensFitAndNothingOverlaps()
        {
            foreach (var shot in Shots(root.transform))
            {
                shot.show();
                AssertTextFitsAndNoOverlap(shot.screen);
                shot.screen.gameObject.SetActive(false);
            }
        }

        struct Shot
        {
            public string name;
            public Transform screen;
            public System.Action show;
        }

        /// <summary>The BB-01/02 screens in the states the report shows (all in memory: no save file is touched).</summary>
        List<Shot> Shots(Transform parent)
        {
            var list = new List<Shot>();
            var menu = MenuView.Create(parent, art);
            list.Add(new Shot { name = "menu-new.png", screen = menu.transform, show = () => { menu.gameObject.SetActive(true); menu.SetRunInfo(new ProgressData(), null); } });
            list.Add(new Shot { name = "menu-continue.png", screen = menu.transform, show = () =>
                { menu.gameObject.SetActive(true); menu.SetRunInfo(P(5, 0, UnlockRules.Boss1), SavedRun(5, 1)); } });
            list.Add(new Shot { name = "goal-alldone.png", screen = menu.transform, show = () =>
                { menu.gameObject.SetActive(true); menu.SetRunInfo(P(8, 3, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1, UnlockRules.Win3), null); } });

            var title = RunTitleView.Create(parent, art);
            list.Add(new Shot { name = "runtitle.png", screen = title.transform, show = () => title.Show(FlowWithSave(P(5, 0, UnlockRules.Boss1), SavedRun(4, 0))) });

            var summary = RunSummaryView.Create(parent, art);
            list.Add(new Shot { name = "summary-win.png", screen = summary.transform, show = () =>
            {
                var run = WonRun(0);
                run.Achievements.AddRange(new[] { UnlockRules.Boss2, UnlockRules.Win1 });
                summary.Show(run, P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1));
            } });
            list.Add(new Shot { name = "summary-loss.png", screen = summary.transform, show = () => summary.Show(LostRun(), P(6, 0, UnlockRules.Boss1)) });
            list.Add(new Shot { name = "summary-win-retry.png", screen = summary.transform, show = () =>
            {
                var run = WonRun(3);
                run.Achievements.AddRange(new[] { UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1 });
                summary.Show(run, P(8, 1, UnlockRules.Boss1, UnlockRules.Boss2, UnlockRules.Win1));
            } });
            foreach (var s in list) s.screen.gameObject.SetActive(false);
            return list;
        }

        /// <summary>Writes the BB-01/02 screenshots (270x480) to the folder in BB01_CAPTURE; skipped when unset.</summary>
        [Test]
        public void CaptureBb01Screens()
        {
            var dir = System.Environment.GetEnvironmentVariable("BB01_CAPTURE");
            if (string.IsNullOrEmpty(dir)) Assert.Ignore("BB01_CAPTURE not set");
            System.IO.Directory.CreateDirectory(dir);
            var camGo = new GameObject("CaptureCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 240f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                camGo.transform.position = new Vector3(0f, 0f, -10f);
                foreach (var shot in Shots(root.transform))
                {
                    shot.show();
                    Shoot(cam, System.IO.Path.Combine(dir, shot.name));
                    shot.screen.gameObject.SetActive(false);
                }
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(270, 480, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(270, 480, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 270, 480), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }
    }
}