using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FishNet.Connection;
using FishNet.Object;
using UnityEngine;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// Active round-trip liveness probe: a single scene <see cref="NetworkObject"/> present on every
    /// peer. Any client sends a token to the server and awaits the server's targeted ack; a fresh success
    /// proves the P2P link is alive RIGHT NOW — unlike FishNet's <c>Started</c>, which stays true on a
    /// silently-dead mobile-suspended link.
    ///
    /// Modelled as a scene object (not a spawned player prefab) so it needs no spawner or
    /// DefaultPrefabObjects registration — FishNet assigns its scene id automatically. Each peer's local
    /// instance is exposed as <see cref="Instance"/>. Continuations resume on Unity's main-thread
    /// SynchronizationContext, so the pending-token map is single-threaded.
    ///
    /// Because it lives in gameplay scenes only, <see cref="Instance"/> is null in menus and lobby —
    /// see <see cref="EosP2PBoundary"/> for how a missing probe is reported.
    /// </summary>
    public class ConnectionProbe : NetworkBehaviour
    {
        /// <summary>This peer's local instance of the scene probe (null until networked).</summary>
        public static ConnectionProbe Instance { get; private set; }

        private uint _nextToken = 1;
        private readonly Dictionary<uint, TaskCompletionSource<bool>> _pending = new();

        public override void OnStartClient()
        {
            base.OnStartClient();
            Instance = this;
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            if (Instance == this)
                Instance = null;
        }

        /// <summary>
        /// Fire one probe round-trip. Returns true if the server's ack arrives within
        /// <paramref name="timeout"/>, false on timeout (treat as a dead link -> rebuild).
        /// </summary>
        public async Task<bool> ProbeAsync(TimeSpan timeout)
        {
            if (!IsSpawned)
                return false;

            uint token = _nextToken++;
            var tcs = new TaskCompletionSource<bool>();
            _pending[token] = tcs;

            SendProbeServerRpc(token);

            Task completed = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
            _pending.Remove(token);
            bool alive = completed == tcs.Task && tcs.Task.Result;
            // Steady-state success is silent; only a failed probe (a dead link) is worth a line.
            if (!alive)
                Debug.Log($"[Probe] round-trip token={token} timed out — link not alive");
            return alive;
        }

        [ServerRpc(RequireOwnership = false)]
        private void SendProbeServerRpc(uint token, NetworkConnection sender = null)
        {
            // Reply straight back to the caller — the ack reaching them proves the link is live.
            AckProbeTargetRpc(sender, token);
        }

        [TargetRpc]
        private void AckProbeTargetRpc(NetworkConnection target, uint token)
        {
            if (_pending.TryGetValue(token, out TaskCompletionSource<bool> tcs))
                tcs.TrySetResult(true);
        }
    }
}
