#nullable enable
using System;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Bounds a lobby connect operation that cannot be cancelled.
    ///
    /// <para><b>Why this exists:</b> every <c>EosLobbyService</c> entry point completes a bare
    /// <see cref="TaskCompletionSource{TResult}"/> from an EOS callback. If that callback never fires —
    /// no network, EOS not configured, the platform tick stalled — the returned task never completes
    /// and never faults. There is no exception to catch and no cancellation to observe: the await
    /// simply hangs forever, and with it the UI that is showing "Creating room…".</para>
    ///
    /// <para>Racing the operation against a watchdog task is the only way out. The operation keeps
    /// running (EOS gives us no way to abort it), so a late success must still be observed and cleaned
    /// up — otherwise a lobby is created on the EOS backend that no one is in. That is what
    /// <c>onTimeout</c> is for: the caller receives the still-running task and takes ownership of the
    /// orphan.</para>
    ///
    /// <para>The watchdog is passed in rather than created here so tests can drive it deterministically
    /// instead of sleeping for real seconds.</para>
    /// </summary>
    public static class LobbyConnectWatchdog
    {
        /// <summary><see cref="LobbyResult.Error"/> value returned when the watchdog wins the race.</summary>
        public const string TimeoutError = "timeout";

        /// <summary><see cref="LobbyResult.Error"/> value when the operation itself was cancelled.</summary>
        public const string CancelledError = "cancelled";

        /// <summary>
        /// Races <paramref name="operation"/> against <paramref name="watchdog"/>.
        ///
        /// <para>Returns the operation's own result when it wins. On timeout returns
        /// <c>Fail(<see cref="TimeoutError"/>)</c> and hands the still-running
        /// <paramref name="operation"/> to <paramref name="onTimeout"/> so the caller can orphan-clean
        /// a late success.</para>
        /// </summary>
        /// <param name="operation">The already-started, uncancellable lobby call.</param>
        /// <param name="watchdog">Completes when the operation has taken too long.</param>
        /// <param name="cancelWatchdog">
        /// Invoked when the operation wins, to stop the watchdog timer. Optional.
        /// </param>
        /// <param name="onTimeout">
        /// Invoked with the still-running operation when the watchdog wins. Optional.
        /// </param>
        public static async Task<LobbyResult> RunAsync(
            Task<LobbyResult> operation,
            Task watchdog,
            Action? cancelWatchdog = null,
            Action<Task<LobbyResult>>? onTimeout = null)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            if (watchdog == null) throw new ArgumentNullException(nameof(watchdog));

            await Task.WhenAny(operation, watchdog).ConfigureAwait(true);

            // Prefer a completed operation even when the watchdog finished in the same tick — a real
            // result always beats a timeout we no longer need.
            if (operation.IsCompleted)
            {
                cancelWatchdog?.Invoke();
                try
                {
                    return await operation.ConfigureAwait(true);
                }
                catch (OperationCanceledException)
                {
                    return LobbyResult.Fail(CancelledError);
                }
            }

            onTimeout?.Invoke(operation);
            return LobbyResult.Fail(TimeoutError);
        }
    }
}
