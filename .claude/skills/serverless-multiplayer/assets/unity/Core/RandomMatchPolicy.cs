using System;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Pure random-match candidate filter. Godot <c>MultiplayerManagerEOS.can_join_room</c>
    /// (:139-175) rejected rooms that were full, already started, of another mode or of another
    /// build before joining; the Unity search used to join the first lobby in the bucket blindly,
    /// so one full or in-match room anywhere in the shared bucket failed every Random Match with
    /// <c>LobbyTooManyPlayers</c> even when "nobody was online".
    /// </summary>
    public static class RandomMatchPolicy
    {
        public readonly struct Candidate
        {
            public readonly uint AvailableSlots;
            /// <summary>Room <see cref="LobbyKeys.IsRoomStarted"/>; null when not replicated.</summary>
            public readonly string IsRoomStarted;
            /// <summary>Room <see cref="LobbyKeys.Mode"/>; null when the room never set one.</summary>
            public readonly string Mode;
            /// <summary>Room <see cref="LobbyKeys.Bundle"/>; null for rooms from builds predating it.</summary>
            public readonly string Bundle;

            /// <summary>Room <see cref="LobbyKeys.Private"/>; "1" = private. Null for rooms that never wrote it.</summary>
            public readonly string IsPrivate;

            public Candidate(uint availableSlots, string isRoomStarted, string mode, string bundle,
                string isPrivate = null)
            {
                IsPrivate = isPrivate;
                AvailableSlots = availableSlots;
                IsRoomStarted = isRoomStarted;
                Mode = mode;
                Bundle = bundle;
            }
        }

        /// <summary>
        /// Rejects: no free slot, started, mode mismatch (a room with NO mode is not a product
        /// room — gym/harness lobbies share the bucket — so it is rejected too when a mode is
        /// wanted), or a bundle that names a different build. A missing bundle is tolerated so
        /// rooms hosted by a build that predates the attribute stay joinable.
        /// </summary>
        public static bool IsJoinable(in Candidate c, string wantedMode, string wantedBundle, out string reason)
        {
            reason = null;
            if (c.AvailableSlots == 0)
            {
                reason = "full";
                return false;
            }
            if (string.Equals(c.IsRoomStarted, "1", StringComparison.Ordinal))
            {
                reason = "started";
                return false;
            }
            // A private room stays advertised (so code join works) but must never be matched at random.
            if (string.Equals(c.IsPrivate, "1", StringComparison.Ordinal))
            {
                reason = "private";
                return false;
            }
            if (!string.IsNullOrEmpty(wantedMode)
                && !string.Equals(c.Mode, wantedMode, StringComparison.OrdinalIgnoreCase))
            {
                reason = "mode " + (c.Mode ?? "(none)");
                return false;
            }
            if (!string.IsNullOrEmpty(wantedBundle) && !string.IsNullOrEmpty(c.Bundle)
                && !string.Equals(c.Bundle, wantedBundle, StringComparison.Ordinal))
            {
                reason = "bundle " + c.Bundle;
                return false;
            }
            return true;
        }

        /// <summary>
        /// Godot fell back to <c>create_room</c> when no joinable room existed. A failed join is
        /// only worth retrying as a create when it is a matchmaking miss, never an auth /
        /// cancellation / timeout the caller must surface.
        /// </summary>
        public static bool ShouldCreateAfterJoinFailure(string error)
        {
            if (string.IsNullOrEmpty(error))
                return false;
            switch (error)
            {
                case "not-found":
                case "LobbyTooManyPlayers":
                case "LobbyLobbyAlreadyExists":
                case "NotFound":
                    return true;
                default:
                    return false;
            }
        }
    }
}
