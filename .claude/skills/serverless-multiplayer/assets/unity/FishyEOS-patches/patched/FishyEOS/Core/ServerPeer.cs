using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Epic.OnlineServices;
using Epic.OnlineServices.P2P;
using FishNet.Managing;
using FishNet.Plugins.FishyEOS.Util;
using UnityEngine;

namespace FishNet.Transporting.FishyEOSPlugin
{
    public class ServerPeer : CommonPeer
    {
        /// <summary>
        /// An auto-incrementing id for each peer.
        /// </summary>
        private static int _latestId = 1;

        # region Private.

        /// <summary>
        /// EOS Socket Id for this peer.
        /// </summary>
        private SocketId _socketId;

        /// <summary>
        /// EOS Server User Id for this peer.
        /// </summary>
        private ProductUserId _localUserId;

        /// <summary>
        /// Queue of incoming local client host packets.
        /// </summary>
        private Queue<LocalPacket> _clientHostIncoming = new Queue<LocalPacket>();

        /// <summary>
        /// Reference to Local Client Host.
        /// </summary>
        private ClientHostPeer _clientHost;

        /// <summary>
        /// EOS Handle for Incoming Peer Requests.
        /// </summary>
        private ulong? _acceptPeerConnectionsEventHandle;

        /// <summary>
        /// EOS Handle for Incoming Peer Connections.
        /// </summary>
        private Dictionary<Connection, ulong>
            _establishPeerConnectionEventHandles = new Dictionary<Connection, ulong>();

        /// <summary>
        /// EOS Handle for Incoming Peer Disconnections.
        /// </summary>
        private Dictionary<string, ulong> _closePeerConnectionEventHandles = new Dictionary<string, ulong>();

        /// <summary>
        /// List of connections to this server.
        /// </summary>
        private List<Connection> _clients = new List<Connection>();

        /// <summary>
        /// Remote ProductUserIds whose connection is actually ESTABLISHED (past
        /// OnPeerConnectionEstablished). The live-PUID guard protects only these — an entry that is
        /// in <see cref="_clients"/> but not here is a zombie (accepted, never established, e.g. a
        /// peer that dropped mid-handshake with no Closed callback) and may be evicted so a genuine
        /// reconnect is not blocked forever.
        /// </summary>
        private readonly HashSet<string> _establishedRemoteUsers = new HashSet<string>();

        /// <summary>
        /// EOS Handle for Peer Connection Interruptions, per remote ProductUserId. EOS raises an
        /// interrupt ~6s after a peer stops responding (vs the ~30s reestablish-timeout before a
        /// Closed callback), letting the host free a dropped slot early so a reconnect is accepted
        /// fast instead of being blocked by the stale session.
        /// </summary>
        private readonly Dictionary<string, ulong> _interruptedPeerConnectionEventHandles = new Dictionary<string, ulong>();

        /// <summary>
        /// In-flight grace coroutines per remote ProductUserId: on interrupt we wait a short grace
        /// then UNCONDITIONALLY proactively close the peer (EOS self-recovery is NOT detected). This
        /// is intentional: the client's own liveness watchdog already reconnects on any interrupt
        /// this long, so freeing the slot early just lets that in-flight reconnect complete instead
        /// of waiting out EOS's ~30s reestablish-timeout. A normal Closed during the grace removes
        /// the peer from <see cref="_clients"/> first, making the proactive close a no-op.
        /// </summary>
        private readonly Dictionary<string, Coroutine> _pendingProactiveClose = new Dictionary<string, Coroutine>();

        /// <summary>Grace after an interrupt before the host proactively closes a dropped peer.</summary>
        private const float ProactiveCloseGraceSeconds = 3f;

        /// <summary>
        /// Stable FishNet connection id per remote ProductUserId. A peer keeps the same id across
        /// reconnects so its owned NetworkObjects are not orphaned by a fresh id (the persistent-peer
        /// intent, mirroring Godot NetworkManager.gd:1127/:1174).
        /// </summary>
        private readonly Dictionary<string, int> _clientIdByRemoteUser = new Dictionary<string, int>();

