using System;
using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Unit tests for the persistent-peer reuse decision (Godot <c>create_server</c>/
    /// <c>join_game</c> reuse guard). The load-bearing case is that a Started-but-dead link
    /// (a silently-dropped mobile-suspended peer) is REBUILT, never reused. Driven by the
    /// <see cref="FakeP2PBoundary"/>; the fake completes synchronously so async decisions are
    /// drained with GetAwaiter().GetResult().
    /// </summary>
    public class PersistentPeerReuseTests
    {
        private static readonly PlayerId Host = new PlayerId("host-0001");
        private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

        // ---- Client reuse --------------------------------------------------------------

        [Test]
        public void Client_reuses_when_started_same_host_and_probe_alive()
        {
            var p2p = new FakeP2PBoundary { IsStarted = true, ConnectedHost = Host, Alive = true };
            var reuse = new PersistentPeerReuse(p2p);

            ReuseDecision d = reuse.DecideClientReuseAsync(Host, Timeout).GetAwaiter().GetResult();

            Assert.IsTrue(d.Reuse, d.Reason);
        }

        [Test]
        public void Client_rebuilds_when_probe_dead_despite_started()
        {
            // The crux: link is Started AND points at the right host, but the active probe
            // fails (silently-dead suspended link). Must NOT reuse.
            var p2p = new FakeP2PBoundary { IsStarted = true, ConnectedHost = Host, Alive = false };
            var reuse = new PersistentPeerReuse(p2p);

            ReuseDecision d = reuse.DecideClientReuseAsync(Host, Timeout).GetAwaiter().GetResult();

            Assert.IsFalse(d.Reuse, "Started-but-dead link must be rebuilt, not reused");
        }

        [Test]
        public void Client_rebuilds_when_connected_to_a_different_host()
        {
            var p2p = new FakeP2PBoundary { IsStarted = true, ConnectedHost = new PlayerId("other-host"), Alive = true };
            var reuse = new PersistentPeerReuse(p2p);

            ReuseDecision d = reuse.DecideClientReuseAsync(Host, Timeout).GetAwaiter().GetResult();

            Assert.IsFalse(d.Reuse, "a link to a different host cannot be reused for this host");
        }

        [Test]
        public void Client_rebuilds_when_transport_not_started()
        {
            var p2p = new FakeP2PBoundary { IsStarted = false, ConnectedHost = Host, Alive = true };
            var reuse = new PersistentPeerReuse(p2p);

            ReuseDecision d = reuse.DecideClientReuseAsync(Host, Timeout).GetAwaiter().GetResult();

            Assert.IsFalse(d.Reuse, "no live peer to reuse when transport is down");
        }

        // ---- Host reuse ----------------------------------------------------------------

        [Test]
        public void Host_reuses_running_server_peer()
        {
            var p2p = new FakeP2PBoundary { IsStarted = true };
            var reuse = new PersistentPeerReuse(p2p);

            Assert.IsTrue(reuse.DecideHostReuse().Reuse,
                "recreating a running server would drop connected clients");
        }

        [Test]
        public void Host_rebuilds_when_no_server_peer()
        {
            var p2p = new FakeP2PBoundary { IsStarted = false };
            var reuse = new PersistentPeerReuse(p2p);

            Assert.IsFalse(reuse.DecideHostReuse().Reuse);
        }
    }
}
