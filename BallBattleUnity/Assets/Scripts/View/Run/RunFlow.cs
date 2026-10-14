using System;
using System.Collections.Generic;
using BallBattle.Sim;
using BallBattle.Sim.Run;

namespace BallBattle.View
{
    /// <summary>
    /// Run mode screens: Title → PickWeapon → (Prepare → Countdown → Playing → FightOver → FightResult)×8 → Summary.
    /// Plain C# so it is unit-testable; RunController drives it with Tick(dt) and the arena's match end.
    /// Saves after every change. Starting a fight locks the run and saves, so quitting during a fight (even after
    /// seeing its result) resumes straight into that same fight with the same build: the result cannot be changed.
    /// </summary>
    public sealed class RunFlow
    {
        public enum State { Closed, Title, PickWeapon, StartPick, Prepare, Countdown, Playing, FightOver, FightResult, Summary }

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

        /// <summary>Tester switch (-bbForceTrait): this trait is always in the offer before fight 1. Null = normal play.</summary>
        public string ForceTraitId { get; set; }

        /// <summary>Damage totals of the fight being played; the arena feeds it the sim's events (RunController).</summary>
        public FightStats Stats { get; } = new FightStats();

        /// <summary>The fight being played (from StartFight until its result is applied); null otherwise.</summary>
        public MatchSim CurrentMatch => match;

        /// <summary>Result of the last finished fight, shown on the FightResult screen.</summary>
        public FightReport LastReport { get; private set; }
        public int CountdownNumber => Current == State.Countdown ? Math.Max(1, (int)Math.Ceiling(timer)) : 0;

        /// <summary>A fight is ready: show it frozen for the countdown.</summary>
        public event Action<MatchSim> MatchPrepared;
        /// <summary>Countdown over: unfreeze.</summary>
        public event Action FightBegan;
        /// <summary>Anything the screens show changed (state, coins, offer, build).</summary>
        public event Action Changed;
        /// <summary>Player left Run mode.</summary>
        public event Action Closed;
        /// <summary>A card was taken (Prepare or the start/retry pick) / the offer was rerolled. Audio cues only.</summary>
        public event Action CardTaken, Rerolled;

        public RunFlow(ISaveStore store, float countdownSeconds = 2f, float fightOverSeconds = 2f)
        {
            this.store = store;
            CountdownSeconds = countdownSeconds;
            FightOverSeconds = fightOverSeconds;
        }

        public void Open()
        {
            save = store.Load() ?? new SaveData();
            if (save.HasRun && !RunValidation.IsValid(save.Run?.ToRun()))
            {
                save.HasRun = false;   // unreadable or from an older build: drop the run, keep progress
                save.Run = new RunSave();
                store.Save(save);
            }
            Go(State.Title);
        }

        /// <summary>
        /// Progress and the run CONTINUE would resume (null when none or no longer valid), for the main menu.
        /// Reads the save without opening Run mode and without writing anything.
        /// </summary>
        public (ProgressData progress, RunState resumable) PeekSave()
        {
            var data = store.Load() ?? new SaveData();
            var saved = data.HasRun ? data.Run?.ToRun() : null;
            var run = RunValidation.IsValid(saved) ? saved : null;
            return (data.Progress ?? new ProgressData(), run);
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
            UnityEngine.Debug.Log(RunStartLog(pendingSeed, weaponId, ForceTraitId));
            DebugOptions.ApplyForcedTrait(Run, ForceTraitId);
            Persist();
            Go(State.StartPick);
        }

        /// <summary>Logged when a run starts, so a tester can report the seed and replay the same run.</summary>
        public static string RunStartLog(uint seed, string weaponId, string forceTraitId) =>
            $"[BallBattle] Run started: seed {seed}, weapon {weaponId}" + (forceTraitId != null ? $", -bbForceTrait {forceTraitId}" : "");

        public void Continue()
        {
            if (Current != State.Title || !HasSavedRun) return;
            Run = save.Run.ToRun();
            UnityEngine.Debug.Log($"[BallBattle] Run resumed: seed {Run.Seed}, fight {Run.FightIndex + 1}");
            if (DebugOptions.ApplyForcedTrait(Run, ForceTraitId)) Persist();
            if (Run.FightLocked) StartFight();   // quit mid-fight: replay that exact fight, no edits
            else Go(Run.StartPickPending ? State.StartPick : State.Prepare);
        }

        public void Reroll()
        {
            if (Current != State.Prepare || !Run.CanReroll) return;
            Run.Reroll();
            DebugOptions.ApplyForcedTrait(Run, ForceTraitId);
            Persist();
            Rerolled?.Invoke();
            Changed?.Invoke();
        }

        public void Pick(int index)
        {
            if (Current != State.Prepare || !Run.CanPick(index)) return;
            Run.Pick(index);
            Persist();
            CardTaken?.Invoke();
            Changed?.Invoke();
        }

        public void PickStart(int index)
        {
            if (Current != State.StartPick || !Run.CanPick(index)) return;
            var retry = Run.IsRetry;
            Run.Pick(index);
            if (retry) Run.FightLocked = true;
            Persist();
            CardTaken?.Invoke();
            if (retry) StartFight();
            else Go(State.Prepare);
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
            Stats.Reset(match);
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
            UnityEngine.Debug.Log(FightLog.Ended($"Fight {Run.FightIndex + 1}", finished, Stats, Run.Seed));   // real values, for testers' logs
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
            var before = FightReport.Snapshot.Of(Run);
            Run.ApplyResult(finished);
            UnlockRules.Record(save.Progress, Run);
            LastReport = FightReport.Build(finished, Stats, before, Run);
            Go(State.FightResult);   // result screen first, then Prepare (or the summary)
            Persist();
        }

        /// <summary>Leave the post-fight result screen: next fight's Prepare, or the summary when the run is over.</summary>
        public void NextAfterResult()
        {
            if (Current != State.FightResult) return;
            Go(Run.Over ? State.Summary : Run.StartPickPending ? State.StartPick : State.Prepare);
        }

        void Persist()
        {
            save.HasRun = Run != null && !Run.Over;
            save.Run = save.HasRun ? RunSave.From(Run) : new RunSave();
            store.Save(save);
        }

        void Go(State s)
        {
            Current = s;
            Changed?.Invoke();
        }
    }
}
