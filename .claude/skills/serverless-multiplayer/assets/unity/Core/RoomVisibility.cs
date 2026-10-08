namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// How discoverable a room is. Deliberately NOT Epic's <c>LobbyPermissionLevel</c>: this seam is
    /// SDK-free (fakes and tests reference only the Core assembly), and the
    /// two levels used here carry room-lifecycle meaning that the EOS names do not.
    ///
    /// Measured against live EOS with two devices (260717), which is why <c>Inviteonly</c> is absent:
    ///
    /// | EOS level          | stranger's bucket/code search | non-member JoinById |
    /// |--------------------|-------------------------------|---------------------|
    /// | Publicadvertised   | finds it (leaks roster/PUIDs) | Success             |
    /// | Inviteonly         | no                            | SessionsNotAllowed  |
    /// | Joinviapresence    | no                            | Success             |
    ///
    /// A started room needs exactly the third row — invisible to every search, still joinable by a
    /// former member who holds the persisted room id. <c>Inviteonly</c> blocks that rejoin outright,
    /// so nothing maps to it.
    /// </summary>
    public enum RoomVisibility
    {
        /// <summary>Listed in Random Match and resolvable by join code. The lobby phase.</summary>
        Advertised,

        /// <summary>
        /// Absent from every search, joinable only by whoever already holds the 32-char room id.
        /// A started match, so a stranger can neither land in it nor read its roster.
        /// </summary>
        HiddenJoinableById,
    }
}
