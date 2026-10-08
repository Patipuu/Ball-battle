using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Object;
using FishNet.Transporting;
using UnityEngine;

namespace TeamNet.Multiplayer.FishNetEos
{
    /// <summary>
    /// SKELETON of a scene-scoped player spawner that is gated on the match load barrier instead of the
    /// client CONNECT event.
    ///
    /// Why not FishNet's stock <c>PlayerSpawner</c>: its trigger is the client's connect event. When the
    /// session (NetworkManager + lobby) deliberately outlives the gameplay scene so a rematch keeps the
    /// room and its peers, an already-connected client never raises that event again, so re-entering
    /// gameplay leaves the scene without players. And spawning on connect hands a player to a peer that
    /// is still in the lobby, whose own Single-mode scene load then destroys it (an empty scene).
    ///
    /// What this template keeps from the original design:
    ///  - Lives IN the gameplay scene: it owns player objects for exactly as long as the scene is loaded.
    ///  - Server-only. Polls (every 0.5 s) for the life of the scene so a mid-match joiner is served once
    ///    it loads too; despawn stays event-driven.
    ///  - Multiplayer room: a connection gets its player only once <see cref="IMatchLoadGate"/> says it
    ///    reported loading THIS match AND <see cref="ISpawnSlotResolver"/> can place it.
    ///  - Solo: serves only the host's own local client, so a stale peer still dialling this PUID cannot
    ///    walk a second, remotely controlled player into a solo session. A remote connection seen
    ///    while the gate is missing or inactive raises the same starvation alarm, so a forgotten
    ///    <see cref="MatchLoadGate.Provider"/> cannot fail silently.
    ///  - Starvation alarm: a connected client the barrier keeps skipping is normally mid scene-load for
    ///    a few seconds; past <see cref="BarrierStarvationSeconds"/> it can no longer open for it (e.g.
    ///    its PUID never resolves because of a transport id-space mismatch) and the player would sit in
    ///    the match with no character and no error — alarm once instead of failing silently.
    ///  - <see cref="HoldSpawning"/> lets a custom-content loader hold the spawner until the spawn
    ///    points are valid.
    ///
    /// What the game supplies: the player prefab, an <see cref="ISpawnSlotResolver"/> (assign
    /// <see cref="SlotResolver"/>), and an installed <see cref="MatchLoadGate.Provider"/>.
    /// </summary>
    public class PlayerSpawnServiceTemplate : MonoBehaviour
    {
        [Tooltip("Player prefab to spawn per connected client.")]
        [SerializeField] private NetworkObject playerPrefab;

        /// <summary>Game-supplied placement. Assign before the server starts (e.g. in Awake of your subclass).</summary>
        public ISpawnSlotResolver SlotResolver { get; set; }

        /// <summary>While true the spawn poll serves no one.</summary>
        public bool HoldSpawning { get; set; }

        /// <summary>Seconds a client may be skipped by the barrier before the starvation alarm fires (once).</summary>
        public float BarrierStarvationSeconds { get; set; } = 10f;

        private NetworkManager _networkManager;
        private readonly Dictionary<NetworkConnection, NetworkObject> _players = new();
        private readonly Dictionary<int, float> _barrierSkipSince = new();
        private readonly HashSet<int> _barrierAlarmed = new();

        private void Start()
        {
            _networkManager = InstanceFinder.NetworkManager;
            if (_networkManager == null)
            {
                Debug.LogError("[PlayerSpawn] No NetworkManager — no players will spawn.");
                return;
            }

            // Despawn on disconnect stays event-driven. Spawning does NOT: the host reads its own
            // load-epoch write asynchronously and loads before a peer still in the lobby, so a
            // connect-triggered spawn would hand that peer a player its own scene load then destroys.
            _networkManager.ServerManager.OnRemoteConnectionState += OnRemoteConnectionState;

            // The host may be brought up by the scene bootstrap on this very frame, so Start-order
            // between the two is a coin flip. The coroutine waits for the server instead of requiring
            // it pre-started; on a pure client it simply never fires.
            StartCoroutine(SpawnWhenLoaded());
        }

        private void OnDestroy()
        {
            if (_networkManager == null)
                return;
            if (_networkManager.ServerManager != null)
                _networkManager.ServerManager.OnRemoteConnectionState -= OnRemoteConnectionState;
            DespawnAll();
        }

