using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Invariant #3 unit tests: the host return-quorum barrier and the client-side self-return
    /// fallback, both driven by a fake MONOTONIC clock (a mutable local read through a closure).
    /// Proves commit-on-all-ready, commit-on-timeout, single-shot commit, and that a wedged host
    /// does not wedge the client (fallback fires) — without any real timer or transport.
    /// </summary>
    public class MatchReturnQuorumTests
    {
        // ---- MatchReturnQuorum (host) --------------------------------------------------

        [Test]
        public void Commits_all_ready_once_every_expected_peer_acks()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock, timeoutSeconds: 5.0);
            q.BeginSession(new[] { 1, 2, 3 });

            q.MarkPeerReady(1);
            q.MarkPeerReady(2);
            Assert.IsFalse(q.TryCommit(clock, out _), "not all peers ready yet");

            q.MarkPeerReady(3);
            Assert.IsTrue(q.TryCommit(clock, out string reason));
            Assert.AreEqual("all_ready", reason);
        }

        [Test]
        public void Commits_timeout_when_a_peer_never_acks()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock, timeoutSeconds: 5.0);
            q.BeginSession(new[] { 1, 2 });
            q.MarkPeerReady(1); // peer 2 never ACKs

            clock = 4.9;
            Assert.IsFalse(q.TryCommit(clock, out _), "before the timeout the host keeps waiting");

            clock = 5.0;
            Assert.IsTrue(q.TryCommit(clock, out string reason));
            Assert.AreEqual("timeout", reason, "host-authoritative fallback commits at the timeout");
        }

        [Test]
        public void Commit_is_single_shot()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock, timeoutSeconds: 5.0);
            q.BeginSession(new[] { 1 });
            q.MarkPeerReady(1);

            Assert.IsTrue(q.TryCommit(clock, out _), "first commit fires");
            Assert.IsTrue(q.Committed);
            clock = 100;
            Assert.IsFalse(q.TryCommit(clock, out _), "already committed, never fires again");
        }

        [Test]
        public void MarkPeerReady_reports_new_vs_duplicate_and_needs_a_session()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock);

            Assert.IsFalse(q.MarkPeerReady(1), "no active session yet");

            q.BeginSession(new[] { 1 });
            Assert.IsTrue(q.MarkPeerReady(1), "first ACK is new");
            Assert.IsFalse(q.MarkPeerReady(1), "duplicate ACK is not new");
            Assert.IsFalse(q.MarkPeerReady(0), "invalid peer id ignored");
        }

        [Test]
        public void Session_ids_are_strictly_increasing_across_resets()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock);

            int first = q.BeginSession(new[] { 1 });
            q.Reset();
            int second = q.BeginSession(new[] { 1 });

            Assert.AreEqual(1, first);
            Assert.AreEqual(2, second, "serial keeps increasing so a stale watchdog cannot match");
            Assert.AreEqual(2, q.SessionId, "the new session is the active one");
        }

        [Test]
        public void Empty_expected_set_is_trivially_ready()
        {
            double clock = 0;
            var q = new MatchReturnQuorum(() => clock);
            q.BeginSession(new int[0]);

            Assert.IsTrue(q.AreAllReady());
            Assert.IsTrue(q.TryCommit(clock, out string reason));
            Assert.AreEqual("all_ready", reason);
        }

        // ---- ClientReturnFallback (client) ---------------------------------------------

        [Test]
        public void Client_self_returns_once_after_the_bound()
        {
            double clock = 0;
            var fb = new ClientReturnFallback(() => clock, boundSeconds: 8.0);
            fb.Arm();

            clock = 7.9;
            Assert.IsFalse(fb.ShouldSelfReturn(clock), "within the bound the client keeps waiting for the host");

            clock = 8.0;
            Assert.IsTrue(fb.ShouldSelfReturn(clock), "host return did not arrive -> self return");
            Assert.IsFalse(fb.ShouldSelfReturn(clock), "single-shot");
        }

        [Test]
        public void Client_does_not_self_return_when_host_return_arrives_first()
        {
            double clock = 0;
            var fb = new ClientReturnFallback(() => clock, boundSeconds: 8.0);
            fb.Arm();

            clock = 4.0;
            fb.Disarm(); // host's authoritative return arrived

            clock = 20.0;
            Assert.IsFalse(fb.ShouldSelfReturn(clock), "disarmed: never self-returns");
        }

        [Test]
        public void Arm_is_idempotent()
        {
            double clock = 0;
            var fb = new ClientReturnFallback(() => clock, boundSeconds: 8.0);
            fb.Arm();
            clock = 5.0;
            fb.Arm(); // must not restart the clock

            clock = 8.0;
            Assert.IsTrue(fb.ShouldSelfReturn(clock), "bound measured from the FIRST arm, not the second");
        }
    }
}