        /// <summary>
        /// Maximum number of connections allowed.
        /// </summary>
        private int _maximumClients = short.MaxValue;

        #endregion

        /// <summary>
        /// Starts the server.
        /// </summary>
        internal bool StartConnection()
        {
            base.SetLocalConnectionState(LocalConnectionState.Starting, true);
            _transport.StartCoroutine(AuthenticateAndStartListeningForConnections());
            return true;
        }

        /// <summary>
        /// Coroutine that authenticates with EOS and starts listening for incoming connections.
        /// </summary>
        private IEnumerator AuthenticateAndStartListeningForConnections()
        {
            if (_transport.AutoAuthenticate)
            {
                yield return _transport.AuthConnectData.Connect(out var authDataLogin);
                if (authDataLogin.loginCallbackInfo?.ResultCode != Result.Success)
                {
                    _transport.NetworkManager.LogError($"[ServerPeer] Failed to authenticate with EOS Connect. {authDataLogin.loginCallbackInfo?.ResultCode}");
                    base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
                    yield break;
                }
            }

            _transport.NetworkManager.Log($"[ServerPeer] Authenticated with EOS Connect. {EOS.LocalProductUserId}");

            // Attempt to Start Listening for Peer Connections...
            try
            {
                _localUserId = EOS.LocalProductUserId;
                _socketId = new SocketId { SocketName = _transport.SocketName };
                var addNotifyPeerConnectionRequestOptions = new AddNotifyPeerConnectionRequestOptions
                {
                    SocketId = _socketId,
                    LocalUserId = _localUserId,
                };
                _acceptPeerConnectionsEventHandle = EOS.GetCachedP2PInterface().AddNotifyPeerConnectionRequest(
                    ref addNotifyPeerConnectionRequestOptions, null, OnPeerConnectionRequest);

                _transport.NetworkManager.Log($"[ServerPeer] Started listening for incoming connections. Handle #{_acceptPeerConnectionsEventHandle}");
            }
            catch (Exception e)
            {
                _transport.NetworkManager.LogError($"[ServerPeer] Failed to start listening for incoming connections. {e}");
                base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
                yield break;
            }

            base.SetLocalConnectionState(LocalConnectionState.Started, true);
        }

        /// <summary>
        /// Event Callback when peer connection request is received.
        /// </summary>
        private void OnPeerConnectionRequest(ref OnIncomingConnectionRequestInfo data)
        {
            string remoteUserId = data.RemoteUserId.ToString();

            // If this PUID already has a LIVE connection, the request is a duplicate/renegotiation
            // (e.g. EOS switching relay<->direct). Keep the existing FishNet connection untouched —
            // minting a new id or tearing down the old one here would despawn a mid-match peer's owned
            // objects ("client sees no character"). The peer only truly leaves via OnPeerConnectionClosed.
            for (int i = 0; i < _clients.Count; i++)
            {
                if (_clients[i].RemoteUserId != data.RemoteUserId)
                    continue;

                // Live established peer: a duplicate request is a relay<->direct renegotiation.
                // Keep it untouched — minting a new id / tearing it down here despawns its owned
                // objects ("client sees no character"). It truly leaves via OnPeerConnectionClosed.
                if (_establishedRemoteUsers.Contains(remoteUserId))
                {
                    Debug.Log($"[ServerPeer] IGNORED duplicate from {remoteUserId} — live established peer");
                    return;
                }

                // Zombie: accepted before but never established (peer dropped mid-handshake and we
                // never got a Closed callback). Evict it so this genuine reconnect isn't blocked
                // forever by the live-guard, then fall through to accept the fresh request.
                // NOTE: the pre-establishment window is intentionally NOT protected — a peer here
                // owns no NetworkObjects yet, so evicting it cannot cause "client sees no character";
                // worst case a relay<->direct renegotiation mid-handshake re-dials (observable via
                // the EVICT/ACCEPT logs). Only ESTABLISHED peers get the D4 protection above.
                Debug.Log($"[ServerPeer] EVICT zombie (accepted, never established) for {remoteUserId} — allowing reconnect");
                EvictClientConnection(_clients[i]);
                break;
            }

            // New or rejoining peer. Reuse the stable id for this PUID if we've seen it before so a
            // genuine reconnect keeps its identity; otherwise mint a fresh one.
            if (!_clientIdByRemoteUser.TryGetValue(remoteUserId, out int nextId))
            {
                nextId = _latestId++;
                _clientIdByRemoteUser[remoteUserId] = nextId;
            }

            var clientConnection = new Connection(nextId, data.LocalUserId, data.RemoteUserId, data.SocketId);
            _clients.Add(clientConnection);
            Debug.Log($"[ServerPeer] ACCEPT {remoteUserId} id={nextId} (_clients count={_clients.Count})");

            var addNotifyPeerConnectionEstablishedOptions = new AddNotifyPeerConnectionEstablishedOptions
            {
                SocketId = data.SocketId,
                LocalUserId = data.LocalUserId,
            };
            var connectionEstablishedHandle = EOS.GetCachedP2PInterface().AddNotifyPeerConnectionEstablished(
                ref addNotifyPeerConnectionEstablishedOptions, clientConnection, OnPeerConnectionEstablished);
            _establishPeerConnectionEventHandles.Add(clientConnection, connectionEstablishedHandle);

            var acceptConnectionOptions = new AcceptConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = data.RemoteUserId,
                SocketId = data.SocketId,
            };
            var acceptConnectionResult = EOS.GetCachedP2PInterface().AcceptConnection(ref acceptConnectionOptions);

