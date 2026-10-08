using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// The host-side rejoin gate: may the peer on a freshly-Started FishNet connection stay in a
    /// running match? Pure decision over the replicated former-members ledger, so it is testable
    /// without FishNet or EOS — the orchestrator only supplies the PUID and the started flag.
    ///
    /// The gate is defense-in-depth, NOT the primary control (the room is Joinviapresence, so a
    /// stranger cannot find it, and the barriers already ignore non-participants). That asymmetry
    /// drives the fail-open cases below: kicking a real player is worse than admitting a peer who
    /// already holds the room id.
    /// </summary>
    public class RejoinAdmissionTests
    {
        private const int Epoch = 3;

        private static string Ledger(params string[] puids) => FormerMembers.Serialize(Epoch, puids);

        // Most cases are outside the start grace window — the interesting gate decisions are the ones
        // made against the ledger, long after the match began.
        private static AdmissionVerdict Decide(
            string puid, string ledger, bool isMatchStarted, bool isHostConnection, bool isWithinStartGrace = false)
            => RejoinAdmission.Decide(puid, ledger, Epoch, isMatchStarted, isHostConnection, isWithinStartGrace);

        [Test]
        public void Lobby_phase_admits_everyone()
        {
            // Before a match starts the room is advertised and anyone may join — that IS the lobby.
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-stranger", Ledger("puid-a"), isMatchStarted: false, isHostConnection: false));
        }

        [Test]
        public void Start_grace_admits_a_peer_the_snapshot_may_have_missed()
        {
            // A player who joined the room just before the host pressed start can finish its FishNet
            // handshake a moment AFTER the match started. Whether the host's EOS member list had
            // replicated it in time for the start-time snapshot is a race, so during the grace window
            // the ledger is not yet trustworthy evidence of absence — admit (and append) instead of
            // kicking a real player. A stranger with the room id arrives far later than this.
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-late-lobby-joiner", Ledger("puid-a"),
                isMatchStarted: true, isHostConnection: false, isWithinStartGrace: true));
        }

        [Test]
        public void After_the_grace_window_the_ledger_is_authoritative_again()
        {
            Assert.AreEqual(AdmissionVerdict.Reject, Decide(
                "puid-late-lobby-joiner", Ledger("puid-a"),
                isMatchStarted: true, isHostConnection: false, isWithinStartGrace: false));
        }

        [Test]
        public void Started_match_admits_a_puid_in_this_epochs_ledger()
        {
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-a", Ledger("puid-a", "puid-b"), isMatchStarted: true, isHostConnection: false));
        }

        [Test]
        public void Started_match_rejects_a_puid_outside_the_ledger()
        {
            Assert.AreEqual(AdmissionVerdict.Reject, Decide(
                "puid-stranger", Ledger("puid-a", "puid-b"), isMatchStarted: true, isHostConnection: false));
        }

        [Test]
        public void Host_connection_is_never_gated()
        {
            // The host's own local client is not a remote PUID. Gating it would make the host kick
            // itself out of its own match the moment the ledger write lagged.
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-host", Ledger("puid-a"), isMatchStarted: true, isHostConnection: true));
        }

        [Test]
        public void Unresolved_puid_is_unknown_not_a_rejection()
        {
            // The transport has not surfaced the connection address yet. Answering Reject here would
            // kick a legitimate rejoiner on a timing race; the caller retries a bounded number of
            // ticks and only then treats Unknown as a reject.
            Assert.AreEqual(AdmissionVerdict.Unknown, Decide(
                null, Ledger("puid-a"), isMatchStarted: true, isHostConnection: false));
            Assert.AreEqual(AdmissionVerdict.Unknown, Decide(
                "", Ledger("puid-a"), isMatchStarted: true, isHostConnection: false));
        }

        [Test]
        public void Absent_ledger_fails_open()
        {
            // No ledger => no basis to gate on (the start-time write was rejected and logged loudly).
            // Fail open: the barriers already fall back to counting every member, and the room is
            // still unsearchable. Kicking every peer because a write failed is the worse outcome.
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-a", null, isMatchStarted: true, isHostConnection: false));
            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-a", "not-a-ledger", isMatchStarted: true, isHostConnection: false));
        }

        [Test]
        public void Ledger_from_a_previous_match_fails_open()
        {
            // A stale stamp describes the wrong roster — same fallback the barriers take, rather than
            // gating this match's players against last match's list.
            string stale = FormerMembers.Serialize(Epoch - 1, new[] { "puid-old" });

            Assert.AreEqual(AdmissionVerdict.Allow, Decide(
                "puid-a", stale, isMatchStarted: true, isHostConnection: false));
        }
    }
}
