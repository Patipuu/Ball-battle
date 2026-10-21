using System;
using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Run mode: owns RunFlow and its screens, drives the shared ArenaView for fights. Opened from the main
    /// menu by GameController; raises Closed when the player goes back.
    /// </summary>
    public sealed class RunController : MonoBehaviour
    {
        ArenaView arena;
        RunTitleView title;
        PickWeaponView pick;
        StartPickView startPick;
        PrepareView prepare;
        RunOverlayView overlay;
        RunSummaryView summary;
        RunResultView result;
        TutorialCoach coach;
        HintPointer hints;
        TapView tap;
        ChestView chest;
        RunFlow.State shown = RunFlow.State.Closed;
        bool dirty;
        float tutorialSlowElapsed;
        public const float TutorialSlowLimitSeconds = 2.5f;

        public RunFlow Flow { get; private set; }
        public bool IsOpen => Flow.Current != RunFlow.State.Closed;
        public PrepareView Prepare => prepare;
        public RunResultView Result => result;
        public RunSummaryView Summary => summary;
        public RunTitleView Title => title;
        public TutorialCoach Coach => coach;
        public HintPointer Hints => hints;
        public TapView Tap => tap;
        public RunOverlayView Overlay => overlay;
        public ChestView Chest => chest;
        public PickWeaponView Pick => pick;
        public StartPickView StartPick => startPick;

        public event Action Closed;

        public static RunController Create(Transform parent, ArenaView arena, ISaveStore store)
        {
            var go = new GameObject("Run");
            go.transform.SetParent(parent, false);
            var c = go.AddComponent<RunController>();
            c.Init(arena, store);
            return c;
        }

        void Init(ArenaView arenaView, ISaveStore store)
        {
            arena = arenaView;
            var art = arena.Art;
            Flow = new RunFlow(store);
            Flow.ForceTraitId = DebugOptions.ForceTrait;   // null unless launched with -bbForceTrait
            Flow.ForceBossId = DebugOptions.ForceBoss;     // null unless launched with -bbForceBoss (Run only; Versus never reads it)
            Flow.ForceArenaId = DebugOptions.ForceArena;   // null unless launched with -bbForceArena (Run only; Versus never reads it)
            Flow.ForceSkill = DebugOptions.ForceSkill;
            title = RunTitleView.Create(transform, art);
            pick = PickWeaponView.Create(transform, art);
            startPick = StartPickView.Create(transform, art);
            prepare = PrepareView.Create(transform, art);
            overlay = RunOverlayView.Create(transform, art);
            summary = RunSummaryView.Create(transform, art);
            result = RunResultView.Create(transform, art);
            hints = HintPointer.Create(transform, art);
            tap = TapView.Create(transform, arena);   // TAP-4: tap to strike during Run fights
            chest = ChestView.Create(transform, art);   // BB-12b: the run's chest, over the summary / start pick
            chest.Collected += () => { Flow.MarkChestShown(); dirty = true; };

            title.ContinueRequested += () => Flow.Continue();
            title.NewRunRequested += () => Flow.NewRun(NewSeed(), Flow.Progress.RunsPlayed == 0, title.SelectedChapter, title.SelectedHeat);
            title.BackRequested += () => Flow.Close();
            pick.WeaponPicked += id => { Flow.MarkHintSeen("pick"); Flow.ChooseWeapon(id); };
            startPick.CardPicked += i => { Flow.MarkHintSeen("startpick"); Flow.PickStart(i); };
            prepare.CardPicked += i => { if (Flow.Run?.FightIndex == 1) Flow.MarkHintSeen("shop"); Flow.Pick(i); };
            startPick.SkillPicked += (i, branch) => { Flow.MarkHintSeen("startpick"); Flow.PickStart(i, branch); };
            prepare.SkillPicked += (i, branch) => { if (Flow.Run?.FightIndex == 1) Flow.MarkHintSeen("shop"); Flow.Pick(i, branch); };
            prepare.RerollRequested += () => { if (Flow.Run?.FightIndex == 1 && Flow.Run.CanReroll) Flow.MarkHintSeen("shop"); Flow.Reroll(); };
            prepare.FightRequested += () => { if (Flow.Run?.FightIndex == 3) Flow.MarkHintSeen("boss"); Flow.Fight(); };
            prepare.MenuRequested += () => Flow.Close();
            overlay.SpeedToggled += () =>
            {
                ChosenSpeed = ChosenSpeed > 1f ? 1f : 2f;
                if (arena.TimeScale >= 1f) arena.TimeScale = ChosenSpeed;   // the tutorial's slow window keeps its 0.35
                overlay.SetSpeed(ChosenSpeed);
            };
            overlay.PauseRequested += () =>
            {
                arena.Paused = true;
                overlay.ShowPause();
            };
            overlay.ResumeRequested += () =>
            {
                overlay.ClosePause();
                arena.Paused = Flow.Current != RunFlow.State.Playing;
            };
            overlay.SaveExitRequested += () => Flow.Close();
            result.ContinueRequested += () => { Flow.MarkHintSeen("carry"); Flow.NextAfterResult(); };
            // Again from the summary: the same chapter and heat after a loss; after a win the chapter it opened (heat 1),
            // or, in the last chapter, the next heat.
            summary.NewRunRequested += () =>
            {
                var r = Flow.Run;
                var ch = 0; var heat = 0;
                if (r != null)
                {
                    if (r.Won && r.Chapter + 1 < BallBattle.Sim.Run.Chapters.Count) ch = r.Chapter + 1;
                    else { ch = r.Chapter; heat = r.Won ? r.Heat + 1 : r.Heat; }
                }
                Flow.NewRun(NewSeed(), false, ch, heat);
            };
            summary.MenuRequested += () => { Flow.MarkHintSeen("next"); Flow.Close(); };

            Flow.Changed += () => dirty = true;
            Flow.MatchPrepared += sim =>
            {
                tutorialSlowElapsed = 0f;
                Debug.Log(FightLog("prepared", sim));
                arena.MarkedBall = 0;   // ball 0 is always the player in Run mode
                arena.ArenaId = Flow.FightArenaId;   // floor tiles and wall colors of this fight's arena (-bbForceArena aware)
                arena.Boss = BossLookFor(Flow.Run);
                arena.ChapterIndex = Flow.Run.Chapter;   // the chapter's backdrop around the arena
                arena.StartMatch(sim, "YOU", Flow.Run.IsBossFight ? "BOSS" : "FOE");
                // Stat cards taken this run: HUD badges with the card icons (DAMAGE, SPEED CAP, MAX HP).
                var build = Flow.Run.Build;
                arena.Badges?.SetCardBadges(0, build.DamagePct > BallBattle.Sim.Fix.Zero, build.SpeedPct > BallBattle.Sim.Fix.Zero, build.MaxHp > BallBattle.Sim.Run.RunTuning.PlayerStartHp);
                arena.Paused = true;
                if (Flow.Run.IsRetry) overlay.ShowRetryBanner();
            };
            Flow.FightBegan += () =>
            {
                arena.Paused = false;
                arena.TimeScale = ChosenSpeed;   // X2 stays on from fight to fight (it used to drop to x1 while the button said X2)
                overlay.SetSpeed(ChosenSpeed);
                Debug.Log(FightLog(arena.Sim == Flow.CurrentMatch ? "began" : "began ON ANOTHER MATCH", arena.Sim));
            };
            Flow.Closed += () =>
            {
                HideAll();
                arena.Paused = true;
                arena.TimeScale = 1f;
                arena.MarkedBall = -1;
                arena.Boss = ArenaView.BossLook.None;
                arena.ChapterIndex = -1;
                Closed?.Invoke();
            };
            arena.MatchEnded += OnArenaMatchEnded;
            arena.SimEventRaised += OnArenaEvent;

            HideAll();
            overlay.SetSpeed(ChosenSpeed);
        }

        const string SpeedPref = "run_speed";

        /// <summary>The player's X1 / X2 choice; kept for the next fights and the next session.</summary>
        public float ChosenSpeed
        {
            get { var v = PlayerPrefs.GetFloat(SpeedPref, 1f); return v > 1f ? 2f : 1f; }
            set { PlayerPrefs.SetFloat(SpeedPref, value > 1f ? 2f : 1f); PlayerPrefs.Save(); }
        }

        void OnArenaMatchEnded(int winner) => Flow.OnMatchEnded(arena.Sim);

        /// <summary>Damage totals for the result screen: the sim's own event values, only while a Run fight plays.</summary>
        void OnArenaEvent(BallBattle.Sim.SimEvent e)
        {
            if (Flow.Current == RunFlow.State.Playing)
            {
                Flow.Stats.Add(e);
                if (Flow.Run?.TutorialRun == true && Flow.Run.FightIndex == 0 && e.A == 0 &&
                    (e.Type == BallBattle.Sim.SimEventType.TapStrike || e.Type == BallBattle.Sim.SimEventType.TapPerfect))
                {
                    Flow.MarkStrikeLearned();
                    arena.TimeScale = ChosenSpeed;
                }
                if (Flow.Run?.TutorialRun == true && Flow.Run.FightIndex == 0 && e.A == 0 &&
                    e.Type == BallBattle.Sim.SimEventType.SkillFx &&
                    e.Variant == (byte)BallBattle.Sim.SkillFxEvent.WreckLaunch)
                    Flow.MarkHintSeen("wrecker_release");
            }
        }

        void OnDestroy()
        {
            if (arena != null)
            {
                arena.MatchEnded -= OnArenaMatchEnded;
                arena.SimEventRaised -= OnArenaEvent;
            }
        }

        public void Open()
        {
            arena.Paused = true;
            Flow.Open();
        }

        void HideAll()
        {
            title.Hide();
            pick.Hide();
            startPick.Hide();
            prepare.Hide();
            overlay.Hide();
            summary.Hide();
            result.Hide();
            hints.Hide();
            tap?.Tick(null, 0f);
            chest?.Hide();
            shown = RunFlow.State.Closed;
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>One frame (Update's body; tests drive it with a fixed step).</summary>
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            if (overlay.PauseOpen) return;
            if (GuideView.Blocking)
            {
                // BI is talking: countdowns and fights wait, but the screen BI talks about is drawn first.
                if (dirty) Refresh();
                overlay.Render(Flow, 0f);   // the gear and X1/X2 BI points at
                return;
            }
            var wasPlaying = Flow.Current == RunFlow.State.Playing;
            CheckArenaShowsTheFight();
            if (!overlay.TickRetryBanner(dt)) Flow.Tick(dt);
            if (dirty) Refresh();
            overlay.Render(Flow, dt);
            result.Tick(dt);
            TickLesson(wasPlaying ? dt : 0f);
            tap.Tick(Flow, dt);
        }

        /// <summary>Show the screen for the current state; redraw it when its data changed.</summary>
        void Refresh()
        {
            dirty = false;
            var s = Flow.Current;
            if (s != shown)
            {
                title.Hide();
                pick.Hide();
                startPick.Hide();
                prepare.Hide();
                summary.Hide();
                result.Hide();
                shown = s;
            }

            switch (s)
            {
                case RunFlow.State.Title: title.Show(Flow); break;
                case RunFlow.State.PickWeapon: pick.Show(Flow.StartChoices); break;
                case RunFlow.State.StartPick: startPick.Show(Flow.Run); break;
                case RunFlow.State.Prepare:
                    prepare.ArenaOverride = Flow.ForceArenaId;
                    prepare.TutorialHintActive = !GuideView.Enabled && Flow.Run?.TutorialRun == true && !DebugOptions.NoTips && Flow.Progress.Onboarding.HintsOff == false &&
                        (Flow.Run.FightIndex == 1 && !Flow.HintSeen("shop") || Flow.Run.FightIndex == 3 && !Flow.HintSeen("boss"));
                    prepare.Show(Flow.Run);
                    break;
                case RunFlow.State.FightResult: result.Show(Flow.LastReport, Flow.Run?.TutorialRun == true); break;
                case RunFlow.State.Summary: summary.Show(Flow.Run, Flow.Progress); break;
            }
            // BB-12b: a chest earned by the run that just ended (summary) or by the saved run a new run replaced (start pick)
            // comes first; tips wait until it is collected.
            if (Flow.ChestToShow != null && (s == RunFlow.State.Summary || s == RunFlow.State.StartPick) && !chest.IsOpen)
                chest.Show(Flow.ChestToShow, Flow.Progress, Flow.ChestCaption, summary.transform, startPick.transform);
            OfferHint(chest.IsOpen ? RunFlow.State.Closed : s);
        }

        void OfferHint(RunFlow.State state)
        {
            hints.Hide();
            if (GuideView.Enabled) return;   // BI explains each screen now: no floating hint captions on top of it
            if (DebugOptions.NoTips || !(Flow.Run?.TutorialRun == true || state == RunFlow.State.PickWeapon && Flow.PendingTutorial) ||
                Flow.Progress?.Onboarding == null || Flow.Progress.Onboarding.HintsOff) return;
            foreach (var hint in HintPointer.Hints)
            {
                if (hint.Screen != state || Flow.HintSeen(hint.Id)) continue;
                if (hint.Id == "carry" && Flow.LastReport?.FightNumber != 1) continue;
                if (hint.Id == "shop" && (Flow.Run.FightIndex != 1 || Flow.Run.Coins <= 0)) continue;
                if (hint.Id == "boss" && Flow.Run.FightIndex != 3) continue;
                hints.Show(hint, hint.Id == "next" ? HintPointer.NextStepLine(Flow.Progress) : null);
                return;
            }
        }

        void TickLesson(float dt)
        {
            var firstFight = Flow.Current == RunFlow.State.Playing && Flow.Run?.TutorialRun == true &&
                             Flow.Run.FightIndex == 0 && Flow.Progress?.Onboarding?.HintsOff == false &&
                             !DebugOptions.NoTips;
            var lesson = firstFight && Flow.Progress.Onboarding.StrikeLearned == false;
            tap.TutorialLesson = lesson;
            tap.TutorialWreckerFollowUp = firstFight && !Flow.HintSeen("wrecker_release");
            var state = lesson && arena.Sim?.Balls.Count > 0 ? arena.Sim.Balls[0].Tap : null;
            if (state != null && state.Ready && state.Taps == 0)
            {
                // Give the first skill a brief window to notice. The prompt stays visible,
                // but a player who does not tap must still be able to watch the fight at full speed.
                if (tutorialSlowElapsed < TutorialSlowLimitSeconds) arena.TimeScale = 0.35f;
                else if (arena.TimeScale < 1f) arena.TimeScale = ChosenSpeed;
                tutorialSlowElapsed += Mathf.Max(0f, dt);
            }
            else
            {
                if (!lesson) tutorialSlowElapsed = 0f;
                if (arena.TimeScale < 1f) arena.TimeScale = ChosenSpeed;
            }
        }

        /// <summary>
        /// ART-2: dedicated 64px bodies on Prepare and in the arena. The old weapon ball remains the disabled-art fallback.
        /// </summary>
        public static bool BossArtEnabled = true;

        public static ArenaView.BossLook BossLookFor(BallBattle.Sim.Run.RunState run)
        {
            if (!BossArtEnabled || run == null || !run.IsBossFight) return ArenaView.BossLook.None;
            switch (run.CurrentBossId)
            {
                case BallBattle.Sim.Boss.BossIds.ShellKing: return ArenaView.BossLook.ShellKing;
                case BallBattle.Sim.Boss.BossIds.Mimic: return ArenaView.BossLook.Mimic;
                case BallBattle.Sim.Boss.BossIds.Magnet: return ArenaView.BossLook.Magnet;
                case BallBattle.Sim.Boss.BossIds.Warden: return ArenaView.BossLook.Warden;
                case BallBattle.Sim.Boss.BossIds.Hunter: return ArenaView.BossLook.Hunter;
                case BallBattle.Sim.Boss.BossIds.Bell: return ArenaView.BossLook.Bell;
                case BallBattle.Sim.Boss.BossIds.Ember: return ArenaView.BossLook.Ember;
                case BallBattle.Sim.Boss.BossIds.Prism: return ArenaView.BossLook.Prism;
                default: return ArenaView.BossLook.None;
            }
        }
        bool mismatchLogged;

        /// <summary>
        /// Field check for the "HP jumps when the fight starts / Second Wind never fires" report: from the countdown to the
        /// result, the arena must show and step the very match the run prepared. Logged once per fight if not.
        /// </summary>
        void CheckArenaShowsTheFight()
        {
            var s = Flow.Current;
            var inFight = s == RunFlow.State.Countdown || s == RunFlow.State.Playing || s == RunFlow.State.FightOver;
            if (!inFight || Flow.CurrentMatch == null) { mismatchLogged = false; return; }
            if (arena.Sim == Flow.CurrentMatch || mismatchLogged) return;
            mismatchLogged = true;
            Debug.LogError("[BallBattle] The arena is not showing the Run fight. " + FightLog("expected", Flow.CurrentMatch) + " | " + FightLog("shown", arena.Sim));
        }

        /// <summary>One line per fight start in the log: seed, both balls' HP and traits (to compare with what the screen showed).</summary>
        string FightLog(string what, BallBattle.Sim.MatchSim sim)
        {
            if (sim == null) return $"[BallBattle] Fight {what}: no match";
            string Ball(int i)
            {
                if (i >= sim.Balls.Count) return "-";
                var b = sim.Balls[i];
                var traits = new System.Text.StringBuilder();
                foreach (var t in b.Traits) traits.Append(traits.Length > 0 ? "," : "").Append(t.Id).Append(t.Level);
                return $"{b.Weapon.Id} HP {b.Hp.ToFloat():0.##}/{b.MaxHp.ToFloat():0.##} [{traits}]";
            }
            var fight = Flow.Run != null ? Flow.Run.FightIndex + 1 : 0;
            return $"[BallBattle] Fight {fight} {what}: seed {sim.Seed}, tick {sim.Tick}, you {Ball(0)}, enemy {Ball(1)}";
        }

        /// <summary>Seed of a NEW run: the -bbSeed value when given (logged), else random. CONTINUE never comes here.</summary>
        static uint NewSeed() => NewSeed(DebugOptions.ForceSeed);

        public static uint NewSeed(uint? forced)
        {
            if (forced.HasValue)
            {
                Debug.Log("[BallBattle] Run seed forced: " + forced.Value);
                return forced.Value;
            }
            return (uint)UnityEngine.Random.Range(1, int.MaxValue);
        }
    }
}
