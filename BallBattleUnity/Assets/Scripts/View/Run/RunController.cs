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
        RunFlow.State shown = RunFlow.State.Closed;
        bool dirty;

        public RunFlow Flow { get; private set; }
        public bool IsOpen => Flow.Current != RunFlow.State.Closed;
        public PrepareView Prepare => prepare;

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
            title = RunTitleView.Create(transform, art);
            pick = PickWeaponView.Create(transform, art);
            prepare = PrepareView.Create(transform, art);
            overlay = RunOverlayView.Create(transform, art);
            summary = RunSummaryView.Create(transform, art);

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
            summary.NewRunRequested += () => Flow.NewRun(NewSeed());
            summary.MenuRequested += () => Flow.Close();

            Flow.Changed += () => dirty = true;
            Flow.MatchPrepared += sim =>
            {
                arena.StartMatch(sim, "YOU", Flow.Run.IsBossFight ? "BOSS" : "FOE");
                arena.Paused = true;
            };
            Flow.FightBegan += () => arena.Paused = false;
            Flow.Closed += () =>
            {
                HideAll();
                arena.Paused = true;
                arena.TimeScale = 1f;
                Closed?.Invoke();
            };
            arena.MatchEnded += OnArenaMatchEnded;

            HideAll();
            overlay.SetSpeed(1f);
        }

        void OnArenaMatchEnded(int winner) => Flow.OnMatchEnded(arena.Sim);

        void OnDestroy()
        {
            if (arena != null) arena.MatchEnded -= OnArenaMatchEnded;
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
                shown = s;
            }

            switch (s)
            {
                case RunFlow.State.Title: title.Show(Flow); break;
                case RunFlow.State.PickWeapon: pick.Show(Flow.StartChoices); break;
                case RunFlow.State.Prepare: prepare.Show(Flow.Run); break;
                case RunFlow.State.Summary: summary.Show(Flow.Run); break;
            }
        }

        static uint NewSeed() => (uint)UnityEngine.Random.Range(1, int.MaxValue);
    }
}
