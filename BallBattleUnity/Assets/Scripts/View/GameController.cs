using UnityEngine;

namespace BallBattle.View
{
    /// <summary>
    /// Wires the screens together: MenuView → MatchFlow (best-of-3) → ArenaView rounds → OverlayView result,
    /// or MenuView → RunController (Run mode, its own screens) → back to the menu.
    /// The arena is frozen (Paused) in the menu and during countdowns; one clock (Time.deltaTime) drives the flow.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        public ArenaView Arena;

        public MatchFlow Flow { get; private set; }
        public MenuView Menu { get; private set; }
        public OverlayView Overlay { get; private set; }
        public RunController Run { get; private set; }

        void Awake()
        {
            if (Arena == null) Arena = FindAnyObjectByType<ArenaView>();
            Arena.AutoRestart = false;
            Arena.Paused = true;

            Flow = new MatchFlow();
            Menu = MenuView.Create(transform, Arena.Art);
            Overlay = OverlayView.Create(transform, Arena.Art);
            Run = RunController.Create(transform, Arena, FileSaveStore.Default());

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
                Menu.Show(true);
            };

            Menu.StartRequested += (a, b) =>
            {
                Arena.ArenaId = Menu.ArenaId;
                Flow.Begin(a, b, NewSeed());
            };
            Menu.RunRequested += () =>
            {
                if (Flow.Current != MatchFlow.State.Menu) return;
                Menu.Show(false);
                Run.Open();
            };
            Run.Closed += () => Flow.ToMenu();
            Overlay.RematchRequested += () => Flow.Rematch();
            Overlay.NewMatchRequested += () => Flow.NewMatch(NewSeed());
            Overlay.MenuRequested += () => Flow.ToMenu();
        }

        void OnEnable()
        {
            if (Arena != null) Arena.MatchEnded += OnRoundEnded;
            if (Arena != null) Arena.SimEventRaised += OnVersusEvent;
        }

        void OnDisable()
        {
            if (Arena != null) Arena.MatchEnded -= OnRoundEnded;
            if (Arena != null) Arena.SimEventRaised -= OnVersusEvent;
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
                Debug.Log(FightLog.Ended("Versus round " + Flow.Series.DisplayRound, Arena.Sim, versusStats));
            Flow.OnRoundEnded(winner);
        }

        void Update()
        {
            // Esc on desktop = Back on Android: leave the match (or Run mode, which is saved) for the menu.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (Run.IsOpen) Run.Flow.Close();
                else if (Flow.Current != MatchFlow.State.Menu) Flow.ToMenu();
            }

            var dt = Time.deltaTime;
            Flow.Tick(dt);
            Overlay.Render(Flow, dt);
        }

        /// <summary>Match seeds are random per new match; shown on screen so a fight can be replayed.</summary>
        static uint NewSeed() => (uint)Random.Range(1, int.MaxValue);
    }
}
