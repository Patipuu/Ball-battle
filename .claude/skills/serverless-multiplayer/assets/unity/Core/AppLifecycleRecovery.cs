using System;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>What the session should do after the app resumes: keep the live link (and
    /// resync), or tear down and rebuild — split by role because a host can't force its clients
    /// to re-handshake mid-match (Godot falls it back to lobby instead).</summary>
    public enum ResumeAction
    {
        ReuseAndResync,
        ClientRebuild,
        HostReturnToLobby,
    }

    /// <summary>
    /// App-lifecycle recovery (invariant #4). On mobile, backgrounding pauses the update loop so
    /// the EOS SDK stops ticking and the P2P link can silently die while away — no close callback
    /// fires and FishNet stays <c>Started</c>. On resume this runs the ACTIVE liveness probe
    /// BEFORE any reuse decision, then routes: alive ⇒ reuse (+ resync); dead ⇒ client rebuild
    /// or host return-to-lobby. Ports Godot <c>_recover_gameplay_link_after_resume</c>
    /// (<c>NetworkManager.gd:637-682</c>).
    ///
    /// Pure logic over the <see cref="IP2PBoundary"/> seam with the settle delay injected as a
    /// <see cref="Func{TimeSpan,Task}"/> — no <c>MonoBehaviour</c> or Unity timer — so the routing
    /// (including the host branch the device rig can't background) is deterministically
    /// unit-testable. The caller (the single session owner) actuates the returned action.
    /// </summary>
    public sealed class AppLifecycleRecovery
    {
        private readonly IP2PBoundary _p2p;
        private readonly Func<TimeSpan, Task> _delay;
        private readonly Action<string> _log;

        public AppLifecycleRecovery(IP2PBoundary p2p, Func<TimeSpan, Task> delay, Action<string> log = null)
        {
            _p2p = p2p ?? throw new ArgumentNullException(nameof(p2p));
            _delay = delay ?? throw new ArgumentNullException(nameof(delay));
            _log = log;
        }

        /// <summary>
        /// Decide the post-resume action. Waits <paramref name="settle"/> for EOS to re-report
        /// connection status, then probes <paramref name="targetHost"/> once. A fresh probe
        /// success is the ONLY thing that authorizes reuse — never <c>Started</c> alone.
        /// </summary>
        public async Task<ResumeAction> OnResumeAsync(bool isHost, PlayerId targetHost,
                                                      TimeSpan settle, TimeSpan probeTimeout)
        {
            await _delay(settle);
            bool alive = await _p2p.ProbeAliveAsync(targetHost, probeTimeout);
            // alive -> REUSE; dead -> client rebuild or host return-to-lobby.
            // Callers must not rebuild while a reconnect is already in flight.
            if (alive)
                return Log(ResumeAction.ReuseAndResync, "resume link ok");
            return isHost
                ? Log(ResumeAction.HostReturnToLobby, "resume host link dead -> return to lobby")
                : Log(ResumeAction.ClientRebuild, "resume client link dead -> rebuild");
        }

        private ResumeAction Log(ResumeAction action, string reason)
        {
            _log?.Invoke($"[Lifecycle] {action}: {reason}");
            return action;
        }
    }
}
