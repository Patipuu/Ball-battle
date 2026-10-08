using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Invariant #5 — MATCH_EPOCH / LOAD_EPOCH / ROOM_PHASE / CLIENT_PHASE choreography over
    /// the replicated lobby attributes (ported from Godot <c>MultiplayerManagerEOS</c>
    /// :1341-1350 start, :1478-1482 match-loaded ACK, :1440-1442 return, and the
    /// <c>NetworkManager</c> :882/:889/:911 authoritative reads). All decisions read/write the
    /// lobby via <see cref="ILobbyService"/>, so this is unit-testable against a fake — no
    /// transport, no Unity.
    ///
    /// Lane split (do NOT collapse): MATCH_EPOCH / ROOM_PHASE / IS_ROOM_STARTED are ROOM-wide
    /// (host-authored); CLIENT_PHASE / LOAD_EPOCH are PER-MEMBER (each client authors its own).
    /// The match-loaded quorum is a lossless barrier — every member's replicated LOAD_EPOCH must
    /// equal the current MATCH_EPOCH — which replaces the dropped-RPC progress aggregation.
    /// </summary>
    public sealed class EpochPhaseCoordinator
    {
        private readonly ILobbyService _lobby;
        private readonly Action<string> _log;
        private readonly Func<string, bool> _isParticipantLive;

        /// <param name="isParticipantLive">
        /// Optional: does this PUID still hold a live transport link? Supplied by the HOST only (it is
        /// the only peer with a server connection list); null on every other peer, which reads as
        /// "cannot tell" and leaves the barrier scoped on ledger membership alone. Both barrier
        /// callers are already host-gated, so nothing else needs it.
        ///
        /// Note it applies on the no-ledger fallback path too, not just when a ledger governs: a ghost
        /// must not hold the barrier just because the start-time ledger write failed.
        /// </param>
        public EpochPhaseCoordinator(
            ILobbyService lobby, Action<string> log = null, Func<string, bool> isParticipantLive = null)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _log = log;
            _isParticipantLive = isParticipantLive;
        }

        /// <summary>
        /// Host: open a new match. Bump MATCH_EPOCH (monotonic), flip the room to IN_MATCH +
        /// started, move the host's own CLIENT_PHASE to IN_MATCH, and reset the host's
        /// LOAD_EPOCH to 0 (not-yet-loaded). Godot MultiplayerManagerEOS.gd:1341-1350.
        /// </summary>
        public Task<bool> BeginMatchAsync(PlayerId host)
        {
            int next = GetCurrentMatchEpoch() + 1;
            _log?.Invoke($"[Epoch] begin match, epoch {next}");
            return WriteAsync(host,
                (LobbyKeys.IsRoomStarted, "1"),
                (LobbyKeys.RoomPhase, LobbyKeys.Phase.InMatch),
                (LobbyKeys.MatchEpoch, next.ToString(CultureInfo.InvariantCulture)),
                (LobbyKeys.ClientPhase, LobbyKeys.Phase.InMatch),
                (LobbyKeys.LoadEpoch, "0"));
        }

        /// <summary>
        /// Any peer: publish "I have loaded the current match" by writing own LOAD_EPOCH to the
        /// current MATCH_EPOCH on the replicated member lane. Godot :1478-1482.
        /// </summary>
        public Task<bool> MarkLocalMatchLoadedAsync(PlayerId localUser)
        {
            int epoch = GetCurrentMatchEpoch();
            _log?.Invoke($"[Epoch] mark local loaded, epoch {epoch}");
            return WriteAsync(localUser,
                (LobbyKeys.LoadEpoch, epoch.ToString(CultureInfo.InvariantCulture)));
        }

        /// <summary>
        /// Host: return the room to the lobby phase — clear started, ROOM_PHASE back to LOBBY,
        /// and move own CLIENT_PHASE back to LOBBY. MATCH_EPOCH is left untouched so it stays
        /// monotonic across matches. Godot :1440-1442.
        /// </summary>
        public Task<bool> ReturnToLobbyAsync(PlayerId host)
        {
            _log?.Invoke("[Epoch] return to lobby");
            return WriteAsync(host,
                (LobbyKeys.IsRoomStarted, "0"),
                (LobbyKeys.RoomPhase, LobbyKeys.Phase.Lobby),
                (LobbyKeys.ClientPhase, LobbyKeys.Phase.Lobby));
        }

        /// <summary>The host-authored match epoch for the current attempt (0 if none). Godot :889.</summary>
        public int GetCurrentMatchEpoch()
        {
            if (_lobby.TryGetRoomAttribute(LobbyKeys.MatchEpoch, out string value)
                && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int epoch))
                return epoch;
            return 0;
        }

        /// <summary>
        /// True when the lobby authoritatively says the match is running: IS_ROOM_STARTED == 1
        /// AND ROOM_PHASE == IN_MATCH. Read off replicated lobby KV, immune to the scene-load
        /// RPC race. Godot NetworkManager.gd:882.
        /// </summary>
        public bool IsMatchStartedAuthoritative() => IsMatchStarted(_lobby);

        /// <summary>
        /// The same authoritative read, without needing a coordinator to exist yet. A peer that has
        /// just joined must decide where to route BEFORE the session has finished building its
        /// per-room components — the room's attributes are readable the moment the join lands, so the
        /// answer must come from the lobby, not from how far along the session's own setup is.
        /// </summary>
        public static bool IsMatchStarted(ILobbyService lobby)
        {
            if (lobby == null)
                return false;
            bool started = lobby.TryGetRoomAttribute(LobbyKeys.IsRoomStarted, out string s) && s == "1";
            bool inMatch = lobby.TryGetRoomAttribute(LobbyKeys.RoomPhase, out string p)
                && p == LobbyKeys.Phase.InMatch;
            return started && inMatch;
        }

        /// <summary>
        /// The match-loaded quorum: every current member (with a replicated uid) has reported
        /// LOAD_EPOCH == the current MATCH_EPOCH. False when there is no active epoch or no
        /// members. Members whose uid has not replicated yet are skipped (not counted as loaded nor as
        /// blocking — the roster waiter #2 settles them; a mid-join member should not block this
        /// cycle's load barrier). This post-match barrier tracks join-time replication, distinct from
        /// the PUID-keyed start quorum. Godot :911-933.
        /// </summary>
        public bool AreAllMembersMatchLoaded()
        {
            int epoch = GetCurrentMatchEpoch();
            if (epoch <= 0)
                return false;

            // When this match's former-members ledger is present, only ADMITTED participants count toward the
            // barrier. A stranger who merely joined the EOS lobby writes LoadEpoch=0 and would otherwise hold the
            // barrier false forever (remote DoS). The ledger must carry THIS epoch's stamp: one left over from a
            // previous match describes the wrong roster, so it is ignored. Absent (or stale, or malformed) ledger
            // falls back to counting every replicated member — the pre-feature behaviour.
            HashSet<string> admitted = null;
            if (_lobby.TryGetRoomAttribute(LobbyKeys.FormerMembers, out string ledger)
                && FormerMembers.TryParse(ledger, out int ledgerEpoch, out HashSet<string> ledgerPuids)
                && ledgerEpoch == epoch)
                admitted = ledgerPuids;

            IReadOnlyList<LobbyMemberSnapshot> members = _lobby.GetMembers();
            bool anyCounted = false;
            foreach (var m in members)
            {
                if (string.IsNullOrEmpty(m.Uid))
                    continue; // uid not replicated yet — a mid-join member does not block this barrier
                if (admitted != null && !admitted.Contains(m.Member.Value))
                    continue; // not an admitted participant — cannot hold the barrier
                if (!IsLive(m.Member.Value))
                    continue; // admitted but gone: a ghost cannot hold the barrier for the party
                if (m.LoadEpoch != epoch)
                    return false;
                anyCounted = true;
            }
            return anyCounted;
        }

        /// <summary>
        /// Does this PUID still hold a live transport link? True when no liveness source was supplied
        /// (a non-host peer cannot tell, and must not shrink the barrier on a guess) — the ledger-only
        /// behaviour. An EOS lobby membership outlives the FishNet link it came with, so without this
        /// a former member who killed the app lingers as a member with LoadEpoch=0 and holds the
        /// barrier false for everyone.
        /// </summary>
        private bool IsLive(string puid) => _isParticipantLive == null || _isParticipantLive(puid);

        private Task<bool> WriteAsync(PlayerId user, params (string key, string value)[] attrs)
        {
            var batch = new List<KeyValuePair<string, string>>(attrs.Length);
            foreach (var (key, value) in attrs)
                batch.Add(new KeyValuePair<string, string>(key, value));
            return _lobby.SetAttributesAsync(user, batch);
        }
    }
}
