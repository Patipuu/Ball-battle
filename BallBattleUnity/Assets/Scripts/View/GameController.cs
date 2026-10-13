using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Wires the screens together: MenuView → MatchFlow (best-of-3) → ArenaView rounds → OverlayView result,
    /// or MenuView → RunController (Run mode, its own screens) → back to the menu.
    /// The arena is frozen (Paused) in the menu and during countdowns; one clock (Time.deltaTime) drives the flow.
    /// Audio (BB-03): AudioDirector gets the arena's events, the screen shown each frame and the Run flow's cues.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public ArenaView Arena;

        public MatchFlow Flow { get; private set; }
        public MenuView Menu { get; private set; }
        public OverlayView Overlay { get; private set; }
        public RunController Run { get; private set; }
        public AudioDirector Audio { get; private set; }

        void Awake()
        {
            if (Arena == null) Arena = FindAnyObjectByType<ArenaView>();
            Arena.AutoRestart = false;
            Arena.Paused = true;

            Flow = new MatchFlow();
            Menu = MenuView.Create(transform, Arena.Art);
            Overlay = OverlayView.Create(transform, Arena.Art);
            var store = FileSaveStore.Default();
            Run = RunController.Create(transform, Arena, store);
            var fx = Arena.GetComponent<FxView>();
            Audio = AudioDirector.Create(transform, fx != null ? fx.Sfx : null, store);
            WireAudio();

            Flow.RoundPrepared += (a, b, seed) =>
            {
                Menu.Show(false);
                Arena.StartMatch(a, b, seed);
                versusStats.Reset(Arena.Sim);
                Arena.Paused = true;   // frozen during the countdown
            };
            Flow.RoundBegan += () => Arena.Paused = false;
            Flow.MenuShown += () =>
            {
                Arena.Paused = true;
                var (progress, resumable) = Run.Flow.PeekSave();
                Menu.SetRunInfo(progress, resumable);
                Menu.Show(true);
            };

            Menu.StartRequested += (a, b) =>
            {
                Arena.ArenaId = Menu.ArenaId;
                Flow.Begin(a, b, NewVersusSeed(DebugOptions.ForceSeed));
            };
            Menu.RunRequested += () => OpenRun(false);
            Menu.ContinueRequested += () => OpenRun(true);
            Run.Closed += () => Flow.ToMenu();
            Overlay.RematchRequested += () => Flow.Rematch();
            Overlay.NewMatchRequested += () => Flow.NewMatch(NewVersusSeed(DebugOptions.ForceSeed));
            Overlay.MenuRequested += () => Flow.ToMenu();
        }

        /// <summary>RUN opens Run mode on its title screen; CONTINUE RUN opens it and resumes the saved run at once.</summary>
        void OpenRun(bool resume)
        {
            if (Flow.Current != MatchFlow.State.Menu) return;
            Menu.Show(false);
            Run.Open();
            if (resume && Run.Flow.HasSavedRun) Run.Flow.Continue();
        }

        /// <summary>Flow and screen events → AudioDirector (the arena's own events are hooked in OnEnable).</summary>
        void WireAudio()
        {
            Menu.Settings.SetValues(Audio.MusicVolume, Audio.SfxVolume);
            Menu.Settings.MusicChanged += v => Audio.SetMusicVolume(v, true);
            Menu.Settings.SfxChanged += v => Audio.SetSfxVolume(v, true);
            Flow.RoundBegan += () => Audio.RequestUiCue(AudioIds.FightStart);
            Run.Flow.FightBegan += () => Audio.RequestUiCue(AudioIds.FightStart);
            Run.Flow.CardTaken += () => Audio.RequestUiCue(AudioIds.CardPick);
            Run.Flow.Rerolled += () => Audio.RequestUiCue(AudioIds.CardReroll);
            Run.Flow.MatchPrepared += sim => { if (Run.Flow.Run != null && Run.Flow.Run.IsRetry) Audio.RequestUiCue(AudioIds.RetryBanner); };
            Run.Summary.Shown += () => Audio.OnSummaryShown(Run.Flow.Run != null && Run.Flow.Run.Won, Run.Summary.MilestonesShown);
        }

        void OnEnable()
        {
            if (Arena != null) Arena.MatchEnded += OnRoundEnded;
            if (Arena != null) Arena.SimEventRaised += OnVersusEvent;
            if (Arena != null) Arena.SimEventRaised += OnAudioEvent;
            if (Arena != null) Arena.MatchEnded += OnAudioMatchEnded;
            if (Arena != null) Arena.MatchStarted += OnAudioMatchStarted;
        }

        void OnDisable()
        {
            if (Arena != null) Arena.MatchEnded -= OnRoundEnded;
            if (Arena != null) Arena.SimEventRaised -= OnVersusEvent;
            if (Arena != null) Arena.SimEventRaised -= OnAudioEvent;
            if (Arena != null) Arena.MatchEnded -= OnAudioMatchEnded;
            if (Arena != null) Arena.MatchStarted -= OnAudioMatchStarted;
        }

        void OnAudioEvent(BallBattle.Sim.SimEvent e) => Audio?.OnSimEvent(e, Arena.Sim);
        void OnAudioMatchEnded(int winner) => Audio?.OnMatchEnded(winner, Arena.MarkedBall);
        void OnAudioMatchStarted() => Audio?.OnMatchStarted();

        /// <summary>Screen shown now, for the music (AudioIds.TrackFor).</summary>
        public MusicScreen CurrentScreen()
        {
            if (Run.IsOpen)
            {
                switch (Run.Flow.Current)
                {
                    case RunFlow.State.Title: return MusicScreen.RunTitle;
                    case RunFlow.State.PickWeapon: return MusicScreen.PickWeapon;
                    case RunFlow.State.StartPick: return MusicScreen.StartPick;
                    case RunFlow.State.Prepare: return MusicScreen.Prepare;
                    case RunFlow.State.Countdown:
                    case RunFlow.State.Playing:
                    case RunFlow.State.FightOver:
                        return Run.Flow.Run != null && Run.Flow.Run.IsBossFight ? MusicScreen.BossFight : MusicScreen.Fight;
                    case RunFlow.State.FightResult: return MusicScreen.FightResult;
                    case RunFlow.State.Summary: return MusicScreen.Summary;
                    default: return MusicScreen.None;
                }
            }
            switch (Flow.Current)
            {
                case MatchFlow.State.Menu: return Menu.SettingsOpen ? MusicScreen.Settings : Menu.VersusOpen ? MusicScreen.VersusPick : MusicScreen.Menu;
                case MatchFlow.State.Result: return MusicScreen.VersusResult;
                default: return MusicScreen.Fight;
            }
        }

        void Start() => Flow.ToMenu();

        /// <summary>Versus round totals for the end-of-fight log line (Run mode keeps its own in RunFlow.Stats).</summary>
        readonly FightStats versusStats = new FightStats();

        void OnVersusEvent(BallBattle.Sim.SimEvent e)
        {
            if (!Run.IsOpen && Flow.Current == MatchFlow.State.Playing) versusStats.Add(e);
        }

        void OnRoundEnded(int winner)
        {
            if (!Run.IsOpen && Flow.Current == MatchFlow.State.Playing)
            {
                Debug.Log(FightLog.Ended("Versus round " + Flow.Series.DisplayRound, Arena.Sim, versusStats));
                Debug.Log(VersusRoundLog.Ended(Flow.Series, Arena.Sim));
            }
            Flow.OnRoundEnded(winner);
        }

        void Update()
        {
            // Esc on desktop = Back on Android: leave the match (or Run mode, which is saved) for the menu.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                var back = true;
                if (Run.IsOpen) Run.Flow.Close();
                else if (Flow.Current != MatchFlow.State.Menu) Flow.ToMenu();
                else if (Menu.VersusOpen || Menu.SettingsOpen) Menu.ShowHub();
                else back = false;
                if (back) UiAudio.Play(AudioIds.UiBack);
            }

            var dt = Time.deltaTime;
            Flow.Tick(dt);
            Overlay.Render(Flow, dt);
            Audio.SetScreen(CurrentScreen());
        }

        /// <summary>Match seeds are random per new match; shown on screen so a fight can be replayed.</summary>
        public static uint NewVersusSeed(uint? forced)
        {
            if (forced.HasValue)
            {
                Debug.Log("[BallBattle] Versus seed forced: " + forced.Value);
                return forced.Value;
            }
            return (uint)Random.Range(1, int.MaxValue);
        }
    }
}
