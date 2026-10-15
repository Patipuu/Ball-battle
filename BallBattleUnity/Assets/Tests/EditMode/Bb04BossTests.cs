using System.Collections.Generic;
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

namespace BallBattle.Tests
{
    /// <summary>
    /// BB-04 boss pool v1, View side: boss audio cues (REAL SfxLibrary.asset), Prepare name + hint,
    /// summary line, telegraphs, save v3 + migration, -bbForceBoss.
    /// </summary>
    public class Bb04BossTests
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
            root = new GameObject("Bb04Root");
            now = 0f;
            AudioLog.ForceOn = false;
        }

        [TearDown]
        public void TearDown()
        {
            AudioLog.ForceOn = false;
            Object.DestroyImmediate(root);
        }

        AudioDirector Director(SfxLibrary library)
        {
            var d = AudioDirector.Create(root.transform, library, new MemorySaveStore());
            d.Clock = () => now;
            return d;
        }

        static SimEvent BossEv(BossEvent kind, int tick) =>
            new SimEvent { Type = SimEventType.Boss, Tick = tick, A = 1, B = 0, Variant = (byte)kind };

        static MatchSim Sim() =>
            new MatchSim(ArenaRegistry.Create(ArenaRegistry.Classic), 5, new[] { WeaponRegistry.Create(BladeRule.WeaponId), WeaponRegistry.Create(FangRule.WeaponId) });

        // ---------------- audio ----------------

        [Test]
        public void EveryBossEventMapsToItsCueId()
        {
            var expected = new Dictionary<BossEvent, string>
            {
                { BossEvent.ShellWarn, "boss_shellking_warn" }, { BossEvent.ShellOn, "boss_shellking_hit" }, { BossEvent.ShellOff, null },
                { BossEvent.MagnetWarn, "boss_magnet_warn" }, { BossEvent.MagnetPull, "boss_magnet_hit" }, { BossEvent.MagnetEnd, null },
                { BossEvent.MimicCopied, "boss_mimic_hit" },
                { BossEvent.WardenWarn, "boss_warden_warn" }, { BossEvent.WardenStep, "boss_warden_hit" },
            };
            foreach (BossEvent k in System.Enum.GetValues(typeof(BossEvent)))
                Assert.That(AudioIds.BossCue(k), Is.EqualTo(expected[k]), k.ToString());
            Assert.That(AudioIds.Cues, Is.SupersetOf(AudioIds.BossCues), "linked when the files exist");
        }

        [Test]
        public void RealLibraryHasAllFifteenBossAndSkillCuesAndBossEventsPlayTheirClips()
        {
            AudioLog.ForceOn = true;
            var d = Director(lib);
            var cues = AudioIds.BossCues.Concat(AudioIds.SkillCues).ToArray();
            Assert.That(cues.Length, Is.EqualTo(15));
            var missing = cues.Where(id => lib.Get(id) == null).ToArray();
            Assert.That(missing, Is.Empty, "Missing cue ids: " + string.Join(", ", missing));
            var tick = 10;
            foreach (BossEvent k in System.Enum.GetValues(typeof(BossEvent)))
            {
                var result = AudioDirector.BossCueResult.None;
                Assert.DoesNotThrow(() => result = d.RequestBossCue(BossEv(k, tick++), Sim()));
                Assert.That(result, Is.EqualTo(AudioIds.BossCue(k) == null ? AudioDirector.BossCueResult.None : AudioDirector.BossCueResult.Played));
            }
            Assert.That(d.Played.Select(p => p.Id), Is.EquivalentTo(AudioIds.BossCues));
            Assert.That(d.LogLines.Count(l => l.Contains("result=played")), Is.EqualTo(7));
            Assert.That(d.LogLines.Any(l => l.Contains("ShellOff") || l.Contains("MagnetEnd")), Is.False, "off/end have no cue");
        }

        [Test]
        public void BossCuePlaysOnTheSfxBusAtPointEightPitchOneAndIsGatedPerTick()
        {
            var test = ScriptableObject.CreateInstance<SfxLibrary>();
            test.Clips = AudioIds.BossCues.Select(id => AudioClip.Create(id, 64, 1, 22050, false)).ToArray();
            try
            {
                AudioLog.ForceOn = true;
                var d = Director(test);
                Assert.That(d.RequestBossCue(BossEv(BossEvent.ShellOn, 40), Sim()), Is.EqualTo(AudioDirector.BossCueResult.Played));
                Assert.That(d.RequestBossCue(BossEv(BossEvent.ShellOn, 40), Sim()), Is.EqualTo(AudioDirector.BossCueResult.Gated), "same cue, same tick");
                Assert.That(d.RequestBossCue(BossEv(BossEvent.ShellOff, 41), Sim()), Is.EqualTo(AudioDirector.BossCueResult.None));
                Assert.That(d.RequestBossCue(BossEv(BossEvent.MagnetEnd, 42), Sim()), Is.EqualTo(AudioDirector.BossCueResult.None));
                d.OnSimEvent(BossEv(BossEvent.WardenStep, 43), Sim());
                Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { "boss_shellking_hit", "boss_warden_hit" }));
                foreach (var p in d.Played)
                {
                    Assert.That(p.Pitch, Is.EqualTo(1f), "no jitter");
                    Assert.That(p.Volume, Is.EqualTo(0.8f * VolumeCurve.Linear(d.SfxVolume)).Within(1e-6f));
                }
                Assert.That(d.LogLines.Count(l => l.StartsWith("[Audio] Boss")), Is.EqualTo(3));
                Assert.That(d.LogLines.Any(l => l.Contains("result=gated")), Is.True);
            }
            finally
            {
                foreach (var c in test.Clips) Object.DestroyImmediate(c);
                Object.DestroyImmediate(test);
            }
        }

        [Test]
        public void ShellBlockedHitsKeepTheNormalHitCue()
        {
            var d = Director(lib);
            var sim = Sim();
            d.OnSimEvent(new SimEvent { Type = SimEventType.Hit, Tick = 5, A = 0, B = 1, Variant = 1 }, sim);
            d.OnSimEvent(new SimEvent { Type = SimEventType.ProjectileHit, Tick = 6, A = 0, B = 1, Variant = 1 }, sim);
            Assert.That(d.Played.Select(p => p.Id), Is.EqualTo(new[] { d.HitCueFor(BladeRule.WeaponId), d.HitCueFor(BladeRule.WeaponId) }));
        }

        // ---------------- Prepare / summary ----------------

        [TestCase(BossIds.ShellKing, BossEvent.ShellWarn, BossEvent.ShellOn)]
        [TestCase(BossIds.Magnet, BossEvent.MagnetWarn, BossEvent.MagnetPull)]
        [TestCase(BossIds.Warden, BossEvent.WardenWarn, BossEvent.WardenStep)]
        public void BossWarnCueIsRequestedAtItsEventTickBeforeTheAnnouncedAction(string bossId, BossEvent warn, BossEvent action)
        {
            var cfg = new MatchConfig { Gravity = Fix.Zero, MinHorizontalSpeed = Fix.Zero, ShrinkStartTick = int.MaxValue };
            var playerWeapon = WeaponRegistry.Create(BladeRule.WeaponId);
            var bossWeapon = WeaponRegistry.Create(BladeRule.WeaponId);
            playerWeapon.SpinDegPerTick = bossWeapon.SpinDegPerTick = Fix.Zero;
            var m = new MatchSim(cfg, 3, new[] {
                new BallLoadout(playerWeapon) { MaxHp = 9999 },
                new BallLoadout(bossWeapon) { MaxHp = 9999, Radius = RunTuning.BossRadius, Boss = BossRegistry.Create(bossId) }
            });
            var director = Director(lib);
            AudioLog.ForceOn = true;
            int warnTick = -1, warnActive = -1, actionTick = -1, actionActive = -1;
            var expectedDelay = bossId == BossIds.ShellKing ? BossTuning.ShellWarnTicks : bossId == BossIds.Magnet ? BossTuning.MagnetWarnTicks : BossTuning.WardenWarnTicks;
            while (actionTick < 0 && m.Tick < 1000)
            {
                m.Balls[0].Pos = TFix.V(-60, 0); m.Balls[0].Vel = FixVec2.Zero;
                m.Balls[1].Pos = TFix.V(60, 0); m.Balls[1].Vel = FixVec2.Zero;
                m.Step();
                now = m.Tick / 60f;
                foreach (var e in m.Events.Where(e => e.Type == SimEventType.Boss))
                {
                    if (e.Variant == (byte)warn)
                    {
                        warnTick = e.Tick; warnActive = m.ActiveTick;
                        director.OnSimEvent(e, m);
                        var cue = AudioIds.BossCue(warn);
                        Assert.That(director.LogLines.Last(), Does.Contain("tick=" + e.Tick + " active=" + m.ActiveTick));
                        if (lib.Has(cue))
                        {
                            Assert.That(director.Played.Last().Id, Is.EqualTo(cue));
                            Assert.That(director.Played.Last().Time, Is.EqualTo(now), "available cue plays in the event's view tick");
                        }
                        else
                        {
                            Assert.That(director.LogLines.Last(), Does.Contain("result=missing"));
                            Assert.That(director.Played, Is.Empty, "missing asset emits no fallback sound (existing contract)");
                        }
                    }
                    if (e.Variant == (byte)action) { actionTick = e.Tick; actionActive = m.ActiveTick; }
                }
            }
            Assert.That(warnTick, Is.GreaterThan(0));
            Assert.That(actionActive - warnActive, Is.EqualTo(expectedDelay));
            Assert.That(actionTick - warnTick, Is.EqualTo(expectedDelay), "staged fight has no hit-stop");
            var report = System.Environment.GetEnvironmentVariable("BB6F_WARN_REPORT");
            if (!string.IsNullOrEmpty(report))
            {
                if (!System.IO.File.Exists(report)) System.IO.File.WriteAllText(report, "cue,requestTick,requestActiveTick,actionTick,actionActiveTick,delayActiveTicks,clipPresent\n");
                System.IO.File.AppendAllText(report, $"{AudioIds.BossCue(warn)},{warnTick},{warnActive},{actionTick},{actionActive},{expectedDelay},{lib.Has(AudioIds.BossCue(warn))}\n");
            }
        }

        static RunState RunAt(int fightIndex, string boss)
        {
            var p = new ProgressData();
            var run = RunState.Start(21, RunState.StartChoices(21, UnlockRules.Weapons(p))[0], p);
            run.Pick(0);
            run.FightIndex = fightIndex;
            run.BossIds[BossIds.SlotOf(boss)] = boss;
            return run;
        }

        [TestCase(BossIds.ShellKing, "SHELL KING", "SHELL BLOCKS ONE SIDE. HIT ITS BACK.")]
        [TestCase(BossIds.Magnet, "MAGNET", "PULLS YOU IN EVERY 5S.")]
        [TestCase(BossIds.Mimic, "MIMIC", "COPIES YOUR WEAPON AND TRAITS.")]
        [TestCase(BossIds.Warden, "WARDEN", "MOVING WALLS SPIKE YOU.")]   // BB-6f: one line, traits retained
        public void PrepareShowsTheBossNameAndItsHint(string boss, string name, string hint)
        {
            var run = RunAt(RunTuning.BossFights[BossIds.SlotOf(boss)], boss);
            var v = PrepareView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.EnemyLabelText, Is.EqualTo(name));
            Assert.That(v.EnemyLinesText, Does.EndWith(hint), "the hint, wrapped onto the two lines when needed");
            if (boss == BossIds.Warden)
            {
                Assert.That(PixelFont.MeasureWidth(hint), Is.LessThanOrEqualTo(195), "WARDEN hint fits one line");
                var lines = PrepareView.BossLines(run.Enemy, boss, 195);
                Assert.That(lines.Count, Is.EqualTo(2), "keep WARDEN's traits line");
                Assert.That(lines[0], Is.Not.Empty);
                Assert.That(lines[1], Is.EqualTo(hint));
            }
            if (boss == BossIds.Mimic) Assert.That(v.EnemyNameText, Is.EqualTo("COPIES YOU"));
            Bb0102Tests.AssertTextFitsAndNoOverlap(v.transform);
        }

        [Test]
        public void SummaryShowsWhichBossesWereMetAndBeaten()
        {
            var run = RunAt(0, BossIds.Magnet);
            run.History.Add(new FightRecord { FightIndex = 3, Won = true, BossId = BossIds.Magnet });
            run.History.Add(new FightRecord { FightIndex = 7, Won = false, BossId = BossIds.Warden });
            run.History.Add(new FightRecord { FightIndex = 7, Won = false, Retry = true, BossId = BossIds.Warden });
            run.Over = true;
            Assert.That(BossText.SummaryLine(run), Is.EqualTo("MAGNET BEATEN  WARDEN MET"));
            var v = RunSummaryView.Create(root.transform, art);
            v.Show(run);
            Assert.That(v.BossNamesText.Text, Is.EqualTo("MAGNET BEATEN  WARDEN MET"));
            Bb0102Tests.AssertTextFitsAndNoOverlap(v.transform);
        }

        // ---------------- telegraphs ----------------

        [Test]
        public void TelegraphsAreDrawnFromTheSimsBossState()
        {
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            var run = RunAt(RunTuning.BossFights[0], BossIds.ShellKing);
            var sim = run.CreateMatch();
            view.StartMatch(sim, "YOU", "BOSS");
            Assert.That(view.Telegraph.Shown, Is.EqualTo(""));
            var seen = new HashSet<string>();
            for (var i = 0; i < 600 && sim.Outcome == MatchOutcome.Ongoing && !seen.Contains("shell-on"); i++)
            {
                view.Advance(1f / 60f);
                seen.Add(view.Telegraph.Shown);
                if (view.Telegraph.Shown == "shell-on") Assert.That(view.Telegraph.DotsShown, Is.GreaterThan(10));
            }
            Assert.That(seen, Does.Contain("shell-warn"));
            Assert.That(seen, Does.Contain("shell-on"));

            var versus = new GameObject("Versus").AddComponent<ArenaView>();
            versus.transform.SetParent(root.transform);
            versus.Art = art;
            versus.AutoRestart = false;
            versus.StartMatch(BladeRule.WeaponId, FangRule.WeaponId, 3);
            Assert.That(versus.Sim.Bosses.Count, Is.Zero, "Versus never has a boss");
            versus.Advance(1f / 60f);
            Assert.That(versus.Telegraph.DotsShown, Is.Zero);
        }

        [Test]
        public void WardenStepHidesObstaclesOutsideTheWalls()
        {
            var cfg = ArenaRegistry.Create(ArenaRegistry.Bumpers);
            var view = root.AddComponent<ArenaView>();
            view.Art = art;
            view.AutoRestart = false;
            var boss = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { Radius = RunTuning.BossRadius, MaxHp = 9999, Boss = new WardenRule() };
            var player = new BallLoadout(WeaponRegistry.Create(BladeRule.WeaponId)) { MaxHp = 9999 };
            var sim = new MatchSim(cfg, 7, new[] { player, boss });
            view.StartMatch(sim);
            view.Advance(0f);
            var before = view.Frame.ObstaclesShown;
            Assert.That(before, Is.EqualTo(cfg.Layout.ObstacleCount));
            while (sim.ActiveTick < 2000 && sim.Outcome == MatchOutcome.Ongoing) view.Advance(1f / 60f);
            var off = Enumerable.Range(0, cfg.Layout.ObstacleCount).Count(k => !sim.ObstacleActive(k));
            Assert.That(view.Frame.ObstaclesShown, Is.EqualTo(before - off), "the view hides exactly the disabled ones");
        }

        // ---------------- save v3 + migration ----------------

        const string V2WithRun =
            "{\"Version\":2,\"Progress\":{\"Achievements\":[\"boss1\"],\"RunsPlayed\":3,\"RunsWon\":0,\"BestFight\":4},\"HasRun\":true," +
            "\"Run\":{\"SeedBits\":5,\"FightIndex\":5,\"Coins\":7,\"Build\":{\"WeaponId\":\"blade\",\"MaxHp\":100.0,\"Hp\":50.0}}}";

        [Test]
        public void OldV2SaveKeepsProgressAndDropsTheRun()
        {
            Assert.That(SaveData.CurrentVersion, Is.EqualTo(4));
            var d = new MemorySaveStore { Json = V2WithRun }.Load();
            Assert.That(d.Version, Is.EqualTo(4));
            Assert.That(d.HasRun, Is.False, "in-progress run dropped (existing rule)");
            Assert.That(d.oldRunNotice, Is.True);
            Assert.That(d.Progress.Achievements, Is.EqualTo(new[] { UnlockRules.Boss1 }));
            Assert.That((d.Progress.RunsPlayed, d.Progress.BestFight), Is.EqualTo((3, 6)), "compensation: reached fight 6");
            Assert.That(d.Progress.LastBoss, Is.Empty);
            Assert.That(d.Progress.BossCodex, Is.Empty);
            Assert.That(BossSelection.IsFirstRun(d.Progress), Is.False, "a returning player: bosses come from the seed");
        }

        [Test]
        public void BossFieldsSurviveSaveAndLoad()
        {
            var p = new ProgressData { RunsPlayed = 2 };
            p.SetLastBoss(0, BossIds.Magnet);
            p.SetLastBoss(1, BossIds.Mimic);
            p.RecordBoss(BossIds.Magnet, true);
            p.RecordBoss(BossIds.Mimic, false);
            var run = RunState.Start(9, RunState.StartChoices(9, UnlockRules.Weapons(p))[0], p);
            run.ApplyForcedBoss(BossIds.Warden);
            run.History.Add(new FightRecord { FightIndex = 3, Won = true, BossId = BossIds.ShellKing });
            var store = new MemorySaveStore();
            store.Save(new SaveData { Progress = p, HasRun = true, Run = RunSave.From(run) });
            StringAssert.Contains("\"Version\":4", store.Json);
            var back = store.Load();
            Assert.That(back.Progress.LastBoss, Is.EqualTo(new[] { BossIds.Magnet, BossIds.Mimic }));
            Assert.That(back.Progress.BossDefeated(BossIds.Magnet), Is.True);
            Assert.That(back.Progress.BossMet(BossIds.Mimic) && !back.Progress.BossDefeated(BossIds.Mimic), Is.True);
            var r = back.Run.ToRun();
            Assert.That(r.BossIds, Is.EqualTo(run.BossIds));
            Assert.That((r.ForcedBossId, r.ForcedBossFight), Is.EqualTo((BossIds.Warden, 0)));
            Assert.That(r.History[0].BossId, Is.EqualTo(BossIds.ShellKing));
            Assert.That(RunValidation.IsValid(r), Is.True);
            r.BossIds[0] = BossIds.Mimic;
            Assert.That(RunValidation.IsValid(r), Is.False, "a boss in the wrong slot is not resumed");
        }

        // ---------------- -bbForceBoss ----------------

        [Test]
        public void ForceBossParsing()
        {
            Assert.That(DebugOptions.ParseForceBoss(new[] { "x", "-bbForceBoss", "warden" }), Is.EqualTo(BossIds.Warden));
            Assert.That(DebugOptions.ParseForceBoss(new[] { "-BBFORCEBOSS", "Shell-King" }), Is.EqualTo(BossIds.ShellKing));
            Assert.That(DebugOptions.ParseForceBoss(new[] { "-bbForceBoss", "shell_king" }), Is.EqualTo(BossIds.ShellKing));
            Assert.That(DebugOptions.ParseForceBoss(new[] { "-bbForceBoss", "MAGNET" }), Is.EqualTo(BossIds.Magnet));
            LogAssertIgnore(() => Assert.That(DebugOptions.ParseForceBoss(new[] { "-bbForceBoss", "dragon" }), Is.Null));
            LogAssertIgnore(() => Assert.That(DebugOptions.ParseForceBoss(new[] { "-bbForceBoss" }), Is.Null));
            Assert.That(DebugOptions.ParseForceBoss(new[] { "-bbSeed", "5" }), Is.Null, "off by default");
            Assert.That(DebugOptions.ParseForceBoss(null), Is.Null);
        }

        static void LogAssertIgnore(System.Action a)
        {
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;
            try { a(); }
            finally { UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false; }
        }

        [Test]
        public void ForcedBossIsRunOnlyAndNeverEntersTheHistory()
        {
            var store = new MemorySaveStore();
            var flow = new RunFlow(store) { ForceBossId = BossIds.Magnet };
            flow.Open();
            flow.NewRun(77);
            flow.ChooseWeapon(flow.StartChoices[0]);
            var run = flow.Run;
            Assert.That(run.CurrentBossId, Is.EqualTo(BossIds.Magnet), "fight 1 meets the forced boss");
            Assert.That(run.BossFor(RunTuning.BossFights[0]), Is.EqualTo(BossIds.Magnet), "and its own slot");
            Assert.That(run.BossFor(RunTuning.BossFights[1]), Is.EqualTo(BossIds.Mimic));
            Assert.That(flow.Progress.LastBoss, Is.EqualTo(new[] { BossIds.ShellKing, BossIds.Mimic }), "first run's natural bosses, not MAGNET");
            Assert.That(store.Load().Progress.LastBoss, Is.EqualTo(new[] { BossIds.ShellKing, BossIds.Mimic }), "saved history too");
            Assert.That(run.CreateMatch().Boss, Is.InstanceOf<MagnetRule>());
        }
    }
}
