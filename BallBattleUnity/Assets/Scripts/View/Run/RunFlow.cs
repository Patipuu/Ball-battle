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
        bool pendingTutorial;
        int pendingChapter, pendingHeat;
        public bool PendingTutorial => pendingTutorial;

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

        /// <summary>Tester switch (-bbForceBoss, BB-04): this boss takes its slot and the next non-boss fight. Null = normal play.</summary>
        public string ForceBossId { get; set; }

        /// <summary>
        /// Tester switch (-bbForceArena, BB-6d): every fight started while set uses this arena. View-side only: RunState.ArenaId, the
        /// saved run and every seed are untouched. Null = normal play.
        /// </summary>
        public string ForceArenaId { get; set; }
        public BallBattle.Sim.Skills.SkillSpec? ForceSkill { get; set; }

        /// <summary>Arena of the current fight as played: the forced one, else the run's own (RunState.ArenaId).</summary>
        public string FightArenaId => Run == null ? null : ForceArenaId ?? Run.ArenaId;

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
            // The locked fight is intentionally replayed from its saved start on Continue.
            if (save != null) store.Save(save);
            Current = State.Closed;
            match = null;
            Closed?.Invoke();
        }

        /// <summary>End the saved run immediately from the hub and keep all earned meta progress.</summary>
        public void AbandonSavedRun()
        {
            save = store.Load() ?? new SaveData();
            if (!save.HasRun) return;
            UnlockRules.RecordAbandoned(save.Progress);
            LastChest = AwardAbandoned(save.Progress, save.Run);
            LastChestAbandoned = true;
            chestToShow = LastChest != null;
            save.HasRun = false;
            save.Run = new RunSave();
            Run = null;
            store.Save(save);
            Changed?.Invoke();
        }

        /// <summary>Starts choosing a new run. A saved run is abandoned (counted as played) once a weapon is chosen.</summary>
        public void NewRun(uint seed, bool tutorial = false, int chapter = 0, int heat = 0)
        {
            if (Current != State.Title && Current != State.Summary) return;
            Run = null;
            pendingTutorial = tutorial && save.Progress.RunsPlayed == 0 && !save.HasRun;
            if (pendingTutorial) seed = RunState.TutorialSeed;
            StartChoices = RunState.StartChoices(seed, UnlockRules.Weapons(save.Progress), pendingTutorial);
            pendingSeed = seed;
            pendingChapter = pendingTutorial ? 0 : System.Math.Min(System.Math.Max(0, chapter), Chapters.Unlocked(save.Progress));
            pendingHeat = pendingTutorial ? 0 : System.Math.Min(System.Math.Max(0, heat), HeatRules.Unlocked(save.Progress, pendingChapter));
            Go(State.PickWeapon);
        }

        public void ChooseWeapon(string weaponId)
        {
            if (Current != State.PickWeapon || !StartChoices.Contains(weaponId)) return;
            if (save.HasRun)
            {
                UnlockRules.RecordAbandoned(save.Progress);
                LastChest = AwardAbandoned(save.Progress, save.Run);   // BB-12a: an abandoned run that played a fight still earns its chest
                LastChestAbandoned = true;
                chestToShow = LastChest != null;
            }
            Run = RunState.Start(pendingSeed, weaponId, save.Progress, ForceSkill, pendingTutorial, pendingChapter, pendingHeat);
            if (pendingTutorial) save.Progress.Onboarding.AdvanceTo(1);
            BossSelection.Remember(save.Progress, Run);   // BB-04: before any forced boss, so -bbForceBoss never enters the history
            if (ForceBossId != null) Run.ApplyForcedBoss(ForceBossId);
            UnityEngine.Debug.Log(RunStartLog(pendingSeed, weaponId, ForceTraitId) + $", bosses {string.Join("/", Run.BossIds)}" + (ForceBossId != null ? $", -bbForceBoss {ForceBossId}" : ""));
            DebugOptions.ApplyForcedTrait(Run, ForceTraitId);
            Persist();
            Go(State.StartPick);
        }

        /// <summary>
        /// BB-FIX5: the -bbForceSkill override for a resumed run. The save never holds the forced level (BB-10a), only the SKILL UPs the
        /// player really took: each one raised the saved build by one level while the unsaved override rose with it. So the forced
        /// level plus the SKILL UPs taken since (build level - 1), capped at 3, is where the run really was; the branch is the forced
        /// one (or the build's when the flag is level 1). Before, CONTINUE reset to the flag's level and dropped a LV3 taken under 2a.
        /// </summary>
        public static BallBattle.Sim.Skills.SkillSpec? ResumedSkillOverride(BallBattle.Sim.Skills.SkillSpec? force, RunBuild build)
        {
            if (!force.HasValue) return null;
            var level = System.Math.Min(3, force.Value.Level + build.SkillLevel - 1);
            if (level <= 1) return new BallBattle.Sim.Skills.SkillSpec(1);
            var branch = force.Value.Level > 1 ? force.Value.Branch : build.SkillBranch;
            return new BallBattle.Sim.Skills.SkillSpec(level, branch);
        }

        /// <summary>Logged when a run starts, so a tester can report the seed and replay the same run.</summary>
        public static string RunStartLog(uint seed, string weaponId, string forceTraitId) =>
            $"[BallBattle] Run started: seed {seed}, weapon {weaponId}" + (forceTraitId != null ? $", -bbForceTrait {forceTraitId}" : "");

        public void Continue()
        {
            if (Current != State.Title || !HasSavedRun) return;
            Run = save.Run.ToRun();
            Run.SkillOverride = ResumedSkillOverride(ForceSkill, Run.Build);
            UnityEngine.Debug.Log($"[BallBattle] Run resumed: seed {Run.Seed}, fight {Run.FightIndex + 1}");
            var changed = DebugOptions.ApplyForcedTrait(Run, ForceTraitId);
            changed |= ApplyForceBossToNewFight();
            if (changed) Persist();
            if (Run.FightLocked) StartFight();   // quit mid-fight: replay that exact fight, no edits
            else Go(Run.StartPickPending ? State.StartPick : State.Prepare);
        }

        /// <summary>
        /// BB-VIEW3: -bbForceBoss applies only when a NEW fight comes up. A saved retry (Run.IsRetry: the same fight again, boss or
        /// not) keeps its saved enemy, so the force waits until that retry has been played; FinishFight then applies it to the next
        /// fight. Returns true when the run changed (to save it).
        /// </summary>
        bool ApplyForceBossToNewFight()
        {
            if (ForceBossId == null || Run == null || Run.Over || Run.IsRetry || Run.ForcedBossId == ForceBossId) return false;
            return Run.ApplyForcedBoss(ForceBossId);
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

        public void Pick(int index, BallBattle.Sim.Skills.SkillBranch branch = BallBattle.Sim.Skills.SkillBranch.A)
        {
            if (Current != State.Prepare || !Run.CanPick(index)) return;
            Run.Pick(index, branch);
            Persist();
            CardTaken?.Invoke();
            Changed?.Invoke();
        }

        public void PickStart(int index, BallBattle.Sim.Skills.SkillBranch branch = BallBattle.Sim.Skills.SkillBranch.A)
        {
            if (Current != State.StartPick || !Run.CanPick(index)) return;
            var retry = Run.IsRetry;
            Run.Pick(index, branch);
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
            match = Run.CreateMatch(ForceArenaId != null ? ArenaRegistry.Create(ForceArenaId) : null, ForceSkill);
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
            DailyQuests.Record(save.Progress, Run, NextGoalBox.Today);   // daily quests: finished ones drop their chest into PendingChests
            if (Run.Over) save.Progress.Onboarding.AdvanceTo(2);
            if (Run.Over)
            {
                LastChest = BallBattle.Sim.Meta.MetaRules.AwardRun(save.Progress, Run);   // BB-12a: saved by Persist below
                LastChestAbandoned = false;
                chestToShow = LastChest != null;
            }
            ApplyForceBossToNewFight();   // BB-VIEW3: a force deferred by a resumed retry starts with the next new fight
            LastReport = FightReport.Build(finished, Stats, before, Run, ForceArenaId);
            Go(State.FightResult);   // result screen first, then Prepare (or the summary)
            Persist();
        }

        /// <summary>Leave the post-fight result screen: next fight's Prepare, or the summary when the run is over.</summary>
        public void NextAfterResult()
        {
            if (Current != State.FightResult) return;
            Go(Run.Over ? State.Summary : Run.StartPickPending ? State.StartPick : State.Prepare);
        }

        /// <summary>BB-12a: the chest the last finished (or abandoned) run earned, already applied to Progress; null when none.</summary>
        public BallBattle.Sim.Meta.ChestResult LastChest { get; private set; }
        /// <summary>BB-12b: LastChest came from a saved run that a new run replaced.</summary>
        public bool LastChestAbandoned { get; private set; }
        bool chestToShow;
        /// <summary>BB-12b: the chest the CHEST screen still has to show (null once COLLECT closed it).</summary>
        public BallBattle.Sim.Meta.ChestResult ChestToShow => chestToShow ? LastChest : null;
        public void MarkChestShown() => chestToShow = false;
        /// <summary>BB-12b: line under the chest title.</summary>
        public string ChestCaption => LastChestAbandoned ? "FROM YOUR LAST RUN" :
            LastChest == null ? "" : LastChest.Tier == BallBattle.Sim.Meta.ChestTier.Gold ? "RUN WON" :
            LastChest.Tier == BallBattle.Sim.Meta.ChestTier.Silver ? "BOSS 1 BEATEN" : "RUN OVER";

        /// <summary>BB-12a: chest of a saved run that a new run replaces (tier from its fight history).</summary>
        public static BallBattle.Sim.Meta.ChestResult AwardAbandoned(ProgressData p, RunSave saved)
        {
            if (p == null || saved?.History == null) return null;
            var furthest = -1;
            foreach (var h in saved.History) if (h.Won && h.FightIndex > furthest) furthest = h.FightIndex;
            var tier = BallBattle.Sim.Meta.MetaRules.TierFor(false, furthest, saved.History.Count);
            return tier.HasValue ? BallBattle.Sim.Meta.MetaRules.Open(p, tier.Value, saved.Build?.WeaponId, unchecked((uint)saved.SeedBits)) : null;
        }

        /// <summary>UI-TUT1: has the first-run tip <paramref name="id"/> been shown (and closed) already.</summary>
        public bool TipSeen(string id) => save?.Progress?.SeenTips != null && save.Progress.SeenTips.Contains(id);

        public bool HintSeen(string id) => save?.Progress?.Onboarding?.HasSeen(id) == true;
        public void MarkHintSeen(string id)
        {
            if (save?.Progress?.Onboarding == null || save.Progress.Onboarding.HasSeen(id)) return;
            save.Progress.Onboarding.MarkSeen(id);
            store.Save(save);
        }
        public void MarkStrikeLearned()
        {
            if (save?.Progress?.Onboarding == null || save.Progress.Onboarding.StrikeLearned) return;
            save.Progress.Onboarding.StrikeLearned = true;
            save.Progress.Onboarding.MarkSeen("strike");
            store.Save(save);
        }

        /// <summary>UI-TUT1: remember tips as shown, saved at once (the run part of the save is left as last persisted).</summary>
        public void MarkTipsSeen(params string[] ids)
        {
            if (save == null) return;
            var p = save.Progress ?? (save.Progress = new ProgressData());
            if (p.SeenTips == null) p.SeenTips = new System.Collections.Generic.List<string>();
            foreach (var id in ids) if (!p.SeenTips.Contains(id)) p.SeenTips.Add(id);
            store.Save(save);
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
