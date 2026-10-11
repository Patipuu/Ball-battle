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
            prepare = PrepareView.Create(transform, art);
            overlay = RunOverlayView.Create(transform, art);
            summary = RunSummaryView.Create(transform, art);
            result = RunResultView.Create(transform, art);

            title.ContinueRequested += () => Flow.Continue();
            title.NewRunRequested += () => Flow.NewRun(NewSeed());
            title.BackRequested += () => Flow.Close();
            pick.WeaponPicked += id => Flow.ChooseWeapon(id);
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
                arena.MarkedBall = 0;   // ball 0 is always the player in Run mode
                arena.StartMatch(sim, "YOU", Flow.Run.IsBossFight ? "BOSS" : "FOE");
                arena.Paused = true;
            };
            Flow.FightBegan += () => arena.Paused = false;
            Flow.Closed += () =>
            {
                HideAll();
                arena.Paused = true;
                arena.TimeScale = 1f;
                arena.MarkedBall = -1;
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
            prepare.Hide();
            overlay.Hide();
            summary.Hide();
            result.Hide();
            shown = RunFlow.State.Closed;
        }

        void Update()
        {
            if (!IsOpen) return;
            var dt = Time.deltaTime;
            Flow.Tick(dt);
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
                prepare.Hide();
                summary.Hide();
                result.Hide();
                shown = s;
            }

            switch (s)
            {
                case RunFlow.State.Title: title.Show(Flow); break;
                case RunFlow.State.PickWeapon: pick.Show(Flow.StartChoices); break;
                case RunFlow.State.Prepare: prepare.Show(Flow.Run); break;
                case RunFlow.State.FightResult: result.Show(Flow.LastReport); break;
                case RunFlow.State.Summary: summary.Show(Flow.Run); break;
            }
        }

        static uint NewSeed() => (uint)UnityEngine.Random.Range(1, int.MaxValue);
    }
}
