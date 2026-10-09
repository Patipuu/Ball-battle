using System;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Run;

namespace BallBattle.View
{
    /// <summary>
    /// Run mode screens: Title → PickWeapon → (Prepare → Countdown → Playing → FightOver)×8 → Summary.
    /// Plain C# so it is unit-testable; RunController drives it with Tick(dt) and the arena's match end.
    /// Saves after every change. Starting a fight locks the run and saves, so quitting during a fight (even after
    /// seeing its result) resumes straight into that same fight with the same build: the result cannot be changed.
    /// </summary>
    public sealed class RunFlow
    {
        public enum State { Closed, Title, PickWeapon, Prepare, Countdown, Playing, FightOver, Summary }

        public readonly float CountdownSeconds;
        public readonly float FightOverSeconds;

        readonly ISaveStore store;
        SaveData save;
        MatchSim match;
        float timer;
        uint pendingSeed;

        public State Current { get; private set; } = State.Closed;
        public RunState Run { get; private set; }
        public ProgressData Progress => save.Progress;
        public bool HasSavedRun => save != null && save.HasRun;
        public List<string> StartChoices { get; private set; } = new List<string>();
        public bool LastFightWon { get; private set; }
        public bool LastFightDraw { get; private set; }
        public float TimeLeft => timer;
        public int CountdownNumber => Current == State.Countdown ? Math.Max(1, (int)Math.Ceiling(timer)) : 0;

        /// <summary>A fight is ready: show it frozen for the countdown.</summary>
        public event Action<MatchSim> MatchPrepared;
        /// <summary>Countdown over: unfreeze.</summary>
        public event Action FightBegan;
        /// <summary>Anything the screens show changed (state, coins, offer, build).</summary>
        public event Action Changed;
        /// <summary>Player left Run mode.</summary>
        public event Action Closed;

        public RunFlow(ISaveStore store, float countdownSeconds = 2f, float fightOverSeconds = 2f)
        {
            this.store = store;
            CountdownSeconds = countdownSeconds;
            FightOverSeconds = fightOverSeconds;
        }

        public void Open()
        {
            save = store.Load() ?? new SaveData();
            if (save.HasRun && !RunValidation.IsValid(save.Run))
            {
                save.HasRun = false;   // unreadable or from an older build: drop the run, keep progress
                save.Run = new RunState();
                store.Save(save);
            }
            Go(State.Title);
        }

        public void Close()
        {
            if (Current == State.Closed) return;
            Current = State.Closed;
            match = null;
            Closed?.Invoke();
        }

        /// <summary>Starts choosing a new run. A saved run is abandoned (counted as played) once a weapon is chosen.</summary>
        public void NewRun(uint seed)
        {
            if (Current != State.Title && Current != State.Summary) return;
            Run = null;
            StartChoices = RunState.StartChoices(seed, UnlockRules.Weapons(save.Progress));
            pendingSeed = seed;
            Go(State.PickWeapon);
        }

        public void ChooseWeapon(string weaponId)
        {
            if (Current != State.PickWeapon || !StartChoices.Contains(weaponId)) return;
            if (save.HasRun) UnlockRules.RecordAbandoned(save.Progress);
            Run = RunState.Start(pendingSeed, weaponId, save.Progress);
            Persist();
            Go(State.Prepare);
        }

        public void Continue()
        {
            if (Current != State.Title || !HasSavedRun) return;
            Run = save.Run;
            if (Run.FightLocked) StartFight();   // quit mid-fight: replay that exact fight, no edits
            else Go(State.Prepare);
        }

        public void Reroll()
        {
            if (Current != State.Prepare || !Run.CanReroll) return;
            Run.Reroll();
            Persist();
            Changed?.Invoke();
        }

        public void Pick(int index)
        {
            if (Current != State.Prepare || !Run.CanPick(index)) return;
            Run.Pick(index);
            Persist();
            Changed?.Invoke();
        }

        public void Fight()
        {
            if (Current != State.Prepare) return;
            Run.FightLocked = true;
            Persist();
            StartFight();
        }

        void StartFight()
        {
            match = Run.CreateMatch();
            MatchPrepared?.Invoke(match);
            timer = CountdownSeconds;
            Go(State.Countdown);
        }

        /// <summary>The arena reports the end of the current fight.</summary>
        public void OnMatchEnded(MatchSim finished)
        {
            if (Current != State.Playing || finished != match) return;
            LastFightWon = finished.WinnerIndex == 0;
            LastFightDraw = finished.WinnerIndex < 0;
            timer = FightOverSeconds;
            Go(State.FightOver);
        }

        public void Tick(float dt)
        {
            if (Current == State.Countdown)
            {
                timer -= dt;
                if (timer > 0f) return;
                Go(State.Playing);
                FightBegan?.Invoke();
            }
            else if (Current == State.FightOver)
            {
                timer -= dt;
                if (timer <= 0f && match != null) FinishFight();
            }
        }

        void FinishFight()
        {
            var finished = match;
            match = null;   // never applied twice, even if saving fails below
            Run.ApplyResult(finished);
            UnlockRules.Record(save.Progress, Run);
            Go(Run.Over ? State.Summary : State.Prepare);
            Persist();
        }

        void Persist()
        {
            save.HasRun = Run != null && !Run.Over;
            save.Run = save.HasRun ? Run : new RunState();
            store.Save(save);
        }

        void Go(State s)
        {
            Current = s;
            Changed?.Invoke();
        }
    }
}
