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
        RunFlow.State shown = RunFlow.State.Closed;
        bool dirty;

        public RunFlow Flow { get; private set; }
        public bool IsOpen => Flow.Current != RunFlow.State.Closed;
        public PrepareView Prepare => prepare;
        public RunResultView Result => result;

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
            title = RunTitleView.Create(transform, art);
            pick = PickWeaponView.Create(transform, art);
            startPick = StartPickView.Create(transform, art);
            prepare = PrepareView.Create(transform, art);
            overlay = RunOverlayView.Create(transform, art);
            summary = RunSummaryView.Create(transform, art);
            result = RunResultView.Create(transform, art);

            title.ContinueRequested += () => Flow.Continue();
            title.NewRunRequested += () => Flow.NewRun(NewSeed());
            title.BackRequested += () => Flow.Close();
            pick.WeaponPicked += id => Flow.ChooseWeapon(id);
            startPick.CardPicked += i => Flow.PickStart(i);
            prepare.CardPicked += i => Flow.Pick(i);
            prepare.RerollRequested += () => Flow.Reroll();
            prepare.FightRequested += () => Flow.Fight();
            overlay.SpeedToggled += () =>
            {
                arena.TimeScale = arena.TimeScale > 1f ? 1f : 2f;
                overlay.SetSpeed(arena.TimeScale);
            };
            result.ContinueRequested += () => Flow.NextAfterResult();
            summary.NewRunRequested += () => Flow.NewRun(NewSeed());
            summary.MenuRequested += () => Flow.Close();

            Flow.Changed += () => dirty = true;
            Flow.MatchPrepared += sim =>
            {
                Debug.Log(FightLog("prepared", sim));
                arena.MarkedBall = 0;   // ball 0 is always the player in Run mode
                arena.ArenaId = Flow.Run.ArenaId;   // floor tiles and wall colors of this fight's arena
                arena.Boss = BossLookFor(Flow.Run);
                arena.StartMatch(sim, "YOU", Flow.Run.IsBossFight ? "BOSS" : "FOE");
                // Stat cards taken this run: HUD badges with the card icons (DAMAGE, SPEED CAP, MAX HP).
                var build = Flow.Run.Build;
                arena.Badges?.SetCardBadges(0, build.DamagePct > 0f, build.SpeedPct > 0f, build.MaxHp > BallBattle.Sim.Run.RunTuning.PlayerStartHp);
                arena.Paused = true;
                if (Flow.Run.IsRetry) overlay.ShowRetryBanner();
            };
            Flow.FightBegan += () =>
            {
                arena.Paused = false;
                Debug.Log(FightLog(arena.Sim == Flow.CurrentMatch ? "began" : "began ON ANOTHER MATCH", arena.Sim));
            };
            Flow.Closed += () =>
            {
                HideAll();
                arena.Paused = true;
                arena.TimeScale = 1f;
                arena.MarkedBall = -1;
                arena.Boss = ArenaView.BossLook.None;
                Closed?.Invoke();
            };
            arena.MatchEnded += OnArenaMatchEnded;
            arena.SimEventRaised += OnArenaEvent;

            HideAll();
            overlay.SetSpeed(1f);
        }

        void OnArenaMatchEnded(int winner) => Flow.OnMatchEnded(arena.Sim);

        /// <summary>Damage totals for the result screen: the sim's own event values, only while a Run fight plays.</summary>
        void OnArenaEvent(BallBattle.Sim.SimEvent e)
        {
            if (Flow.Current == RunFlow.State.Playing) Flow.Stats.Add(e);
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
            shown = RunFlow.State.Closed;
        }

        void Update() => Tick(Time.deltaTime);

        /// <summary>One frame (Update's body; tests drive it with a fixed step).</summary>
        public void Tick(float dt)
        {
            if (!IsOpen) return;
            CheckArenaShowsTheFight();
            if (!overlay.TickRetryBanner(dt)) Flow.Tick(dt);
            if (dirty) Refresh();
            overlay.Render(Flow, dt);
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
                case RunFlow.State.Prepare: prepare.Show(Flow.Run); break;
                case RunFlow.State.FightResult: result.Show(Flow.LastReport); break;
                case RunFlow.State.Summary: summary.Show(Flow.Run, Flow.Progress); break;
            }
        }

        /// <summary>
        /// Shell King / Mimic art switch. OFF until the sim has those two bosses (v4): today a boss is an ordinary weapon
        /// with boss stats, so it is drawn with its own pre-drawn x1.5 ball (ball_<id>_48, dark outline), the same sprite
        /// on Prepare and in the arena. Flip <see cref="BossArtEnabled"/> (and keep SetMimicLook / glow) when v4 lands.
        /// </summary>
        public static bool BossArtEnabled = false;

        public static ArenaView.BossLook BossLookFor(BallBattle.Sim.Run.RunState run)
        {
            if (!BossArtEnabled || run == null || !run.IsBossFight) return ArenaView.BossLook.None;
            var last = BallBattle.Sim.Run.RunTuning.BossFights[BallBattle.Sim.Run.RunTuning.BossFights.Length - 1];
            return run.FightIndex == last ? ArenaView.BossLook.Mimic : ArenaView.BossLook.ShellKing;
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
                return $"{b.Weapon.Id} HP {b.Hp:0.##}/{b.MaxHp:0.##} [{traits}]";
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