            if (acceptConnectionResult != Result.Success)
            {
                // Roll back the establish-notify handle registered just above, else a failed-accept
                // PUID that later reconnects orphans this EOS notification (leak over flaky retries).
                if (_establishPeerConnectionEventHandles.TryGetValue(clientConnection, out ulong staleHandle))
                {
                    EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionEstablished(staleHandle);
                    _establishPeerConnectionEventHandles.Remove(clientConnection);
                }
                _clients.Remove(clientConnection);
                _transport.NetworkManager.LogError($"[ServerPeer] Failed to accept connection from {data.RemoteUserId} with handle #{data.SocketId} and connection id {nextId}. {acceptConnectionResult}");
            }
        }

        /// <summary>
        /// Event Callback when peer connection is established.
        /// </summary>
        private void OnPeerConnectionEstablished(ref OnPeerConnectionEstablishedInfo data)
        {
            var clientConnection = (Connection)data.ClientData;
            if (_establishPeerConnectionEventHandles.TryGetValue(clientConnection, out var notificationId))
            {
                EOS.GetCachedP2PInterface().RemoveNotifyPeerConnectionEstablished(notificationId);
                _establishPeerConnectionEventHandles.Remove(clientConnection);
            }

            var addNotifyPeerConnectionClosedOptions = new AddNotifyPeerConnectionClosedOptions
            {
                SocketId = clientConnection.SocketId,
                LocalUserId = clientConnection.LocalUserId,
            };
            
            ulong closePeerConnectionHandle =
                EOS.GetCachedP2PInterface().AddNotifyPeerConnectionClosed(ref addNotifyPeerConnectionClosedOptions,
                    clientConnection, OnPeerConnectionClosed);
            
            string remoteUserId = clientConnection.RemoteUserId.ToString();
            if (!_closePeerConnectionEventHandles.TryGetValue(remoteUserId, out ulong existingClosePeerConnectionHandle))
            {
                _closePeerConnectionEventHandles.Add(remoteUserId, closePeerConnectionHandle);
            }
            else
            {
                if (closePeerConnectionHandle != existingClosePeerConnectionHandle)
                {
                    Debug.LogWarningFormat(
                        "[ServerPeer.OnPeerConnectionEstablished] Removing existing close peer connection handle for remote user id {0} with handle #{1} and adding new handle #{2}",
                        remoteUserId, existingClosePeerConnectionHandle, closePeerConnectionHandle);

                    RemoveClosePeerConnectionHandle(remoteUserId, existingClosePeerConnectionHandle);
                    EOS.GetCachedP2PInterface().RemoveNotifyPeerConnectionClosed(existingClosePeerConnectionHandle);
                }
                else
                {
                    Debug.LogWarningFormat("[ServerPeer.OnPeerConnectionEstablished] Existing close peer connection handle for remote user id {0} with handle #{1} is the same as the new handle #{2}",
                        remoteUserId, existingClosePeerConnectionHandle, closePeerConnectionHandle);
                }
            }

            // Watch for an interruption so the host can free the slot early (Step 10). Only one
            // interrupt handle per PUID; a reconnect re-establishes and re-subscribes.
            if (!_interruptedPeerConnectionEventHandles.ContainsKey(remoteUserId))
            {
                var addNotifyInterruptedOptions = new AddNotifyPeerConnectionInterruptedOptions
                {
                    SocketId = clientConnection.SocketId,
                    LocalUserId = clientConnection.LocalUserId,
                };
                ulong interruptedHandle = EOS.GetCachedP2PInterface().AddNotifyPeerConnectionInterrupted(
                    ref addNotifyInterruptedOptions, clientConnection, OnPeerConnectionInterrupted);
                _interruptedPeerConnectionEventHandles.Add(remoteUserId, interruptedHandle);
            }

            _establishedRemoteUsers.Add(remoteUserId);
            _transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started,
                clientConnection.Id, _transport.Index));
            _transport.NetworkManager.Log($"[ServerPeer.OnPeerConnectionEstablished] Established connection from {data.RemoteUserId} with handle #{data.SocketId} and connection id {clientConnection.Id}.");
        }

        /// <summary>
        /// Event Callback when peer connection is closed.
        /// </summary>
        private void OnPeerConnectionClosed(ref OnRemoteConnectionClosedInfo data)
        {
            Debug.Log($"[ServerPeer] CLOSED callback for {data.RemoteUserId} reason={data.Reason}");
            Connection? clientConnection = null;
            for (int i = _clients.Count - 1; i >= 0; i--)
            {
                if(data.RemoteUserId != _clients[i].RemoteUserId)
                    continue;

                clientConnection = _clients[i];
            }

            if (!clientConnection.HasValue)
            {
                Debug.LogWarningFormat("[ServerPeer.OnPeerConnectionClosed] Failed to find connection for remote user id {0}.", data.RemoteUserId);
                return;
            }

            TearDownEstablishedClient(clientConnection.Value);
            _transport.NetworkManager.Log($"[ServerPeer.OnPeerConnectionClosed] Closed connection from {data.RemoteUserId} with connection id {clientConnection.Value.Id}.");
        }

        // Step 10: EOS raises an interrupt ~6s after a peer stops responding (vs the ~30s Closed
        // timeout). After a short grace, proactively close the peer so its reconnect is accepted
        // immediately instead of waiting out the stale session. The close is unconditional — EOS
        // self-recovery is not detected — which is fine because by ~6s the client's liveness
        // watchdog has already begun reconnecting; this just frees the slot for it.
        private void OnPeerConnectionInterrupted(ref OnPeerConnectionInterruptedInfo data)
        {
            string remoteUserId = data.RemoteUserId.ToString();
            if (_pendingProactiveClose.ContainsKey(remoteUserId))
                return; // a grace is already running for this peer
            Debug.Log($"[ServerPeer] INTERRUPTED {remoteUserId} — {ProactiveCloseGraceSeconds:F0}s grace then proactive close");
            _pendingProactiveClose[remoteUserId] =
                _transport.StartCoroutine(ProactiveCloseAfterGrace(data.RemoteUserId));
        }

        private IEnumerator ProactiveCloseAfterGrace(ProductUserId remoteUserId)
        {
            // Realtime (not WaitForSeconds) so a paused (timeScale 0) or backgrounded host still
            // frees the slot on schedule instead of stalling exactly when a reconnect is arriving.
            yield return new WaitForSecondsRealtime(ProactiveCloseGraceSeconds);
            string key = remoteUserId.ToString();
            _pendingProactiveClose.Remove(key); // consumed first so TearDown won't try to stop us

            // A normal Closed during the grace already removed the peer — nothing to do.
            Connection? clientConnection = null;
            for (int i = _clients.Count - 1; i >= 0; i--)
                if (remoteUserId == _clients[i].RemoteUserId)
                    clientConnection = _clients[i];
            if (!clientConnection.HasValue)
                yield break;

            Debug.Log($"[ServerPeer] PROACTIVE CLOSE {key} — interrupted past grace, freeing slot for fast reconnect");
            TearDownEstablishedClient(clientConnection.Value);
        }

        // Full teardown of an established (or interrupted) peer — shared by the normal Closed
        // callback and the Step-10 proactive close. Removes every EOS notify handle, cancels any
        // pending grace, closes the EOS session (so the same PUID can reconnect cleanly), notifies
        // FishNet, and drops the peer from the tracking collections.
        private void TearDownEstablishedClient(Connection clientConnection)
        {
            string remoteUserId = clientConnection.RemoteUserId.ToString();

            if (_pendingProactiveClose.TryGetValue(remoteUserId, out Coroutine pending))
            {
                if (pending != null)
                    _transport.StopCoroutine(pending);
                _pendingProactiveClose.Remove(remoteUserId);
            }
            if (_closePeerConnectionEventHandles.TryGetValue(remoteUserId, out ulong closeHandle))
                RemoveClosePeerConnectionHandle(remoteUserId, closeHandle);
            if (_interruptedPeerConnectionEventHandles.TryGetValue(remoteUserId, out ulong interruptedHandle))
            {
                EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionInterrupted(interruptedHandle);
                _interruptedPeerConnectionEventHandles.Remove(remoteUserId);
            }

            // Fully close the EOS-internal P2P session — without this EOS keeps it alive and, when
            // the same ProductUserId reconnects, "ignores incoming invitation and resends existing
            // session" so the rejoin never establishes.
            var closeConnectionOptions = new CloseConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = clientConnection.RemoteUserId,
                SocketId = _socketId,
            };
            EOS.GetCachedP2PInterface()?.CloseConnection(ref closeConnectionOptions);

            _transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped,
                clientConnection.Id, _transport.Index));

            _establishedRemoteUsers.Remove(remoteUserId);
            _clients.Remove(clientConnection);
        }

        // Tear down a half-open (accepted but never established) connection so its PUID no longer
        // blocks a reconnect. Mirrors OnPeerConnectionClosed's cleanup; a zombie has no close handle
        // (that is registered only once establishment completes).
        private void EvictClientConnection(Connection clientConnection)
        {
            string remoteUserId = clientConnection.RemoteUserId.ToString();

            if (_establishPeerConnectionEventHandles.TryGetValue(clientConnection, out ulong establishHandle))
            {
                EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionEstablished(establishHandle);
                _establishPeerConnectionEventHandles.Remove(clientConnection);
            }
            if (_closePeerConnectionEventHandles.TryGetValue(remoteUserId, out ulong closeHandle))
                RemoveClosePeerConnectionHandle(remoteUserId, closeHandle);

            var closeConnectionOptions = new CloseConnectionOptions
            {
                LocalUserId = _localUserId,
                RemoteUserId = clientConnection.RemoteUserId,
                SocketId = _socketId,
            };
            EOS.GetCachedP2PInterface()?.CloseConnection(ref closeConnectionOptions);

            _establishedRemoteUsers.Remove(remoteUserId);
            _clients.Remove(clientConnection);
        }

        private void RemoveClosePeerConnectionHandle(string remoteUserId, ulong notificationId)
        {
            EOS.GetCachedP2PInterface().RemoveNotifyPeerConnectionClosed(notificationId);
            _closePeerConnectionEventHandles.Remove(remoteUserId);
        }

        /// <summary>
        /// Stops the server.
        /// </summary>
        internal bool StopConnection()
        {
            if (GetLocalConnectionState() == LocalConnectionState.Stopped ||
                GetLocalConnectionState() == LocalConnectionState.Stopping)
                return false;

            base.SetLocalConnectionState(LocalConnectionState.Stopping, true);

            try
            {
                _clients.Clear();
                _establishedRemoteUsers.Clear();
                _clientIdByRemoteUser.Clear();
                _clientHostIncoming.Clear();
                _clientHost?.StopConnection();

                foreach (var entry in _closePeerConnectionEventHandles)
                    EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionClosed(entry.Value);
                _closePeerConnectionEventHandles.Clear();

                foreach (var entry in _establishPeerConnectionEventHandles)
                    EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionEstablished(entry.Value);
                _establishPeerConnectionEventHandles.Clear();

                foreach (var entry in _interruptedPeerConnectionEventHandles)
                    EOS.GetCachedP2PInterface()?.RemoveNotifyPeerConnectionInterrupted(entry.Value);
                _interruptedPeerConnectionEventHandles.Clear();

                foreach (var entry in _pendingProactiveClose)
                    if (entry.Value != null)
                        _transport.StopCoroutine(entry.Value);
                _pendingProactiveClose.Clear();

                if (_acceptPeerConnectionsEventHandle.HasValue)
                {
                    EOS.GetCachedP2PInterface()?
                        .RemoveNotifyPeerConnectionRequest(_acceptPeerConnectionsEventHandle.Value);
                    _acceptPeerConnectionsEventHandle = null;
                }

                var closeConnectionOptions = new CloseConnectionsOptions
                {
                    SocketId = _socketId,
                    LocalUserId = _localUserId,
                };
                EOS.GetCachedP2PInterface()?.CloseConnections(ref closeConnectionOptions);
            }
            catch (Exception e)
            {
                _transport.NetworkManager.LogError($"[ServerPeer] Failed to stop listening for incoming connections. {e}");
                base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
                return false;
            }

            base.SetLocalConnectionState(LocalConnectionState.Stopped, true);
            return true;
        }

        /// <summary>
        /// Stops a remote client from the server, disconnecting the client.
        /// </summary>
        /// <param name="connectionId"></param>
        internal bool StopConnection(int connectionId)
        {
            if (connectionId == FishyEOS.CLIENT_HOST_ID)
            {
                _clientHost.StopConnection();
                return true;
            }

            var clientConnectionExists = _clients.Any(x => x.Id == connectionId);
            if (!clientConnectionExists) return false;

            var clientConnection = _clients.FirstOrDefault(x => x.Id == connectionId);
            var closeConnectionOptions = new CloseConnectionOptions
            {
                SocketId = _socketId,
                LocalUserId = clientConnection.LocalUserId,
                RemoteUserId = clientConnection.RemoteUserId
            };
            EOS.GetCachedP2PInterface().CloseConnection(ref closeConnectionOptions);
            return true;
        }

        /// <summary>
        /// Gets the current ConnectionState of a remote client on the server.
        /// </summary>
        /// <param name="connectionId">ConnectionId to get ConnectionState for.</param>
        internal RemoteConnectionState GetConnectionState(int connectionId)
        {
            if (_clients.Any(x => x.Id == connectionId))
                return RemoteConnectionState.Started;
            else
                return RemoteConnectionState.Stopped;
        }

        /// <summary>
        /// Unused by EOS.
        /// </summary>
        internal void IterateOutgoing() { }

        /// <summary>
        /// Iterates through all incoming packets and handles them.
        /// </summary>
        internal void IterateIncoming()
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            //Iterate local client packets first.
            while (_clientHostIncoming.Count > 0)
            {
                var packet = _clientHostIncoming.Dequeue();
                var segment = new ArraySegment<byte>(packet.Data, 0, packet.Length);
                _transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(segment, packet.Channel,
                    FishyEOS.CLIENT_HOST_ID, _transport.Index));
            }

            var incomingPacketCount = GetIncomingPacketQueueCurrentPacketCount();
            for (ulong i = 0; i < incomingPacketCount; i++)
                if (Receive(_localUserId, out var remoteUserId, out var data, out var channel))
                {
                    int index = 0;
                    bool hasFoundId = false;
                    int connectionId = 0;
                    while (index < _clients.Count && !hasFoundId)
                    {
                        if (_clients[index].RemoteUserId == remoteUserId)
                        {
                            hasFoundId = true;
                            connectionId = _clients[index].Id;
                        }
index++;
                    }

                    if (!hasFoundId) //prevent failures of not getting an id...FishyEOS didn't handle this
                    {
                        return;
                    }
                    _transport.HandleServerReceivedDataArgs(new ServerReceivedDataArgs(data, channel, connectionId,
                        _transport.Index));
                }
        }

        /// <summary>
        /// Sends a packet to a single, or all clients.
        /// </summary>
        internal void SendToClient(byte channelId, ArraySegment<byte> segment, int connectionId)
        {
            if (GetLocalConnectionState() != LocalConnectionState.Started)
                return;

            if (connectionId == FishyEOS.CLIENT_HOST_ID)
            {
                if (_clientHost != null)
                {
                    var packet = new LocalPacket(segment, channelId);
                    _clientHost.ReceivedFromLocalServer(packet);
                }

                return;
            }

            if (_clients.Any(x => x.Id == connectionId))
            {
                var clientConnection = _clients.First(x => x.Id == connectionId);
                var result = Send(_localUserId, clientConnection.RemoteUserId, _socketId, channelId, segment);

                if (result == Result.NoConnection || result == Result.InvalidParameters)
                {
                    _transport.NetworkManager.Log($"Connection to {connectionId} was lost.");
                    StopConnection(connectionId);
                }
                else if (result != Result.Success)
                {
                    _transport.NetworkManager.LogError($"Could not send: {result}");
                }
            }
            else
            {
                _transport.NetworkManager.LogError($"ConnectionId {connectionId} does not exist, data will not be sent.");
            }
        }

        /// <summary>
        /// Returns the maximum number of clients allowed to connect to the server.
        /// If the transport does not support this method the value -1 is returned.
        /// </summary>
        public int GetMaximumClients()
        {
            return _maximumClients;
        }

        /// <summary>
        /// Sets the maximum number of clients allowed to connect to the server.
        /// </summary>
        public void SetMaximumClients(int value)
        {
            _maximumClients = value;
        }

        /// <summary>
        /// Sets the local client host.
        /// </summary>
        internal void SetClientHostPeer(ClientHostPeer clientHostPeer)
        {
            _clientHost = clientHostPeer;
        }

        /// <summary>
        /// Queues a received packet from the local client host.
        /// </summary>
        internal void ReceivedFromClientHost(LocalPacket packet)
        {
            if (_clientHost == null || _clientHost.GetLocalConnectionState() != LocalConnectionState.Started) return;
            _clientHostIncoming.Enqueue(packet);
        }

        /// <summary>
        /// Called when Client Host Connection state changes.
        /// </summary>
        internal void HandleClientHostConnectionStateChange(LocalConnectionState state, bool server)
        {
            switch (state)
            {
                case LocalConnectionState.Started:
                    _transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Started,
                        FishyEOS.CLIENT_HOST_ID, _transport.Index));
                    break;
                case LocalConnectionState.Stopped:
                    _transport.HandleRemoteConnectionState(new RemoteConnectionStateArgs(RemoteConnectionState.Stopped,
                        FishyEOS.CLIENT_HOST_ID, _transport.Index));
                    break;
                case LocalConnectionState.Starting:
                case LocalConnectionState.Stopping:
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }
        }

        /// <summary>
        /// Gets the EOS Local Product User Id of the server.
        /// </summary>
        internal string GetConnectionAddress(int connectionId)
        {
            var client = _clients.FirstOrDefault(x => x.Id == connectionId);
            // Connection is a struct: FirstOrDefault yields a default (RemoteUserId == null) when no
            // remote client owns this id — e.g. the host's own clientHost connection, or an id routed
            // in when this transport is wrapped by Multipass. Guard the null so the lookup returns an
            // empty address instead of throwing (an unhandled throw here aborts the caller, e.g. the
            // player-spawn loop during a host's own spawn).
            return client.RemoteUserId != null ? client.RemoteUserId.ToString() : string.Empty;
        }
    }
}
