using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BallBattle.EditorTools;
using BallBattle.Sim;
using BallBattle.Sim.Boss;
using BallBattle.Sim.Run;
using BallBattle.Sim.Weapons;
using BallBattle.View;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-VIEW3 (View only): boss name on the loss screen, a saved boss retry wins over -bbForceBoss, the retry card's arena is the
    /// forced one, boss goals name the rolled boss, the one-hazard-cue rule (verify), music lines in the audio log. Optional output:
    /// BB_VIEW3_OUT = folder for screenshots and the real-flow audio logs.
    /// </summary>
    public class BbView3Tests
    {
        GameObject root;
        ArtLibrary art;
        SfxLibrary lib;
        float now;

        [SetUp]
        public void SetUp()
        {
            art = AssetDatabase.LoadAssetAtPath<ArtLibrary>(PlaceholderArtGenerator.LibraryPath);
            lib = AssetDatabase.LoadAssetAtPath<SfxLibrary>(PlaceholderSfxGenerator.LibraryPath);
            Assert.That(art, Is.Not.Null);
            Assert.That(lib, Is.Not.Null);
            root = new GameObject("BbView3Root");
            now = 0f;
            AudioLog.ForceOn = false;
        }

        [TearDown]
        public void TearDown()
        {
            AudioLog.ForceOn = false;
            Object.DestroyImmediate(root);
        }

        static string OutDir()
        {
            var dir = Environment.GetEnvironmentVariable("BB_VIEW3_OUT");
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            return string.IsNullOrEmpty(dir) ? null : dir;
        }

        // ---------------- Run flow helpers (the real RunFlow, a memory save) ----------------

        static RunFlow NewFlow(MemorySaveStore store, string forceBoss = null, string forceArena = null)
        {
            var flow = new RunFlow(store, 0f, 0f) { ForceBossId = forceBoss, ForceArenaId = forceArena };
            flow.Open();
            return flow;
        }

        /// <summary>New run (seed), start card taken, then moved to <paramref name="fightIndex"/> with <paramref name="boss"/> rolled in
        /// its slot (when given). Ends on Prepare.</summary>
        static void StartRun(RunFlow flow, uint seed, int fightIndex = 0, string boss = null)
        {
            flow.NewRun(seed);
            flow.ChooseWeapon(flow.StartChoices[0]);
            flow.PickStart(0);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Prepare));
            flow.Run.FightIndex = fightIndex;
            if (boss != null) flow.Run.BossIds[BossIds.SlotOf(boss)] = boss;
        }

        /// <summary>Plays the fight that is about to start (Fight from Prepare, or a retry's start pick), forcing the result by keeping
        /// the loser's HP tiny, through the real flow to the result screen.</summary>
        static MatchSim PlayFight(RunFlow flow, bool playerWins)
        {
            if (flow.Current == RunFlow.State.Prepare) flow.Fight();
            else if (flow.Current == RunFlow.State.StartPick) flow.PickStart(0);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Countdown));
            var m = flow.CurrentMatch;
            flow.Tick(0.01f);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Playing));
            var tiny = Fix.Ratio(1, 100);
            var loser = m.Balls[playerWins ? 1 : 0];
            for (var i = 0; i < 20000 && m.Outcome == MatchOutcome.Ongoing; i++)
            {
                if (loser.Hp > tiny) loser.Hp = tiny;
                m.Step();
            }
            Assert.That(m.WinnerIndex, Is.EqualTo(playerWins ? 0 : 1), "forced result");
            flow.OnMatchEnded(m);
            flow.Tick(0.01f);
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.FightResult));
            return m;
        }

        // ---------------- TASK 1a: boss loss screen names the boss ----------------

        [TestCase(3, BossIds.ShellKing, "SHELL KING")]
        [TestCase(3, BossIds.Magnet, "MAGNET")]
        [TestCase(7, BossIds.Warden, "WARDEN")]
        public void BossLossScreenNamesTheRealBoss(int fightIndex, string boss, string name)
        {
            var store = new MemorySaveStore();
            var flow = NewFlow(store);
            StartRun(flow, 31, fightIndex, boss);
            PlayFight(flow, playerWins: false);
            var r = flow.LastReport;
            Assert.That(r.IsRetry, Is.True);
            Assert.That(r.NextBossId, Is.EqualTo(boss));
            Assert.That(r.NextHeader, Is.EqualTo($"NEXT: FIGHT {fightIndex + 1}/{RunTuning.Fights}  {name}  RETRY"));
            var view = RunResultView.Create(root.transform, art);
            view.Show(r);
            Assert.That(view.NextHeaderText, Is.EqualTo(r.NextHeader), "the screen shows it");
            Assert.That(view.NextHeaderText, Does.Not.Contain("GIANT BOSS"));
            Assert.That(r.BossId, Is.EqualTo(boss), "the fight just lost");
            Assert.That(view.FightLineText, Does.StartWith($"FIGHT {fightIndex + 1}/{RunTuning.Fights}  {name}  VS "), "top line names the boss too");
        }

        // ---------------- TASK 1b: a saved boss retry wins over -bbForceBoss ----------------

        [TestCase(BossIds.Magnet)]   // same slot as the saved SHELL KING
        [TestCase(BossIds.Warden)]   // the other slot
        public void ContinueAfterBossLossKeepsTheSavedRetryBossOverForceBoss(string force)
        {
            var store = new MemorySaveStore();
            var first = NewFlow(store);
            StartRun(first, 31, 3, BossIds.ShellKing);
            PlayFight(first, playerWins: false);
            Assert.That(first.Run.IsRetry, Is.True);

            var flow = NewFlow(store, forceBoss: force);   // the game is restarted with -bbForceBoss
            flow.Continue();
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.StartPick), "the retry's own pre-fight screen (CHOOSE YOUR RETRY)");
            Assert.That(flow.Run.IsRetry, Is.True);
            Assert.That(flow.Run.CurrentBossId, Is.EqualTo(BossIds.ShellKing), "the saved retry boss");
            Assert.That(flow.Run.ForcedBossId, Is.EqualTo(""), "the force waits for a new fight");
            Assert.That(store.Load().Run.ToRun().CurrentBossId, Is.EqualTo(BossIds.ShellKing), "saved run unchanged");

            var m = PlayFight(flow, playerWins: true);   // the retry itself
            Assert.That(m.Boss, Is.InstanceOf<ShellKingRule>(), "the retry fight is the same boss");
            Assert.That(flow.Run.IsRetry, Is.False);
            Assert.That(flow.Run.ForcedBossId, Is.EqualTo(force), "the force applies once a new fight comes up");
            Assert.That(flow.Run.CurrentBossId, Is.EqualTo(force), "fight 5 is the forced boss (RunState.ApplyForcedBoss rule)");
            Assert.That(flow.LastReport.NextBossId, Is.EqualTo(force));
        }

        [Test]
        public void ContinueAfterBossLossWithoutForceBossRetriesTheSameBossNormally()
        {
            var store = new MemorySaveStore();
            var first = NewFlow(store);
            StartRun(first, 31, 3, BossIds.Magnet);
            PlayFight(first, playerWins: false);

            var flow = NewFlow(store);
            flow.Continue();
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.StartPick));
            Assert.That(flow.Run.IsRetry, Is.True);
            Assert.That(flow.Run.CurrentBossId, Is.EqualTo(BossIds.Magnet));
            var m = PlayFight(flow, playerWins: false);
            Assert.That(m.Boss, Is.InstanceOf<MagnetRule>());
            Assert.That(flow.Run.Over, Is.True, "a second loss ends the run, as before");
            Assert.That(flow.Run.ForcedBossId, Is.EqualTo(""));
            flow.NextAfterResult();
            Assert.That(flow.Current, Is.EqualTo(RunFlow.State.Summary));
        }

        [Test]
        public void ARetryOfAForcedBossKeepsItsBossWhenAnotherForceIsGiven()
        {
            var store = new MemorySaveStore();
            var first = NewFlow(store, forceBoss: BossIds.Magnet);
            first.NewRun(31);
            first.ChooseWeapon(first.StartChoices[0]);
            first.PickStart(0);
            Assert.That(first.Run.CurrentBossId, Is.EqualTo(BossIds.Magnet), "fight 1 meets the forced boss");
            PlayFight(first, playerWins: false);

            var flow = NewFlow(store, forceBoss: BossIds.Warden);
            flow.Continue();
            Assert.That(flow.Run.IsRetry, Is.True);
            Assert.That(flow.Run.CurrentBossId, Is.EqualTo(BossIds.Magnet), "the saved retry boss, not WARDEN");
            Assert.That(PlayFight(flow, playerWins: true).Boss, Is.InstanceOf<MagnetRule>());
            Assert.That(flow.Run.ForcedBossId, Is.EqualTo(BossIds.Warden), "WARDEN from the next new fight on");
        }

        // ---------------- TASK 1c: retry card arena = the arena actually used ----------------

        [Test]
        public void RetryCardShowsTheForcedArena()
        {
            var store = new MemorySaveStore();
            var flow = NewFlow(store, forceArena: ArenaRegistry.Tight);
            uint seed = 31;
            for (; seed < 200; seed++)   // a seed whose own fight-1 arena is not TIGHT, so the label can only come from the force
            {
                var probe = RunState.Start(seed, RunState.StartChoices(seed, UnlockRules.Weapons(new ProgressData()))[0], new ProgressData());
                if (probe.ArenaId != ArenaRegistry.Tight) break;
            }
            StartRun(flow, seed);
            var natural = flow.Run.ArenaId;
            Assert.That(natural, Is.Not.EqualTo(ArenaRegistry.Tight));
            PlayFight(flow, playerWins: false);
            var r = flow.LastReport;
            Assert.That(r.IsRetry, Is.True);
            Assert.That(r.NextArenaId, Is.EqualTo(ArenaRegistry.Tight));
            var view = RunResultView.Create(root.transform, art);
            view.Show(r);
            Assert.That(view.NextStatsText, Does.Contain("ARENA " + ArenaRegistry.Get(ArenaRegistry.Tight).DisplayName));
            Assert.That(view.NextStatsText, Does.Not.Contain("ARENA " + ArenaRegistry.Get(natural).DisplayName));
            Assert.That(flow.Run.ArenaId, Is.EqualTo(natural), "the run's own arena is untouched");
        }

        [Test]
        public void RetryCardShowsTheRunsArenaWithoutForce()
        {
            var store = new MemorySaveStore();
            var flow = NewFlow(store);
            StartRun(flow, 32);
            PlayFight(flow, playerWins: false);
            Assert.That(flow.LastReport.NextArenaId, Is.EqualTo(flow.Run.ArenaId));
        }

        // ---------------- TASK 1d: boss goals name the rolled boss ----------------

        static RunState RunWithBosses(string boss4, string boss8)
        {
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            run.BossIds[0] = boss4;
            run.BossIds[1] = boss8;
            return run;
        }

        static ProgressData Progress(int runsPlayed, string last4 = null, string last8 = null, params string[] achievements)
        {
            var p = new ProgressData { RunsPlayed = runsPlayed };
            if (last4 != null) p.SetLastBoss(0, last4);
            if (last8 != null) p.SetLastBoss(1, last8);
            p.Achievements.AddRange(achievements);
            return p;
        }

        [Test]
        public void GoalTitleShowsMagnetAtFight4AndWardenAtFight8WhenRolled()
        {
            var menu = MenuView.Create(root.transform, art);
            menu.gameObject.SetActive(true);
            menu.ShowHub();
            var run = RunWithBosses(BossIds.Magnet, BossIds.Warden);
            menu.SetRunInfo(Progress(1, BossIds.Magnet, BossIds.Warden), run);
            Assert.That(menu.Goal.Info.GoalId, Is.EqualTo(UnlockRules.Boss1));
            Assert.That(menu.Goal.TitleText.Text, Is.EqualTo("BEAT MAGNET"), "fight-4 goal of the saved run");
            menu.SetRunInfo(Progress(1, BossIds.Magnet, BossIds.Warden, UnlockRules.Boss1), run);
            Assert.That(menu.Goal.Info.GoalId, Is.EqualTo(UnlockRules.Win1));
            Assert.That(menu.Goal.TitleText.Text, Is.EqualTo("BEAT WARDEN"), "fight-8 goal of the saved run");
        }

        [Test]
        public void GoalTitleForTheNextRunUsesTheBossItWillGet()
        {
            var box = NextGoalBox.Create(root.transform, art, 8, 244, 10);
            box.Show(new ProgressData());
            Assert.That(box.TitleText.Text, Is.EqualTo("BEAT SHELL KING"), "first run on a save: SHELL KING");
            box.Show(Progress(1, BossIds.ShellKing, BossIds.Mimic));
            Assert.That(box.TitleText.Text, Is.EqualTo("BEAT MAGNET"), "never the last run's boss: MAGNET");
            box.Show(Progress(1, BossIds.ShellKing, BossIds.Mimic, UnlockRules.Boss1));
            Assert.That(box.TitleText.Text, Is.EqualTo("BEAT WARDEN"));
            box.Show(Progress(2, BossIds.Magnet, BossIds.Warden, UnlockRules.Boss1));
            Assert.That(box.TitleText.Text, Is.EqualTo("BEAT MIMIC"));
            box.Show(Progress(3));   // played before bosses were remembered: the seed decides, so no name
            Assert.That(box.TitleText.Text, Is.EqualTo("BEAT THE BOSS"));
            // The predicted boss is what the next run really rolls.
            foreach (uint seed in new uint[] { 1, 2, 3, 99 })
                Assert.That(BossSelection.Choose(seed, Progress(1, BossIds.ShellKing, BossIds.Mimic)), Is.EqualTo(new[] { BossIds.Magnet, BossIds.Warden }));
        }

        [Test]
        public void ForcedBossNamesTheGoalOfTheRunItIsIn()
        {
            var run = RunWithBosses(BossIds.ShellKing, BossIds.Mimic);
            run.ApplyForcedBoss(BossIds.Magnet);
            Assert.That(NextGoalBox.GoalBoss(UnlockRules.Boss1, new ProgressData(), run), Is.EqualTo(BossIds.Magnet), "what fight 4 really is");
        }

        // ---------------- TASK 2: hazard cue rule (verify only) ----------------

        AudioDirector NewDirector()
        {
            var d = AudioDirector.Create(root.transform, lib, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        static SimEvent Hazard(SimEventType type, int tick, int ball, float damage) =>
            new SimEvent { Type = type, Tick = tick, A = ball, B = type == SimEventType.ObstacleHit ? 0 : -1, Value = TFix.X(damage) };

        static readonly MatchSim AnySim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.SpikeWalls), 5,
            new[] { WeaponRegistry.Create(BladeRule.WeaponId), WeaponRegistry.Create(FangRule.WeaponId) });

        [Test]
        public void VerifySpikeHitReplacesTheWallCueInItsTick()
        {
            var d = NewDirector();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 10, 0, 3f), AnySim);   // spike first
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 10, 0, 0f), AnySim);   // then a plain wall, same ball, same tick
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.SpikeHit }), "spike_hit only: the wall cue is dropped");
            d.Played.Clear();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 11, 0, 0f), AnySim);   // next tick: a wall plays again
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall }));
        }

        /// <summary>
        /// The order the BB-6d rule missed (found by the real-match check below, 6 times in 120 matches before BB-VIEW3): a plain wall
        /// bounce, then a spike, for the same ball in one tick. spike_hit replaces the wall cue: one cue, and the log says so.
        /// </summary>
        [Test]
        public void SpikeAfterAWallInTheSameTickReplacesTheWallCue()
        {
            AudioLog.ForceOn = true;
            var d = NewDirector();
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 20, 0, 0f), AnySim);   // plain wall first
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall }));
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 20, 0, 3f), AnySim);   // then a spike, same ball, same tick
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.SpikeHit }), "spike_hit replaces the wall cue");
            Assert.That(d.LogLines.Last(), Does.EndWith("cue=spike_hit allowed=yes replaced=wall"));
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 20, 1, 0f), AnySim);   // another ball's wall in that tick: the per-tick gate (unchanged)
            d.Played.Clear();
            now += AudioDirector.BounceSpacing;   // BB-SFX4: plain bounces are spaced in time; step past it so the wall plays
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 21, 0, 0f), AnySim);   // next tick: plain wall again
            d.OnSimEvent(Hazard(SimEventType.WallBounce, 22, 0, 3f), AnySim);   // a later tick's spike never touches it
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { AudioIds.Wall, AudioIds.SpikeHit }));
        }

        /// <summary>
        /// Real matches (SPIKES arena all weapon pairs, and WARDEN fights with spiked walls on every arena): every hazard event in
        /// Sim order through the real director with the audio log on; per ball and tick, the cues that played (log allowed=yes)
        /// minus replaced wall cues must be at most one. Also counts the "plain wall, then spike" order for one ball in one tick.
        /// </summary>
        [Test]
        public void VerifyOneHazardCuePerBallPerTickInRealMatches()
        {
            AudioLog.ForceOn = true;
            var d = NewDirector();
            var perBallTick = new Dictionary<(int match, int ball, int tick), List<string>>();
            int matches = 0, hazards = 0, wallThenSpike = 0, replacements = 0;
            void Play(MatchSim m)
            {
                d.OnMatchStarted();
                var chosen = new Dictionary<(int, int), List<string>>();
                for (var i = 0; i < 20000 && m.Outcome == MatchOutcome.Ongoing; i++)
                {
                    m.Step();
                    foreach (var e in m.Events)
                    {
                        var hazard = e.Type == SimEventType.WallBounce || e.Type == SimEventType.ObstacleHit;
                        d.OnSimEvent(e, m);
                        if (!hazard) continue;
                        hazards++;
                        var line = d.LogLines[d.LogLines.Count - 1];
                        Assert.That(line, Does.StartWith("[Audio] " + e.Type + " tick=" + e.Tick + " "), "this event's own log line");
                        var key = (matches, e.A, e.Tick);
                        if (!perBallTick.TryGetValue(key, out var list)) perBallTick[key] = list = new List<string>();
                        if (line.Contains(" replaced=wall")) { list.Remove(AudioIds.Wall); replacements++; }
                        if (line.Contains(" allowed=yes")) list.Add(line.Contains("cue=spike_hit") ? AudioIds.SpikeHit : AudioIds.Wall);
                        var cue = AudioDirector.HazardCue(e.Type, e.Value.ToFloat(), lib.Has);
                        if (!chosen.TryGetValue((e.A, e.Tick), out var c)) chosen[(e.A, e.Tick)] = c = new List<string>();
                        if (cue == AudioIds.SpikeHit && c.Contains(AudioIds.Wall)) wallThenSpike++;
                        c.Add(cue);
                    }
                }
                matches++;
            }
            var ids = WeaponRegistry.All.Select(w => w.Id).ToArray();
            var logging = Debug.unityLogger.logEnabled;
            Debug.unityLogger.logEnabled = false;   // ~27k audio-log lines: keep them in LogLines only, not in the Unity log file
            try
            {
            foreach (var a in ids)
                foreach (var b in ids)
                    Play(new MatchSim(ArenaRegistry.Create(ArenaRegistry.SpikeWalls), (uint)(matches + 11), new[] { WeaponRegistry.Create(a), WeaponRegistry.Create(b) }));
            foreach (var arena in ArenaRegistry.All.Select(x => x.Id))
                foreach (var w in ids)
                {
                    var p = new ProgressData();
                    var run = RunState.Start(41, RunState.StartChoices(41, UnlockRules.Weapons(p))[0], p);
                    run.Pick(0);
                    run.Build.WeaponId = w;
                    run.FightIndex = RunTuning.BossFights[1];
                    run.BossIds[1] = BossIds.Warden;
                    Play(run.CreateMatch(ArenaRegistry.Create(arena)));
                }
            }
            finally { Debug.unityLogger.logEnabled = logging; }
            var worst = perBallTick.Values.Max(l => l.Count);
            var multi = perBallTick.Count(kv => kv.Value.Count > 1);
            var summary = $"HAZARD VERIFY matches={matches} hazardEvents={hazards} ballTicks={perBallTick.Count} ballTicksWithMoreThanOneCue={multi} maxCuesPerBallTick={worst} wallThenSpikeSameBallTick={wallThenSpike} wallCuesReplaced={replacements}";
            TestContext.Out.WriteLine(summary);
            Debug.Log(summary);
            var dir = OutDir();
            if (dir != null) File.WriteAllText(Path.Combine(dir, "hazard-verify.txt"), summary + "\n");
            Assert.That(hazards, Is.GreaterThan(1000), "enough real hazard events");
            Assert.That(worst, Is.LessThanOrEqualTo(1), summary);
            Assert.That(replacements, Is.LessThanOrEqualTo(wallThenSpike), "replacements happen only in wall-then-spike ticks (a wall gated by another ball needs none)");
        }

        // ---------------- TASK 2: music lines in the audio log ----------------

        [Test]
        public void AudioLogHasMusicStartStopAndTrackLines()
        {
            AudioLog.ForceOn = true;
            var d = NewDirector();
            d.SetScreen(MusicScreen.Menu);
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] track track=music_menu screen=Menu result=start t=0\.00$"));
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] start track=music_menu slot=0 t=0\.00$"));
            now = 1f;
            d.SetScreen(MusicScreen.Fight);
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] start track=music_battle slot=1 t=1\.00$"));
            for (var i = 0; i < 10; i++) { now += 0.1f; d.Tick(0.1f); }
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] stop track=music_menu slot=0 t=\d+\.\d\d$"), "the crossfaded-out track stops");
            d.RequestUiCue(AudioIds.FightWin);
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] fadeout track=music_battle cue=fight_win t=\d+\.\d\d$"));
            for (var i = 0; i < 10; i++) { now += 0.1f; d.Tick(0.1f); }
            Assert.That(d.LogLines, Has.Some.Match(@"^\[Music\] stop track=music_battle slot=1 t=\d+\.\d\d$"));
            var before = d.LogLines.Count;
            d.SetScreen(MusicScreen.Fight);   // same screen: nothing
            Assert.That(d.LogLines.Count, Is.EqualTo(before));
        }

        [Test]
        public void MusicLinesAreOffWithoutTheSwitch()
        {
            var d = NewDirector();
            d.SetScreen(MusicScreen.Menu);
            d.SetScreen(MusicScreen.Fight);
            Assert.That(d.LogLines, Is.Empty);
        }

        // ---------------- real-flow logs (-bbForceDraw and one normal match), written with BB_VIEW3_OUT ----------------

        List<string> VersusMatchLog(bool forceDraw, string file)
        {
            AudioLog.ForceOn = true;
            var d = NewDirector();
            var flow = new MatchFlow(3f, 2f) { ForceDraw = forceDraw };
            var overlay = OverlayView.Create(root.transform, art);
            var sim = new MatchSim(ArenaRegistry.Create(ArenaRegistry.SpikeWalls), 77u, new[] { WeaponRegistry.Create(BladeRule.WeaponId), WeaponRegistry.Create(FangRule.WeaponId) });
            void Frame(float dt)
            {
                now += dt;
                flow.Tick(dt);
                d.SetScreen(GameController.VersusScreen(flow.Current, false, false));
                overlay.Render(flow, dt);
                d.Tick(dt);
            }
            d.SetScreen(MusicScreen.VersusPick);
            flow.Begin(BladeRule.WeaponId, FangRule.WeaponId, 77u);
            for (var i = 0; i < 40; i++) Frame(0.1f);
            Assert.That(flow.Current, Is.EqualTo(MatchFlow.State.Playing));
            d.OnMatchStarted();
            for (var i = 0; i < 20000 && sim.Outcome == MatchOutcome.Ongoing; i++)
            {
                sim.Step();
                foreach (var e in sim.Events) d.OnSimEvent(e, sim);
            }
            d.LogLines.Add($"--- match end: real winner {sim.WinnerIndex}, reported {GameController.AudioWinner(sim.WinnerIndex, runOpen: false, flow)} ---");
            flow.OnRoundEnded(sim.WinnerIndex);
            d.OnMatchEnded(GameController.AudioWinner(sim.WinnerIndex, runOpen: false, flow), -1);
            for (var i = 0; i < 60; i++)
            {
                Frame(0.1f);
                if (!forceDraw && flow.Current == MatchFlow.State.Playing) break;   // a normal Versus round: next round begins
            }
            var cues = d.Played.Select(p => p.Id).ToList();
            var lines = d.LogLines.ToList();
            lines.Add("--- cues played after the match end (Played): " + string.Join(" ", cues.Where(c => c == AudioIds.FightWin || c == AudioIds.FightLose || c == AudioIds.RunWin || c == AudioIds.RunOver)));
            var dir = OutDir();
            if (dir != null) File.WriteAllLines(Path.Combine(dir, file), lines);
            return lines;
        }

        [Test]
        public void RealLogForceDrawShowsOnlyMusicResultAtMatchEnd()
        {
            var lines = VersusMatchLog(true, "audio-log-forcedraw.txt");
            var end = lines.FindIndex(l => l.StartsWith("--- match end"));
            var after = lines.Skip(end + 1).ToList();
            var starts = after.Where(l => l.StartsWith("[Music] start")).ToList();
            Assert.That(starts, Has.Count.EqualTo(1), string.Join("\n", after));
            Assert.That(starts[0], Does.Contain("track=music_result"));
            Assert.That(after.Last(), Is.EqualTo("--- cues played after the match end (Played): "), "no win/lose stinger");
            Assert.That(after.Any(l => l.Contains("fadeout")), Is.False, "no stinger cue faded the music");
        }

        [Test]
        public void RealLogOneMatchShowsTheMusicLines()
        {
            var lines = VersusMatchLog(false, "audio-log-match.txt");
            Assert.That(lines, Has.Some.StartsWith("[Music] start track=music_menu"));
            Assert.That(lines, Has.Some.StartsWith("[Music] start track=music_battle"));
            Assert.That(lines, Has.Some.StartsWith("[Music] stop track=music_menu"));
            Assert.That(lines, Has.Some.StartsWith("[Audio] WallBounce"), "the hazard lines are still there");
        }

        // ---------------- screenshots (BB_VIEW3_OUT) ----------------

        static void Shoot(Camera cam, string path)
        {
            var rt = new RenderTexture(270, 480, 24) { filterMode = FilterMode.Point };
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(270, 480, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 270, 480), 0, 0);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(tex);
            rt.Release();
        }

        [Test]
        public void CaptureView3Screens()
        {
            var dir = OutDir();
            if (dir == null) Assert.Ignore("BB_VIEW3_OUT not set");
            var camGo = new GameObject("CaptureCam");
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = 240f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Palette.Background;
                camGo.transform.position = new Vector3(0f, 0f, -10f);

                // 1. Boss loss: fight 4, SHELL KING, retry.
                var store = new MemorySaveStore();
                var flow = NewFlow(store);
                StartRun(flow, 31, 3, BossIds.ShellKing);
                PlayFight(flow, playerWins: false);
                var result = RunResultView.Create(root.transform, art);
                result.Show(flow.LastReport);
                Shoot(cam, Path.Combine(dir, "1-boss-loss-shellking.png"));
                File.WriteAllText(Path.Combine(dir, "1-boss-loss-shellking.txt"), flow.LastReport.NextHeader + "\n" + flow.LastReport.NextStats + "\n");

                // 2. Retry card with -bbForceArena tight (a normal fight lost).
                var store2 = new MemorySaveStore();
                var flow2 = NewFlow(store2, forceArena: ArenaRegistry.Tight);
                StartRun(flow2, 33);
                var natural = flow2.Run.ArenaId;
                PlayFight(flow2, playerWins: false);
                result.Show(flow2.LastReport);
                Shoot(cam, Path.Combine(dir, "2-retry-card-forced-arena-tight.png"));
                File.WriteAllText(Path.Combine(dir, "2-retry-card-forced-arena-tight.txt"),
                    $"run's own arena: {natural}\nforced: {ArenaRegistry.Tight}\n{flow2.LastReport.NextHeader}\n{flow2.LastReport.NextStats}\n");
                Object.DestroyImmediate(result.gameObject);

                // 3-4. Menu NEXT GOAL with a saved run that rolled MAGNET (fight 4) and WARDEN (fight 8).
                var menu = MenuView.Create(root.transform, art);
                menu.gameObject.SetActive(true);
                menu.ShowHub();
                var run = RunWithBosses(BossIds.Magnet, BossIds.Warden);
                menu.SetRunInfo(Progress(1, BossIds.Magnet, BossIds.Warden), run);
                Shoot(cam, Path.Combine(dir, "3-goal-fight4-magnet.png"));
                var t3 = menu.Goal.TitleText.Text;
                menu.SetRunInfo(Progress(1, BossIds.Magnet, BossIds.Warden, UnlockRules.Boss1), run);
                Shoot(cam, Path.Combine(dir, "4-goal-fight8-warden.png"));
                File.WriteAllText(Path.Combine(dir, "3-4-goal-titles.txt"), t3 + "\n" + menu.Goal.TitleText.Text + "\n");
            }
            finally
            {
                Object.DestroyImmediate(camGo);
            }
        }
    }
}