        private IEnumerator SpawnWhenLoaded()
        {
            var wait = new WaitForSeconds(0.5f);
            while (_networkManager != null && _networkManager.ServerManager != null
                   && !_networkManager.ServerManager.Started)
                yield return wait;

            while (_networkManager != null && _networkManager.ServerManager != null
                   && _networkManager.ServerManager.Started)
            {
                if (HoldSpawning)
                {
                    yield return wait;
                    continue;
                }

                // Re-read each poll: the gate can appear (bootstrap ordering) or be torn down
                // (direct-scene launch) while this coroutine lives — a captured null would kill the loop.
                IMatchLoadGate gate = MatchLoadGate.Provider;
                bool barrierActive = gate != null && gate.IsBarrierActive;

                foreach (NetworkConnection conn in _networkManager.ServerManager.Clients.Values)
                {
                    if (conn == null || _players.ContainsKey(conn))
                        continue;

                    if (barrierActive)
                    {
                        // Multiplayer room: no player until this peer reports loading THIS match.
                        if (!gate.HasClientLoadedCurrentMatch(conn.ClientId))
                        {
                            WarnIfBarrierStarved(conn.ClientId, "the load barrier never opened — check PUID resolution (transport id spaces) and load-epoch replication");
                            continue;
                        }
                    }
                    else
                    {
                        // Solo: the load barrier never opens (no match epoch), so serve only this
                        // host's own client.
                        NetworkConnection local = _networkManager.ClientManager.Connection;
                        if (local == null || local.ClientId != conn.ClientId)
                        {
                            // A remote peer while the barrier is off is either a stale dialer (fine to
                            // keep out) or, more likely, a gate that was never installed / reports
                            // inactive too early. Never leave that silent: alarm after the window.
                            WarnIfBarrierStarved(conn.ClientId, gate == null
                                ? "MatchLoadGate.Provider is not installed, so the multiplayer barrier is off"
                                : "IsBarrierActive is false while a remote peer is connected (is the gate keyed on lobby membership?)");
                            continue;
                        }
                    }

                    if (SlotResolver == null
                        || !SlotResolver.TryResolve(conn, out Vector3 position, out Quaternion rotation))
                    {
                        // Slot not known yet (e.g. join order not replicated): wait, do not guess.
                        WarnIfBarrierStarved(conn.ClientId, "no spawn slot could be resolved");
                        continue;
                    }

                    _barrierSkipSince.Remove(conn.ClientId);
                    _barrierAlarmed.Remove(conn.ClientId);
                    SpawnFor(conn, position, rotation);
                }
                yield return wait;
            }
        }

        private void OnRemoteConnectionState(NetworkConnection conn, RemoteConnectionStateArgs args)
        {
            // Only teardown here — spawning is gated on the load barrier in SpawnWhenLoaded.
            if (args.ConnectionState != RemoteConnectionState.Started)
            {
                DespawnFor(conn);
                _barrierSkipSince.Remove(conn.ClientId);
                _barrierAlarmed.Remove(conn.ClientId);
            }
        }

        private void WarnIfBarrierStarved(int clientId, string detail)
        {
            if (!_barrierSkipSince.TryGetValue(clientId, out float since))
            {
                _barrierSkipSince[clientId] = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - since < BarrierStarvationSeconds || !_barrierAlarmed.Add(clientId))
                return;
            Debug.LogError($"[PlayerSpawn] client {clientId} has been connected {Time.unscaledTime - since:F0}s "
                           + $"and has NO player character: {detail}.");
        }

        private void SpawnFor(NetworkConnection conn, Vector3 position, Quaternion rotation)
        {
            if (playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawn] playerPrefab is not assigned.");
                return;
            }
            NetworkObject nob = _networkManager.GetPooledInstantiated(playerPrefab, position, rotation, true);
            _networkManager.ServerManager.Spawn(nob, conn);
            _players[conn] = nob;
            Debug.Log($"[PlayerSpawn] client {conn.ClientId} has a character");
        }

        private void DespawnFor(NetworkConnection conn)
        {
            if (conn == null || !_players.TryGetValue(conn, out NetworkObject nob))
                return;
            _players.Remove(conn);
            if (nob != null && nob.IsSpawned)
                _networkManager.ServerManager.Despawn(nob);
        }

        /// <summary>Returns every player this scene spawned, so the surviving session does not own
        /// objects belonging to a scene that no longer exists.</summary>
        private void DespawnAll()
        {
            if (_networkManager.ServerManager == null || !_networkManager.ServerManager.Started)
            {
                _players.Clear();
                return;
            }
            foreach (NetworkObject nob in _players.Values)
                if (nob != null && nob.IsSpawned)
                    _networkManager.ServerManager.Despawn(nob);
            _players.Clear();
        }
    }
}
