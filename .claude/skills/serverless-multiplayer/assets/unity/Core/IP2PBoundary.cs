using System;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Tri-state outcome of a liveness probe. <see cref="Unavailable"/> means "no probe could be asked"
    /// (e.g. the probe object only exists in gameplay scenes) — that is NOT evidence of death.
    /// </summary>
    public enum P2PProbeResult
    {
        Alive,
        Dead,
        Unavailable,
    }

    /// <summary>
    /// Abstraction over the FishyEOS-backed P2P transport for the reuse / liveness
    /// decisions. Lets AppLifecycleRecovery and the persistent-peer reuse path be
    /// exercised against a fake instead of a live socket + device.
    /// </summary>
    public interface IP2PBoundary
    {
        /// <summary>
        /// Active round-trip liveness check over the P2P link — the reuse-liveness
        /// predicate. A connection is reused ONLY on a fresh success here, never on
        /// FishNet's <c>LocalConnectionState.Started</c> alone (which stays Started
        /// on a silently-dead mobile-suspended link with no close callback).
        /// </summary>
        Task<bool> ProbeAliveAsync(PlayerId peer, TimeSpan timeout);

        /// <summary>
        /// Stable ClientId for a PUID so a reconnecting peer keeps its identity and
        /// owned NetworkObjects across a rejoin (Godot persistent-peer intent).
        /// Returns the existing id if the PUID is already mapped, otherwise assigns
        /// and remembers a new one.
        /// </summary>
        int StableClientIdFor(PlayerId peer);

        /// <summary>
        /// The host this transport currently has a client link to (default
        /// <c>!IsValid</c> when hosting or not connected). Mirrors Godot's
        /// <c>_connected_gameplay_owner_id</c> so client reuse can require the link to be
        /// to the SAME host before reusing it (<c>join_game</c>, NetworkManager.gd:1174).
        /// </summary>
        PlayerId ConnectedHost { get; }

        /// <summary>
        /// Whether the transport peer is up (FishNet <c>LocalConnectionState.Started</c>).
        /// NOTE: Started does NOT mean the link is alive — a silently-dead mobile-suspended
        /// link stays Started. It is a NECESSARY but not sufficient reuse condition; the
        /// active <see cref="ProbeAliveAsync"/> is what confirms liveness.
        /// </summary>
        bool IsStarted { get; }
    }
}
