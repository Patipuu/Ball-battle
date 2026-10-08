namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// What the load barrier needs from the session owner. Implemented once by the game's session
    /// object and installed in <see cref="MatchLoadGate.Provider"/>; both
    /// <see cref="MatchLoadObserverCondition"/> and <see cref="PlayerSpawnServiceTemplate"/> read it, so
    /// the visibility gate and the spawn gate can never disagree.
    /// </summary>
    public interface IMatchLoadGate
    {
        /// <summary>
        /// True while a multiplayer room match is running and the barrier must apply. False for solo /
        /// singleplayer / directly-loaded dev scenes, where there is no slower peer to protect.
        /// Key it on lobby MEMBERSHIP (the room has a lobby / ledger), NEVER on member count: a poll that
        /// runs before the peer's attributes replicate sees "1 member", reads the host as solo, hands it
        /// extra characters and places the partner on the host's side.
        /// </summary>
        bool IsBarrierActive { get; }

        /// <summary>
        /// Has this FishNet client reported loading the CURRENT match epoch (its replicated LOAD_EPOCH
        /// equals MATCH_EPOCH)? Resolve the clientId to a PUID with <see cref="AddressTransport"/>.
        /// This is called per networked object x per connection on every observer tick: read a
        /// snapshot cached once per frame, never call <c>ILobbyService.GetMembers()</c> here.
        /// </summary>
        bool HasClientLoadedCurrentMatch(int clientId);
    }

    /// <summary>Process-wide holder of the installed <see cref="IMatchLoadGate"/> (null = no session system).</summary>
    public static class MatchLoadGate
    {
        public static IMatchLoadGate Provider { get; set; }
    }
}
