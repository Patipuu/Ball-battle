using System;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>Verdict of a persistent-peer reuse check: keep the live peer or tear it
    /// down and rebuild. Carries a reason for diagnostics (mirrors Godot's EVID2 logs).</summary>
    public readonly struct ReuseDecision
    {
        public bool Reuse { get; }
        public string Reason { get; }

        private ReuseDecision(bool reuse, string reason)
        {
            Reuse = reuse;
            Reason = reason;
        }

        public static ReuseDecision KeepAlive(string reason) => new ReuseDecision(true, reason);
        public static ReuseDecision Rebuild(string reason) => new ReuseDecision(false, reason);
    }

    /// <summary>
    /// Decides whether to reuse the existing FishNet peer across matches/reconnects instead
    /// of tearing it down and re-handshaking — the fix for cross-match "client sees no
    /// character" flaps (Godot <c>create_server</c>:1121 / <c>join_game</c>:1174, which reuse
    /// only when <c>get_connection_status() == CONNECTION_CONNECTED</c>).
    ///
    /// FishNet has no such tri-state — <c>IsStarted</c> stays true on a silently-dead
    /// mobile-suspended link — so the client path gates reuse on an ACTIVE round-trip probe
    /// (<see cref="IP2PBoundary.ProbeAliveAsync"/>), never on <c>IsStarted</c> alone. Pure
    /// decision logic over the <see cref="IP2PBoundary"/> seam, so it is unit-testable with a
    /// fake; the real probe is an app-level RPC on the transport, verified on device.
    /// </summary>
    public sealed class PersistentPeerReuse
    {
        private readonly IP2PBoundary _p2p;
        private readonly Action<string> _log;

        public PersistentPeerReuse(IP2PBoundary p2p, Action<string> log = null)
        {
            _p2p = p2p ?? throw new ArgumentNullException(nameof(p2p));
            _log = log;
        }

        /// <summary>
        /// Client reuse: keep the existing client link ONLY when it is up, points at the same
        /// <paramref name="targetHost"/>, AND a fresh liveness probe succeeds. Any failure ⇒
        /// rebuild. The probe is what prevents reusing a Started-but-dead link after a resume.
        /// </summary>
        public async Task<ReuseDecision> DecideClientReuseAsync(PlayerId targetHost, TimeSpan probeTimeout)
        {
            if (!_p2p.IsStarted)
                return Log(ReuseDecision.Rebuild("transport not started"));
            if (_p2p.ConnectedHost != targetHost)
                return Log(ReuseDecision.Rebuild("connected to a different host"));

            bool alive = await _p2p.ProbeAliveAsync(targetHost, probeTimeout);
            return Log(alive
                ? ReuseDecision.KeepAlive("fresh liveness probe ok")
                : ReuseDecision.Rebuild("liveness probe timed out (Started-but-dead)"));
        }

        /// <summary>
        /// Host reuse: keep the running server peer rather than recreating it — recreating a
        /// live server drops every connected client (the cross-match flap root cause). Gated on
        /// <c>IsStarted</c>; per-client liveness after a host resume is handled by the resume
        /// probe (AppLifecycleRecovery #4), which runs before this decision.
        /// </summary>
        public ReuseDecision DecideHostReuse()
        {
            return Log(_p2p.IsStarted
                ? ReuseDecision.KeepAlive("server peer still running")
                : ReuseDecision.Rebuild("no live server peer"));
        }

        private ReuseDecision Log(ReuseDecision decision)
        {
            _log?.Invoke($"[PeerReuse] {(decision.Reuse ? "REUSE" : "REBUILD")}: {decision.Reason}");
            return decision;
        }
    }
}
