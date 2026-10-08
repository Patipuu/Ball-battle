using System.Collections.Generic;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>What the host should do with a peer that just connected.</summary>
    public enum AdmissionVerdict
    {
        /// <summary>Keep the connection.</summary>
        Allow,

        /// <summary>Not a participant of this match — disconnect it.</summary>
        Reject,

        /// <summary>
        /// Undecidable right now (the peer's PUID has not surfaced yet). The caller must retry on a
        /// later tick rather than convert this to a reject — a legitimate rejoiner reads as Unknown
        /// for the first few ticks of its own handshake.
        /// </summary>
        Unknown,
    }

    /// <summary>
    /// The host-side rejoin gate: may this peer stay in a running match? Defense-in-depth only — the
    /// primary controls are that a started room is <see cref="RoomVisibility.HiddenJoinableById"/>
    /// (so a stranger cannot find it) and that the match barriers already ignore non-participants
    /// (so a stranger who does get in cannot wedge them).
    ///
    /// Being secondary is what sets the failure direction: every uncertainty resolves toward
    /// <see cref="AdmissionVerdict.Allow"/>. Kicking a real player out of a live match is a worse,
    /// louder failure than tolerating a peer who already holds the room id.
    /// </summary>
    public static class RejoinAdmission
    {
        /// <param name="puid">The connecting peer's product user id; null/empty until the transport resolves it.</param>
        /// <param name="formerMembersLedger">Raw <see cref="LobbyKeys.FormerMembers"/> room attribute value.</param>
        /// <param name="currentEpoch">The current MATCH_EPOCH, to reject a ledger left by an earlier match.</param>
        /// <param name="isMatchStarted">Authoritative started flag (<see cref="EpochPhaseCoordinator.IsMatchStartedAuthoritative"/>).</param>
        /// <param name="isHostConnection">Is this the host's own local client?</param>
        /// <param name="isWithinStartGrace">Did the match start just now (see the grace case below)?</param>
        public static AdmissionVerdict Decide(
            string puid,
            string formerMembersLedger,
            int currentEpoch,
            bool isMatchStarted,
            bool isHostConnection,
            bool isWithinStartGrace)
        {
            // The lobby phase is open by definition — that is how players arrive at all.
            if (!isMatchStarted)
                return AdmissionVerdict.Allow;

            // The host's own local client is not a remote peer and is never in the remote ledger path.
            if (isHostConnection)
                return AdmissionVerdict.Allow;

            if (string.IsNullOrEmpty(puid))
                return AdmissionVerdict.Unknown;

            // The match started moments ago. A player who joined the room just before the host pressed
            // start can still be finishing its FishNet handshake, and whether the host's EOS member
            // list had replicated it in time for the start-time snapshot is a race we do not control.
            // So inside this window the ledger is not trustworthy evidence of ABSENCE — admit, and let
            // the caller append. The peer this gate exists to stop (someone who holds the room id and
            // dials in mid-match) arrives far outside the window, so nothing is really let through.
            if (isWithinStartGrace)
                return AdmissionVerdict.Allow;

            // No ledger, a malformed one, or one stamped for a previous match: there is no roster to
            // gate against. Fall back to open, exactly as the barriers fall back to counting every
            // member — gating this match's players against last match's list would kick real players.
            if (!FormerMembers.TryParse(formerMembersLedger, out int ledgerEpoch, out HashSet<string> admitted)
                || ledgerEpoch != currentEpoch)
                return AdmissionVerdict.Allow;

            return admitted.Contains(puid) ? AdmissionVerdict.Allow : AdmissionVerdict.Reject;
        }
    }
}
