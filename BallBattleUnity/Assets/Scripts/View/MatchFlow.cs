using System;
using BallBattle.Sim;

namespace BallBattle.View
{
    /// <summary>
    /// Screen flow: Menu → Countdown → Playing → RoundOver → (Countdown … ) → Result.
    /// Plain C# (no MonoBehaviour) so it is unit-testable; GameController drives it with Tick(dt) and
    /// reacts to RoundPrepared / RoundBegan. Uses one clock: the dt passed to Tick.
    /// </summary>
    public sealed class MatchFlow
    {
        public enum State { Menu, Countdown, Playing, RoundOver, Result }

        public readonly float CountdownSeconds;
        /// <summary>Pause after a knockout before the next round (≥ the 0.5 s knockout slow-mo, so it is never cut).</summary>
        public readonly float RoundOverSeconds;

        public State Current { get; private set; } = State.Menu;
        public Series Series { get; private set; }
        public int LastRoundWinner { get; private set; } = -1;

        /// <summary>
        /// Debug (-bbForceDraw, BB-SFX2): the first round to resolve is reported as a draw and ends the whole match as a draw
        /// (the Result screen follows the normal pause), so the draw flow can be tested. Off by default; View only (the
        /// match itself plays as usual, only its result is replaced). Run mode has its own flow and never reads it.
        /// </summary>
        public bool ForceDraw { get; set; }

        /// <summary>The match ended as a draw (only with <see cref="ForceDraw"/>; a normal drawn round is replayed).</summary>
        public bool EndedInDraw { get; private set; }

        /// <summary>The winner the rest of the View should use for a round that ended with <paramref name="winner"/>.</summary>
        public int ReportedWinner(int winner) => ForceDraw ? -1 : winner;
        float timer;

        /// <summary>A round must be set up (weapon A, weapon B, round seed) and shown frozen during the countdown.</summary>
        public event Action<string, string, uint> RoundPrepared;
        /// <summary>Countdown finished: unfreeze the arena.</summary>
        public event Action RoundBegan;
        /// <summary>Entered the Result screen.</summary>
        public event Action SeriesFinished;
        /// <summary>Returned to the menu.</summary>
        public event Action MenuShown;

        public MatchFlow(float countdownSeconds = 3f, float roundOverSeconds = 2f)
        {
            CountdownSeconds = countdownSeconds;
            RoundOverSeconds = roundOverSeconds;
        }

        /// <summary>Seconds left on the current timed state (countdown or round-over pause).</summary>
        public float TimeLeft => timer;

        /// <summary>3, 2, 1 during the countdown; 0 otherwise.</summary>
        public int CountdownNumber => Current == State.Countdown ? Math.Max(1, (int)Math.Ceiling(timer)) : 0;

        /// <summary>Start a series from the menu. Ignored in any other state (stale clicks cannot restart a running series).</summary>
        public void Begin(string weaponA, string weaponB, uint matchSeed)
        {
            if (Current != State.Menu) return;
            StartSeries(weaponA, weaponB, matchSeed);
        }

        void StartSeries(string weaponA, string weaponB, uint matchSeed)
        {
            Series = new Series(weaponA, weaponB, matchSeed);
            LastRoundWinner = -1;
            EndedInDraw = false;
            PrepareRound();
        }

        /// <summary>Same weapons, same match seed: the series replays exactly.</summary>
        public void Rematch()
        {
            if (Current != State.Result || Series == null) return;
            StartSeries(Series.WeaponA, Series.WeaponB, Series.MatchSeed);
        }

        public void NewMatch(uint matchSeed)
        {
            if (Current != State.Result || Series == null) return;
            StartSeries(Series.WeaponA, Series.WeaponB, matchSeed);
        }

        /// <summary>Back to the menu from any state (result MENU button, Esc / Android back).</summary>
        public void ToMenu()
        {
            Current = State.Menu;
            MenuShown?.Invoke();
        }

        /// <summary>Called when the arena reports the current round's end (winner index or -1 for a draw).</summary>
        public void OnRoundEnded(int winner)
        {
            if (Current != State.Playing) return;
            winner = ReportedWinner(winner);
            LastRoundWinner = winner;
            Series.ReportRound(winner);
            if (ForceDraw) EndedInDraw = true;
            Current = State.RoundOver;
            timer = RoundOverSeconds;
        }

        public void Tick(float dt)
        {
            switch (Current)
            {
                case State.Countdown:
                    timer -= dt;
                    if (timer <= 0f)
                    {
                        Current = State.Playing;
                        RoundBegan?.Invoke();
                    }
                    break;
                case State.RoundOver:
                    timer -= dt;
                    if (timer > 0f) break;
                    if (Series.IsOver || EndedInDraw)
                    {
                        Current = State.Result;
                        SeriesFinished?.Invoke();
                    }
                    else PrepareRound();
                    break;
            }
        }

        void PrepareRound()
        {
            Current = State.Countdown;
            timer = CountdownSeconds;
            RoundPrepared?.Invoke(Series.WeaponA, Series.WeaponB, Series.CurrentRoundSeed);
        }
    }
}
