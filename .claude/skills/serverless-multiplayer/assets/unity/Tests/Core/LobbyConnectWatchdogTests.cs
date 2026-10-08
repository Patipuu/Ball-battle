using System;
using System.Threading;
using System.Threading.Tasks;
using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// The bug these pin down: a lobby screen sat on "Creating room" forever with an empty roster,
    /// no error and no retry. <c>EosLobbyService</c> completes a bare <c>TaskCompletionSource</c> from
    /// an EOS callback and nothing else, so when that callback never arrives the await never returns
    /// and never faults — there is no exception to catch and no cancellation to observe. Only
    /// join-by-id was raced against a watchdog; host and join-random awaited EOS bare on the
    /// (incorrect) assumption that a "longer EOS auth budget" bounded them.
    ///
    /// <para><b>Why the watchdog is passed in:</b> <see cref="LobbyConnectWatchdog.RunAsync"/> takes
    /// the timeout as a <see cref="Task"/> rather than a duration, so these tests drive it with a
    /// <see cref="TaskCompletionSource{TResult}"/> they complete by hand. Nothing here sleeps, and
    /// nothing depends on scheduler timing — the "hung EOS call" is modelled exactly as it behaves in
    /// production: a task that is simply never completed.</para>
    /// </summary>
    public class LobbyConnectWatchdogTests
    {
        /// <summary>A call that never comes back — the production failure this whole file is about.</summary>
        private static Task<LobbyResult> NeverCompletes() =>
            new TaskCompletionSource<LobbyResult>().Task;

        private static PlayerId AnyUser => default;

        // ---------- the timeout path ----------

        [Test]
        public async Task RunAsync_WhenOperationNeverCompletes_ReturnsTimeoutInsteadOfHanging()
        {
            var watchdog = new TaskCompletionSource<bool>();
            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(NeverCompletes(), watchdog.Task);

            Assert.IsFalse(race.IsCompleted, "precondition: nothing has fired yet, so the race is open");

            watchdog.SetResult(true);
            LobbyResult result = await race;

            Assert.IsFalse(result.Ok, "a hung EOS call must not report success");
            Assert.AreEqual(LobbyConnectWatchdog.TimeoutError, result.Error,
                "the caller distinguishes timeout from other failures to decide whether to offer Retry");
        }

        [Test]
        public async Task RunAsync_OnTimeout_HandsTheStillRunningOperationToTheCaller()
        {
            var hung = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();
            Task<LobbyResult> handedBack = null;

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(
                hung.Task, watchdog.Task, onTimeout: late => handedBack = late);

            watchdog.SetResult(true);
            await race;

            Assert.AreSame(hung.Task, handedBack,
                "the caller must receive the live task — EOS cannot be aborted, so a create that " +
                "lands late leaves a room on the backend that nobody is in, and only the caller can " +
                "orphan-leave it");
        }

        [Test]
        public async Task RunAsync_OnTimeout_DoesNotCancelTheWatchdog()
        {
            var watchdog = new TaskCompletionSource<bool>();
            bool cancelled = false;

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(
                NeverCompletes(), watchdog.Task, cancelWatchdog: () => cancelled = true);

            watchdog.SetResult(true);
            await race;

            Assert.IsFalse(cancelled, "there is no timer left to stop once the watchdog has already won");
        }

        // ---------- the operation-wins path ----------

        [Test]
        public async Task RunAsync_WhenOperationSucceedsFirst_ReturnsItsResult()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(op.Task, watchdog.Task);
            op.SetResult(LobbyResult.Success("lobby-abc", AnyUser));
            LobbyResult result = await race;

            Assert.IsTrue(result.Ok);
            Assert.AreEqual("lobby-abc", result.LobbyId, "the real result must survive the race intact");
        }

        [Test]
        public async Task RunAsync_WhenOperationFailsFirst_PreservesItsErrorRatherThanReportingTimeout()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(op.Task, watchdog.Task);
            op.SetResult(LobbyResult.Fail("eos-connect-failed"));
            LobbyResult result = await race;

            Assert.IsFalse(result.Ok);
            Assert.AreEqual("eos-connect-failed", result.Error,
                "a genuine EOS error is more actionable than 'timeout' and must not be flattened into it");
        }

        [Test]
        public async Task RunAsync_WhenOperationWins_StopsTheWatchdogTimer()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();
            bool cancelled = false;

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(
                op.Task, watchdog.Task, cancelWatchdog: () => cancelled = true);

            op.SetResult(LobbyResult.Success("lobby-abc", AnyUser));
            await race;

            Assert.IsTrue(cancelled,
                "the pending Task.Delay must be cancelled or every connect leaks a live timer");
        }

        [Test]
        public async Task RunAsync_WhenOperationWins_DoesNotRunTheTimeoutHandler()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();
            bool timedOut = false;

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(
                op.Task, watchdog.Task, onTimeout: _ => timedOut = true);

            op.SetResult(LobbyResult.Success("lobby-abc", AnyUser));
            await race;

            Assert.IsFalse(timedOut,
                "running the timeout handler on success would orphan-leave the lobby we just joined");
        }

        /// <summary>
        /// Both tasks already complete when the race is entered. The operation must win: it carries a
        /// real answer, and treating it as a timeout would orphan-leave a lobby that actually
        /// succeeded.
        /// </summary>
        [Test]
        public async Task RunAsync_WhenBothAlreadyComplete_PrefersTheOperation()
        {
            Task<LobbyResult> op = Task.FromResult(LobbyResult.Success("lobby-abc", AnyUser));
            Task watchdog = Task.CompletedTask;
            bool timedOut = false;

            LobbyResult result = await LobbyConnectWatchdog.RunAsync(
                op, watchdog, onTimeout: _ => timedOut = true);

            Assert.IsTrue(result.Ok, "a completed success must not be discarded as a timeout");
            Assert.AreEqual("lobby-abc", result.LobbyId);
            Assert.IsFalse(timedOut);
        }

        // ---------- cancellation ----------

        [Test]
        public async Task RunAsync_WhenOperationIsCancelled_ReportsCancelledNotTimeout()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(op.Task, watchdog.Task);
            op.SetCanceled();
            LobbyResult result = await race;

            Assert.IsFalse(result.Ok);
            Assert.AreEqual(LobbyConnectWatchdog.CancelledError, result.Error,
                "leaving the lobby screen mid-connect is not a network timeout and must not toast as one");
        }

        [Test]
        public void RunAsync_WhenOperationFaults_LetsTheExceptionThrough()
        {
            var op = new TaskCompletionSource<LobbyResult>();
            var watchdog = new TaskCompletionSource<bool>();

            Task<LobbyResult> race = LobbyConnectWatchdog.RunAsync(op.Task, watchdog.Task);
            op.SetException(new InvalidOperationException("EOS platform not initialised"));

            // the caller already logs and toasts on a throw; swallowing it here
            // would turn a configuration error into a silent generic failure.
            var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await race);
            Assert.AreEqual("EOS platform not initialised", ex.Message);
        }

        // ---------- argument contract ----------

        // ThrowsAsync, not Throws: RunAsync is an async method, so a guard-clause throw is captured
        // into the returned Task rather than raised at the call site. Assert.Throws sees nothing.

        [Test]
        public void RunAsync_WithNullOperation_Throws()
            => Assert.ThrowsAsync<ArgumentNullException>(
                async () => await LobbyConnectWatchdog.RunAsync(null, Task.CompletedTask));

        [Test]
        public void RunAsync_WithNullWatchdog_Throws()
            => Assert.ThrowsAsync<ArgumentNullException>(
                async () => await LobbyConnectWatchdog.RunAsync(NeverCompletes(), null));
    }
}
