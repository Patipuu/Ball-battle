using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Unit tests for the app-resume recovery routing (invariant #4, Godot
    /// <c>_recover_gameplay_link_after_resume</c>). The load-bearing guarantees: the liveness
    /// probe runs AFTER the settle window and BEFORE the reuse/rebuild decision, and a dead
    /// link routes to a client re-dial or a host return-to-lobby (never a silent reuse). The
    /// host branch is device-hard on the current rig (host = editor, can't be backgrounded),
    /// so this suite is its only coverage. Async decisions drain with GetAwaiter().GetResult().
    /// </summary>
    public class AppLifecycleRecoveryTests
    {
        private static readonly PlayerId Host = new PlayerId("host-0001");
        private static readonly TimeSpan Settle = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        // Instant no-op settle so routing tests run synchronously.
        private static Task NoDelay(TimeSpan _) => Task.CompletedTask;

        [Test]
        public void ProbeAlive_returns_ReuseAndResync()
        {
            var p2p = new FakeP2PBoundary { ConnectedHost = Host, Alive = true };
            var recovery = new AppLifecycleRecovery(p2p, NoDelay);

            ResumeAction action = recovery
                .OnResumeAsync(isHost: false, Host, Settle, Timeout).GetAwaiter().GetResult();

            Assert.AreEqual(ResumeAction.ReuseAndResync, action);
        }

        [Test]
        public void ProbeAlive_host_returns_ReuseAndResync()
        {
            var p2p = new FakeP2PBoundary { ConnectedHost = Host, Alive = true };
            var recovery = new AppLifecycleRecovery(p2p, NoDelay);

            ResumeAction action = recovery
                .OnResumeAsync(isHost: true, Host, Settle, Timeout).GetAwaiter().GetResult();

            Assert.AreEqual(ResumeAction.ReuseAndResync, action);
        }

        [Test]
        public void ProbeDead_client_returns_ClientRebuild()
        {
            var p2p = new FakeP2PBoundary { ConnectedHost = Host, Alive = false };
            var recovery = new AppLifecycleRecovery(p2p, NoDelay);

            ResumeAction action = recovery
                .OnResumeAsync(isHost: false, Host, Settle, Timeout).GetAwaiter().GetResult();

            Assert.AreEqual(ResumeAction.ClientRebuild, action);
        }

        [Test]
        public void ProbeDead_host_returns_HostReturnToLobby()
        {
            // The rig can't background the editor host, so this branch is unit-only coverage.
            var p2p = new FakeP2PBoundary { ConnectedHost = Host, Alive = false };
            var recovery = new AppLifecycleRecovery(p2p, NoDelay);

            ResumeAction action = recovery
                .OnResumeAsync(isHost: true, Host, Settle, Timeout).GetAwaiter().GetResult();

            Assert.AreEqual(ResumeAction.HostReturnToLobby, action);
        }

        [Test]
        public void Probe_runs_after_settle()
        {
            // Ordering invariant: recovering on a not-yet-settled link would probe a link EOS
            // hasn't re-reported yet and misfire. Record the sequence; settle must precede probe.
            var order = new List<string>();
            var p2p = new RecordingP2P(order, alive: true);
            var recovery = new AppLifecycleRecovery(
                p2p,
                _ => { order.Add("settle"); return Task.CompletedTask; });

            recovery.OnResumeAsync(isHost: false, Host, Settle, Timeout).GetAwaiter().GetResult();

            Assert.AreEqual(new[] { "settle", "probe" }, order.ToArray());
        }

        /// <summary>Records the probe call into a shared ordered list to assert settle→probe.</summary>
        private sealed class RecordingP2P : IP2PBoundary
        {
            private readonly List<string> _order;
            private readonly bool _alive;

            public RecordingP2P(List<string> order, bool alive)
            {
                _order = order;
                _alive = alive;
            }

            public Task<bool> ProbeAliveAsync(PlayerId peer, TimeSpan timeout)
            {
                _order.Add("probe");
                return Task.FromResult(_alive);
            }

            public int StableClientIdFor(PlayerId peer) => -1;
            public PlayerId ConnectedHost => Host;
            public bool IsStarted => true;
        }
    }
}
