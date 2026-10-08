using System;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// Invariant #3 (client side) — the client-side self-return fallback. If the authoritative
    /// host's match-return commit does not arrive within a client-side bound, the client returns
    /// itself to the lobby rather than hanging on a wedged/suspended host (Godot's host-resume
    /// path can drop the in-flight session; clients must not wedge on it).
    ///
    /// Monotonic, single-shot: armed when the client begins waiting for the return, disarmed when
    /// the host's return actually arrives. The bound defaults ABOVE the host quorum timeout so the
    /// host's authoritative return normally wins and the fallback only fires when the host is truly
    /// stuck. Pure logic over an injected clock; the real self-return (scene load) is device-wired.
    /// </summary>
    public sealed class ClientReturnFallback
    {
        /// <summary>Default client bound — above <see cref="MatchReturnQuorum.DefaultTimeoutSeconds"/>
        /// (5s) plus propagation slack, so the host's return wins under normal conditions.</summary>
        public const double DefaultBoundSeconds = 8.0;

        private readonly Func<double> _now;
        private readonly double _bound;
        private readonly Action<string> _log;

        private bool _armed;
        private double _armedAt;
        private bool _fired;

        public ClientReturnFallback(Func<double> nowSeconds, double boundSeconds = DefaultBoundSeconds,
            Action<string> log = null)
        {
            _now = nowSeconds ?? throw new ArgumentNullException(nameof(nowSeconds));
            _bound = boundSeconds;
            _log = log;
        }

        public bool Armed => _armed;
        public bool Fired => _fired;

        /// <summary>Start the fallback clock when the client begins waiting for the host's return.
        /// Idempotent — re-arming an already-armed fallback does not restart it.</summary>
        public void Arm()
        {
            if (_armed)
                return;
            _armed = true;
            _armedAt = _now();
            _fired = false;
            _log?.Invoke("[MatchReturn] client fallback armed");
        }

        /// <summary>The host's return arrived — cancel the fallback.</summary>
        public void Disarm(string reason = "host_return_arrived")
        {
            if (!_armed)
                return;
            _armed = false;
            _fired = false;
            _log?.Invoke($"[MatchReturn] client fallback disarmed ({reason})");
        }

        /// <summary>
        /// True the single time the bound elapses without the host's return — the client should
        /// then self-return to lobby. Poll on a periodic tick with the monotonic clock.
        /// </summary>
        public bool ShouldSelfReturn(double now)
        {
            if (!_armed || _fired)
                return false;
            if (now - _armedAt < _bound)
                return false;
            _fired = true;
            _log?.Invoke("[MatchReturn] client self-returning (host return did not arrive)");
            return true;
        }
    }
}
