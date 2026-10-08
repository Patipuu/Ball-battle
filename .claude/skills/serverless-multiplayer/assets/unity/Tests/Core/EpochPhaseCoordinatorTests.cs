using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Invariant #5 unit tests: MATCH_EPOCH / LOAD_EPOCH / ROOM_PHASE / CLIENT_PHASE
    /// choreography and the match-loaded quorum, driven against the attribute-backed
    /// <see cref="FakeLobbyService"/>. The fake completes synchronously so async writes are
    /// drained with GetAwaiter().GetResult().
    /// </summary>
    public class EpochPhaseCoordinatorTests
    {
        private static readonly PlayerId Host = new PlayerId("host-0001");

        private static PlayerId P(string s) => new PlayerId(s);
        private static void SeedUid(FakeLobbyService f, string uid) => f.SetMemberAttr(P(uid), LobbyKeys.UserId, uid);

        // Seed this match's former-members ledger (room attr) with the admitted participant PUIDs.
        private static void SeedLedger(FakeLobbyService f, int epoch, params string[] puids) =>
            f.SetAttributesAsync(Host, new[] { new System.Collections.Generic.KeyValuePair<string, string>(
                LobbyKeys.FormerMembers, FormerMembers.Serialize(epoch, puids)) }).GetAwaiter().GetResult();

        [Test]
        public void BeginMatch_starts_room_and_bumps_epoch_monotonically()
        {
            var lobby = new FakeLobbyService(Host);
            var epoch = new EpochPhaseCoordinator(lobby);

            Assert.IsFalse(epoch.IsMatchStartedAuthoritative(), "no match before begin");
            Assert.AreEqual(0, epoch.GetCurrentMatchEpoch());

            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            Assert.AreEqual(1, epoch.GetCurrentMatchEpoch());
            Assert.IsTrue(epoch.IsMatchStartedAuthoritative(), "started + IN_MATCH after begin");

            // A second match must advance the epoch, never reuse it.
            epoch.ReturnToLobbyAsync(Host).GetAwaiter().GetResult();
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            Assert.AreEqual(2, epoch.GetCurrentMatchEpoch(), "epoch is monotonic across matches");
        }

        [Test]
        public void ReturnToLobby_clears_started_but_keeps_epoch()
        {
            var lobby = new FakeLobbyService(Host);
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();

            epoch.ReturnToLobbyAsync(Host).GetAwaiter().GetResult();

            Assert.IsFalse(epoch.IsMatchStartedAuthoritative(), "room is back in lobby phase");
            Assert.AreEqual(1, epoch.GetCurrentMatchEpoch(), "epoch stays monotonic (not reset)");
        }

        [Test]
        public void Quorum_false_until_local_marks_loaded_for_current_epoch()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            var epoch = new EpochPhaseCoordinator(lobby);

            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "host LOAD_EPOCH reset to 0 by begin");

            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            Assert.IsTrue(epoch.AreAllMembersMatchLoaded(), "host has now loaded the current epoch");
        }

        [Test]
        public void Quorum_requires_every_member_to_load_current_epoch()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();

            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "peer-b has not loaded yet");

            lobby.SetMemberAttr(P("peer-b"), LobbyKeys.LoadEpoch, "1"); // b's ACK replicates
            Assert.IsTrue(epoch.AreAllMembersMatchLoaded());
        }

        [Test]
        public void Quorum_ignores_members_whose_uid_has_not_replicated()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            lobby.RegisterMember(P("late-joiner")); // present but no uid attr yet
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();

            Assert.IsTrue(epoch.AreAllMembersMatchLoaded(),
                "a not-yet-replicated member does not block the quorum (roster waiter settles it)");
        }

        [Test]
        public void Quorum_false_when_no_active_match()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            lobby.SetMemberAttr(Host, LobbyKeys.LoadEpoch, "0");
            var epoch = new EpochPhaseCoordinator(lobby);

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "no epoch (0) => never a quorum");
        }

        [Test]
        public void MarkLocalLoaded_tracks_the_current_epoch_after_a_rematch()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            var epoch = new EpochPhaseCoordinator(lobby);

            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();      // epoch 1
            epoch.ReturnToLobbyAsync(Host).GetAwaiter().GetResult();
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();      // epoch 2
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();

            Assert.IsTrue(epoch.AreAllMembersMatchLoaded(), "local ACK is for epoch 2, matching current");
        }

        // ---- former-members ledger scoping (rejoin DoS fix) ----------------------------

        [Test]
        public void Quorum_ignores_a_member_outside_the_former_members_ledger()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();

            // A stranger joins mid-match: uid replicated, LoadEpoch=0, NOT an admitted participant.
            SeedUid(lobby, "stranger");
            lobby.SetMemberAttr(P("stranger"), LobbyKeys.LoadEpoch, "0");
            SeedLedger(lobby, 1, Host.Value); // ledger admits only the host

            Assert.IsTrue(epoch.AreAllMembersMatchLoaded(),
                "a member outside the former-members ledger cannot hold the load barrier (DoS fix)");
        }

        [Test]
        public void Quorum_ignores_a_ledger_stamped_for_a_previous_match()
        {
            // A ledger left over from match 1 describes the wrong roster. It must read as absent (fall back to
            // counting every member) rather than govern match 2 — otherwise a real match-2 participant is
            // filtered out of the barrier they must satisfy.
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();  // epoch 1
            epoch.ReturnToLobbyAsync(Host).GetAwaiter().GetResult();
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();  // epoch 2
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value); // STALE: stamped for match 1, admits only the host

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(),
                "a stale ledger must not admit-away peer-b; the barrier falls back and still waits for it");

            lobby.SetMemberAttr(P("peer-b"), LobbyKeys.LoadEpoch, "2");
            Assert.IsTrue(epoch.AreAllMembersMatchLoaded());
        }

        [Test]
        public void Quorum_still_waits_for_an_admitted_member_that_has_not_loaded()
        {
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value, "peer-b"); // both are admitted participants

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "peer-b is admitted and still loading");
            lobby.SetMemberAttr(P("peer-b"), LobbyKeys.LoadEpoch, "1");
            Assert.IsTrue(epoch.AreAllMembersMatchLoaded());
        }

        // ---- started-read without a coordinator (rejoin routing) -------------------------

        [Test]
        public void IsMatchStarted_answers_from_the_lobby_alone()
        {
            // A peer deciding where a join lands has no coordinator yet — the session builds that
            // several awaits into its own join handling. The answer must come from the room's
            // replicated attributes, which are readable the moment the join lands.
            var lobby = new FakeLobbyService(Host);
            Assert.IsFalse(EpochPhaseCoordinator.IsMatchStarted(lobby), "a lobby-phase room is not started");

            new EpochPhaseCoordinator(lobby).BeginMatchAsync(Host).GetAwaiter().GetResult();

            Assert.IsTrue(EpochPhaseCoordinator.IsMatchStarted(lobby),
                "a running match reads as started with no coordinator involved — this is what routes a rejoin "
                + "into the match instead of the lobby");
        }

        [Test]
        public void IsMatchStarted_is_false_for_no_lobby()
        {
            Assert.IsFalse(EpochPhaseCoordinator.IsMatchStarted(null));
        }

        // ---- barrier scoped to LIVE participants (ghost former-member fix) ----------------

        [Test]
        public void Quorum_ignores_an_admitted_member_with_no_live_connection()
        {
            // peer-b was admitted at start, then killed the app. EOS lobby membership outlives the
            // FishNet link, so peer-b lingers as a member with LoadEpoch=0 and would hold the load
            // barrier false for the whole party. Only LIVE admitted participants may hold it.
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby, isParticipantLive: puid => puid == Host.Value);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value, "peer-b");
            lobby.SetMemberAttr(P("peer-b"), LobbyKeys.LoadEpoch, "0"); // never loaded — it is gone

            Assert.IsTrue(epoch.AreAllMembersMatchLoaded(),
                "a ghost former-member with no live connection cannot hold the load barrier");
        }

        [Test]
        public void Quorum_still_waits_for_an_admitted_member_that_is_live()
        {
            // The liveness filter must not swallow a real, connected peer that is merely slow to load.
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby, isParticipantLive: _ => true);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value, "peer-b");

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "peer-b is live, admitted and still loading");
            lobby.SetMemberAttr(P("peer-b"), LobbyKeys.LoadEpoch, "1");
            Assert.IsTrue(epoch.AreAllMembersMatchLoaded());
        }

        [Test]
        public void Quorum_without_a_liveness_source_keeps_the_ledger_only_behaviour()
        {
            // Non-host peers have no server connection list to answer liveness from, so they pass no
            // predicate. That must read as "cannot tell" and leave the pre-existing behaviour intact,
            // never as "nobody is live" (which would pass the barrier on an empty count).
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            SeedUid(lobby, "peer-b");
            var epoch = new EpochPhaseCoordinator(lobby); // no liveness predicate
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value, "peer-b");

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "no predicate => still waits for peer-b");
        }

        [Test]
        public void Quorum_is_false_when_no_admitted_member_is_live()
        {
            // Degenerate case: a liveness blip reports nobody live. The barrier must WAIT (fail closed),
            // never report a quorum it never counted.
            var lobby = new FakeLobbyService(Host);
            SeedUid(lobby, Host.Value);
            var epoch = new EpochPhaseCoordinator(lobby, isParticipantLive: _ => false);
            epoch.BeginMatchAsync(Host).GetAwaiter().GetResult();
            epoch.MarkLocalMatchLoadedAsync(Host).GetAwaiter().GetResult();
            SeedLedger(lobby, 1, Host.Value);

            Assert.IsFalse(epoch.AreAllMembersMatchLoaded(), "no live member counted => no quorum");
        }
    }
}
