using System;
using System.Collections.Generic;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Invariant #3 (host side) — the endgame match-return readiness barrier. Ported from
    /// Godot <c>NetworkManager</c> :265-390: the host opens a serial-numbered return session,
    /// collects a ready ACK from every expected peer, and commits the shared return to lobby
    /// as soon as ALL peers are ready OR a timeout elapses (host-authoritative fallback).
    ///
    /// The watchdog is a MONOTONIC delta polled via an injected clock
    /// (<c>Time.realtimeSinceStartupAsDouble</c>), never an awaited scene-timer: a suspended
    /// authoritative host freezes a wall-clock timer and would wedge every client. Pure logic,
    /// unit-tested by advancing a fake clock; the ObserversRpc confirm + real scene return are
    /// device-wired on top.
    /// </summary>
    public sealed class MatchReturnQuorum
    {
        /// <summary>Godot <c>MATCH_RETURN_READY_TIMEOUT_MS</c> (5000).</summary>
        public const double DefaultTimeoutSeconds = 5.0;

        private readonly Func<double> _now;
        private readonly double _timeout;
        private readonly Action<string> _log;

        private int _serial;
        private int _sessionId;
        private double _startedAt;
        private bool _committed;
        private readonly HashSet<int> _ready = new();
        private readonly HashSet<int> _expected = new();

        public MatchReturnQuorum(Func<double> nowSeconds, double timeoutSeconds = DefaultTimeoutSeconds,
            Action<string> log = null)
        {
            _now = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
            _timeout = timeoutSeconds;
            _log = log;
        }

        public int SessionId => _sessionId;
        public bool Committed => _committed;
        public int ReadyCount => _ready.Count;

        /// <summary>Open a new return session for the given expected peers (serial id, monotonic
        /// start, cleared ready-set). Godot <c>_begin_match_return_session</c>.</summary>
        public int BeginSession(IEnumerable<int> expectedPeers)
        {
            _serial++;
            _sessionId = _serial;
            _startedAt = _now();
            _committed = false;
            _ready.Clear();
            _expected.Clear();
            if (expectedPeers != null)
                foreach (int p in expectedPeers)
                    if (p > 0)
                        _expected.Add(p);
            _log?.Invoke($"[MatchReturn] begin session #{_sessionId}, expecting {_expected.Count}");
            return _sessionId;
        }

        /// <summary>Record a peer's ready-to-return ACK. Returns true if this newly readies the
        /// peer (Godot <c>_mark_match_return_peer_ready</c>). Ignored without an active session.</summary>
        public bool MarkPeerReady(int peerId)
        {
            if (peerId <= 0 || _sessionId <= 0)
                return false;
            return _ready.Add(peerId);
        }

        /// <summary>Every expected peer has ACKed. An empty expected set is trivially ready
        /// (Godot <c>_are_all_match_return_peers_ready</c>).</summary>
        public bool AreAllReady()
        {
            if (_sessionId <= 0)
                return false;
            foreach (int p in _expected)
                if (!_ready.Contains(p))
                    return false;
            return true;
        }

        /// <summary>
        /// Commit the shared return exactly once — when all peers are ready ("all_ready") or the
        /// monotonic timeout has elapsed ("timeout"). Poll this after each ready ACK and on a
        /// periodic tick. Returns true only on the single committing call.
        /// </summary>
        public bool TryCommit(double now, out string reason)
        {
            reason = null;
            if (_sessionId <= 0 || _committed)
                return false;

            if (AreAllReady())
                reason = "all_ready";
            else if (now - _startedAt >= _timeout)
                reason = "timeout";
            else
                return false;

            _committed = true;
            _log?.Invoke($"[MatchReturn] commit #{_sessionId} ({reason}) {_ready.Count}/{_expected.Count} ready");
            return true;
        }

        /// <summary>Clear all tracking (Godot <c>_reset_match_return_tracking</c>); the serial is
        /// preserved so the next session id is strictly increasing.</summary>
        public void Reset()
        {
            _sessionId = 0;
            _startedAt = 0;
            _committed = false;
            _ready.Clear();
            _expected.Clear();
        }
    }
}
