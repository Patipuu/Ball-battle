using FishNet.Managing;
using Transport = global::FishNet.Transporting.Transport;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// Resolves a FishNet clientId to the peer's EOS PUID through the correct transport.
    ///
    /// With Multipass in the scene, server clientIds live in the TOP-LEVEL transport's id space —
    /// Multipass renumbers its sub-transports' connections and only it can translate an id back to the
    /// owning sub-transport. Asking FishyEOS directly with a Multipass id resolves the wrong connection:
    /// FishyEOS's own counter survives the process while its per-session map does not, so the two spaces
    /// only coincide in the FIRST hosting session; afterwards the PUID reads empty, the load barrier
    /// never opens, and the joining player is never spawned. Hard rule: every per-connection query
    /// (address, disconnect, stats) goes through <see cref="Resolve"/>, never straight to FishyEOS.
    /// </summary>
    public static class AddressTransport
    {
        /// <summary>
        /// The transport whose id space clientIds live in: <c>TransportManager.Transport</c> (the
        /// Multipass instance when present), else <paramref name="fallback"/> (a single FishyEOS transport).
        /// </summary>
        public static Transport Resolve(NetworkManager networkManager, Transport fallback = null)
            => networkManager != null
               && networkManager.TransportManager != null
               && networkManager.TransportManager.Transport != null
                ? networkManager.TransportManager.Transport
                : fallback;

        /// <summary>The remote PUID of <paramref name="clientId"/>, or "" when it cannot be resolved.</summary>
        public static string GetConnectionAddress(NetworkManager networkManager, int clientId, Transport fallback = null)
        {
            Transport transport = Resolve(networkManager, fallback);
            return transport != null ? transport.GetConnectionAddress(clientId) ?? "" : "";
        }

        /// <summary>
        /// Resolve a connection to its stable EOS participant identity. The host's own local client has
        /// no remote address, so it resolves to <paramref name="localProductUserId"/> (pass the EOS local
        /// PUID string).
        /// </summary>
        public static bool TryGetClientPuid(NetworkManager networkManager, int clientId,
            string localProductUserId, out string puid, Transport fallback = null)
        {
            var local = networkManager != null ? networkManager.ClientManager.Connection : null;
            if (networkManager != null && networkManager.ServerManager.Started
                && local != null && local.ClientId == clientId)
                puid = localProductUserId ?? "";
            else
                puid = GetConnectionAddress(networkManager, clientId, fallback);
            return !string.IsNullOrEmpty(puid);
        }
    }
}
