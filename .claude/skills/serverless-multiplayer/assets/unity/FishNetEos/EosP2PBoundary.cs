using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Transporting.FishyEOSPlugin;
using TeamNet.Multiplayer.Core;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// What <see cref="EosP2PBoundary.ProbeAliveAsync"/> reports when there is no probe to ask
    /// (<see cref="P2PProbeResult.Unavailable"/>), because the <see cref="IP2PBoundary"/> contract is a
    /// plain bool.
    /// </summary>
    public enum UnavailableProbePolicy
    {
        /// <summary>
        /// Legacy mapping: no probe = "no evidence of death" = alive. Right when probes only exist in
        /// gameplay scenes and menus/lobby ride EOS attributes rather than the P2P link. This is the
        /// mapping the original game shipped, so it is the default.
        /// </summary>
        TreatAsAlive,

        /// <summary>Strict mapping: no probe = cannot prove the link, so treat as dead (rebuild).</summary>
        TreatAsDead,
    }

    /// <summary>
    /// Production <see cref="IP2PBoundary"/> over FishNet + FishyEOS, feeding the persistent-peer reuse
    /// decision (<see cref="PersistentPeerReuse"/>). The liveness check is the active
    /// <see cref="ConnectionProbe"/> round-trip (never FishNet's <c>Started</c> alone); the stable
    /// ClientId is read from the server's live connections by PUID — the FishyEOS <c>ServerPeer</c>
    /// keeps that id stable across a reconnect, so a rejoining peer keeps its owned objects.
    /// </summary>
    public sealed class EosP2PBoundary : IP2PBoundary
    {
        private readonly NetworkManager _networkManager;
        private readonly FishyEOS _transport;
        private readonly UnavailableProbePolicy _unavailablePolicy;

        public EosP2PBoundary(NetworkManager networkManager, FishyEOS transport,
            UnavailableProbePolicy unavailablePolicy = UnavailableProbePolicy.TreatAsAlive)
        {
            _networkManager = networkManager ?? throw new ArgumentNullException(nameof(networkManager));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _unavailablePolicy = unavailablePolicy;
        }

        public bool IsStarted =>
            _networkManager.ClientManager.Started || _networkManager.ServerManager.Started;

        public PlayerId ConnectedHost => new PlayerId(_transport.RemoteProductUserId);

        /// <summary>
        /// Tri-state liveness of the link to the host. <see cref="P2PProbeResult.Unavailable"/> when no
        /// <see cref="ConnectionProbe"/> exists on this peer — it is a SCENE object living only in
        /// gameplay scenes, so a peer in Boot/MainMenu/Lobby/Results has nothing to ask. That is not
        /// evidence of death, and callers must not act on it as such: the original game's reuse tick tore
        /// down a healthy link every 4 s for the whole lobby phase (and through the rejoin window, where
        /// the rejoiner waits on the menu for the match scene) because "no probe" was read as dead.
        /// </summary>
        public async Task<P2PProbeResult> ProbeAsync(TimeSpan timeout)
        {
            ConnectionProbe probe = ConnectionProbe.Instance;
            if (probe == null)
                return P2PProbeResult.Unavailable;
            return await probe.ProbeAsync(timeout) ? P2PProbeResult.Alive : P2PProbeResult.Dead;
        }

        /// <summary>
        /// <see cref="IP2PBoundary"/> contract (bool). Alive/Dead map directly; Unavailable maps per
        /// <see cref="UnavailableProbePolicy"/> — by default alive (legacy: a missing probe is "no
        /// evidence of death"). The only other signal then is <see cref="IsStarted"/>, which the caller
        /// has already checked; it cannot catch a silently-dead suspended link, but entering a gameplay
        /// scene spawns the probe, which will.
        /// </summary>
        public async Task<bool> ProbeAliveAsync(PlayerId peer, TimeSpan timeout)
        {
            switch (await ProbeAsync(timeout))
            {
                case P2PProbeResult.Alive: return true;
                case P2PProbeResult.Dead: return false;
                default: return _unavailablePolicy == UnavailableProbePolicy.TreatAsAlive;
            }
        }

        /// <summary>
        /// The stable FishNet ClientId currently mapped to <paramref name="peer"/> on the server
        /// (matched by the transport's connection address = remote PUID), or -1 when not hosting or the
        /// peer is not connected. Read-only: the id is assigned/kept stable inside <c>ServerPeer</c>.
        /// </summary>
        public int StableClientIdFor(PlayerId peer)
        {
            if (!_networkManager.ServerManager.Started || !peer.IsValid)
                return -1;
            // Server clientIds live in the top-level transport's id space (see AddressTransport).
            global::FishNet.Transporting.Transport addressTransport =
                AddressTransport.Resolve(_networkManager, _transport);
            foreach (KeyValuePair<int, NetworkConnection> kv in _networkManager.ServerManager.Clients)
                if (addressTransport.GetConnectionAddress(kv.Key) == peer.Value)
                    return kv.Key;
            return -1;
        }
    }
}
