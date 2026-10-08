using System;
using System.Threading;
using System.Threading.Tasks;
using Epic.OnlineServices;
using FishNet.Plugins.FishyEOS.Util;
using FishNet.Plugins.FishyEOS.Util.Coroutines;
using TeamNet.Multiplayer.Core;
using UnityEngine;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>Timings of <see cref="EosBootstrap"/>. Defaults are the values measured on device.</summary>
    public sealed class EosBootstrapConfig
    {
        /// <summary>How long boot waits for the caller's dependencies (<c>dependenciesReady</c>) before giving up.</summary>
        public float DependencyWaitSeconds = 5f;
        /// <summary>How long boot waits for the EOS Connect interface to exist after the platform is created.</summary>
        public float PlatformTimeoutSeconds = 15f;
        /// <summary>Boot login: our own realtime timeout. FishyEOS's own timeout runs on a coroutine of the EOS
        /// manager, which cannot fire when this very login is what created that manager.</summary>
        public float LoginTimeoutSeconds = 12f;
        /// <summary>Floor of the timeout used by <see cref="EosBootstrap.EnsureConnectedAsync"/> (it takes the max of this and AuthData.timeout).</summary>
        public float MinEnsureLoginTimeoutSeconds = 5f;
        /// <summary>Connect.Logout is best effort; give up waiting for its callback after this long.</summary>
        public float LogoutTimeoutSeconds = 5f;
    }

    /// <summary>
    /// Boot-time EOS bring-up and Connect login, plus the "make sure I have a live Connect user" call used
    /// before lobby create/join. Ported from the KT boot task (InitEosAsync) and the lobby session
    /// (EnsureEosConnectAsync), including their ordering rules:
    ///
    /// Ordering is the whole point. The first code to touch EOS also constructs the EOS platform
    /// (AddComponent EOSManager -> Awake -> Init -> LoadEOSLibraries). A login issued in the same call
    /// that constructs the platform never gets its callback. So: ensure the platform, poll until the
    /// Connect interface exists, wait one frame so the platform is pumping, and only then log in.
    /// Against a platform that is already up the same login answers in well under a second.
    ///
    /// All waits use realtime (<see cref="Time.realtimeSinceStartup"/>) and a frame-yield, never
    /// <c>Time.time</c> (scaled; stops while paused) and never a FishyEOS coroutine timeout.
    /// Failures are reported through <see cref="AuthFailed"/> so a room coordinator can fail queued
    /// intents instead of waiting on a login that is not coming.
    ///
    /// Not a MonoBehaviour: it only needs a main-thread continuation (Unity's synchronization
    /// context), which <see cref="Task.Yield"/> provides. Tests can inject <c>frameYield</c> and <c>clock</c>.
    /// Status: compiled only, NOT run on device in this skill (the algorithm is KT's).
    /// </summary>
    public sealed class EosBootstrap
    {
        readonly EosBootstrapConfig config;
        readonly Func<AuthData> authProvider;
        readonly Func<bool> dependenciesReady;
        readonly Action<string> log;
        readonly Func<Task> frameYield;
        readonly Func<float> clock;

        /// <summary>Raised with a short machine-readable reason on every bring-up failure.</summary>
        public event Action<string> AuthFailed;
        /// <summary>Raised with the PUID string after a successful boot login.</summary>
        public event Action<PlayerId> Authenticated;

        /// <summary>Last PUID observed after a successful login or reuse; default when none.</summary>
        public PlayerId LocalUser { get; private set; }
        /// <summary>Reason of the most recent failure from either entry point; null after a success.</summary>
        public string LastError { get; private set; }

        /// <param name="authProvider">Builds the AuthData used for login (display name, timeout); usually the
        /// transport's <c>AuthConnectData</c> at boot, a fresh <c>new AuthData{displayName=...}</c> later.</param>
        /// <param name="dependenciesReady">Null = no dependency to wait for. Otherwise boot waits (up to
        /// <see cref="EosBootstrapConfig.DependencyWaitSeconds"/>) until it returns true.</param>
        public EosBootstrap(Func<AuthData> authProvider, Func<bool> dependenciesReady = null,
            EosBootstrapConfig config = null, Action<string> log = null,
            Func<Task> frameYield = null, Func<float> clock = null)
        {
            this.authProvider = authProvider ?? throw new ArgumentNullException(nameof(authProvider));
            this.dependenciesReady = dependenciesReady;
            this.config = config ?? new EosBootstrapConfig();
            this.log = log ?? (_ => { });
            this.frameYield = frameYield ?? DefaultFrameYield;
            this.clock = clock ?? DefaultClock;
        }

        static async Task DefaultFrameYield() => await Task.Yield();
        static float DefaultClock() => Time.realtimeSinceStartup;

        // ---- boot ---------------------------------------------------------------------------------

        /// <summary>
        /// Boot task: wait dependencies, create the platform, wait for Connect, one frame, login.
        /// Returns false on any failure (after raising <see cref="AuthFailed"/>) so the boot pipeline can retry;
        /// a later success clears the error, so reporting early costs the retry nothing.
        /// </summary>
        public async Task<bool> InitAsync(CancellationToken ct = default)
        {
            if (dependenciesReady != null)
            {
                float waitStarted = clock();
                while (!dependenciesReady())
                {
                    if (ct.IsCancellationRequested) return false;
                    if (clock() - waitStarted > config.DependencyWaitSeconds)
                        return Fail("session-dependencies-unresolved");
                    await frameYield();
                }
            }

            if (!EnsurePlatform(out string platformError)) return Fail(platformError);

            float platformStarted = clock();
            while (EOS.GetCachedConnectInterface() == null)
            {
                if (ct.IsCancellationRequested) return false;
                if (clock() - platformStarted > config.PlatformTimeoutSeconds)
                    return Fail("eos-platform-unavailable");
                await frameYield();
            }
            // One tick with the interfaces live, so the platform is pumping before the login lands.
            await frameYield();

            if (!DevelopmentEosAuth.TryResolve(authProvider(), out var auth, out string authError))
                return Fail(authError);

            float started = clock();
            var outcome = await LoginAsync(auth, config.LoginTimeoutSeconds, ct);
            if (outcome.Cancelled) return false;
            if (outcome.Error != null) return Fail(outcome.Error);

            log($"[EosBootstrap] login OK in {clock() - started:F2}s");
            return Succeed(outcome.Puid, raiseAuthenticated: true);
        }

        // ---- ensure (before lobby create / join) --------------------------------------------------

        /// <summary>
        /// Ensure a live Connect user. Without <paramref name="forceRefresh"/> an existing PUID is reused
        /// only when Connect reports <c>GetLoginStatus == LoggedIn</c> (a non-null PUID alone can still make
        /// CreateLobby fail with InvalidAuth after long sessions). With it, the current user is logged out
        /// first (best effort, <see cref="EosBootstrapConfig.LogoutTimeoutSeconds"/>) and a fresh login runs.
        /// </summary>
        public async Task<bool> EnsureConnectedAsync(CancellationToken ct = default, bool forceRefresh = false)
        {
            var defaultAuth = authProvider();
            if (!DevelopmentEosAuth.TryResolve(defaultAuth, out var auth, out string authError))
            {
                LastError = authError;
                log($"[EosBootstrap] {authError}");
                return false;
            }

            if (!forceRefresh)
            {
                var existing = EOS.LocalProductUserId;
                if (existing != null && existing.IsValid())
                {
                    var connect = EOS.GetCachedConnectInterface();
                    if (connect != null && connect.GetLoginStatus(existing) == LoginStatus.LoggedIn)
                    {
                        string existingStr = existing.ToString();
                        if (!string.IsNullOrEmpty(existingStr))
                        {
                            LocalUser = new PlayerId(existingStr);
                            LastError = null;
                            log("[EosBootstrap] already logged in, reusing PUID");
                            return true;
                        }
                    }
                }
            }
            else
            {
                await LogoutBestEffortAsync(ct);
                if (ct.IsCancellationRequested) return false;
            }

            float timeout = Mathf.Max(config.MinEnsureLoginTimeoutSeconds, auth.timeout);
            var outcome = await LoginAsync(auth, timeout, ct);
            if (outcome.Cancelled) return false;
            if (outcome.Error != null)
            {
                LastError = outcome.Error;
                log($"[EosBootstrap] ensure failed: {outcome.Error}");
                return false;
            }
            Succeed(outcome.Puid, raiseAuthenticated: false);
            log($"[EosBootstrap] login OK forceRefresh={forceRefresh}");
            return true;
        }

        /// <summary>
        /// One-shot re-auth: runs <paramref name="operation"/>; if it answers InvalidAuth or AuthExpired,
        /// forces a logout + relogin once and runs it again. Never loops: the second answer is returned as is,
        /// and a failed relogin returns the original result.
        /// </summary>
        public async Task<Result> RunWithReauthOnceAsync(Func<Task<Result>> operation, CancellationToken ct = default)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            var first = await operation();
            if (!NeedsReauth(first)) return first;

            log($"[EosBootstrap] {first} -> forcing re-auth once");
            if (!await EnsureConnectedAsync(ct, forceRefresh: true)) return first;
            return await operation();
        }

        /// <summary>True for the results that a stale Connect session produces.</summary>
        public static bool NeedsReauth(Result result)
            => result == Result.InvalidAuth || result == Result.AuthExpired;

        // ---- internals ----------------------------------------------------------------------------

        readonly struct LoginOutcome
        {
            public readonly bool Cancelled;
            public readonly string Error;
            public readonly string Puid;
            public LoginOutcome(bool cancelled, string error, string puid)
            { Cancelled = cancelled; Error = error; Puid = puid; }
        }

        // Deliberately not `yield return auth.Connect(...)`: we wait on the result ourselves with our own
        // realtime clock so a lost callback stays bounded.
        async Task<LoginOutcome> LoginAsync(AuthData auth, float timeoutSeconds, CancellationToken ct)
        {
            AuthDataLogin login;
            try
            {
                auth.Connect(out login);
            }
            catch (Exception e)
            {
                return new LoginOutcome(false, "eos-login-threw:" + e.Message, null);
            }

            float started = clock();
            while (login.loginCallbackInfo == null)
            {
                if (ct.IsCancellationRequested) return new LoginOutcome(true, null, null);
                if (clock() - started > timeoutSeconds)
                    return new LoginOutcome(false, $"eos-login-no-answer-in-{timeoutSeconds:F0}s", null);
                await frameYield();
            }

            Result result = login.loginCallbackInfo.Value.ResultCode;
            if (result != Result.Success) return new LoginOutcome(false, $"eos-login-{result}", null);

            var puid = EOS.LocalProductUserId;
            if (puid == null || !puid.IsValid())
                return new LoginOutcome(false, "eos-login-puid-invalid", null);
            string puidStr = puid.ToString();
            if (string.IsNullOrEmpty(puidStr))
                return new LoginOutcome(false, "eos-login-puid-empty", null);
            return new LoginOutcome(false, null, puidStr);
        }

        async Task LogoutBestEffortAsync(CancellationToken ct)
        {
            try
            {
                var connect = EOS.GetCachedConnectInterface();
                var puid = EOS.LocalProductUserId;
                if (connect == null || puid == null || !puid.IsValid()) return;

                var tcs = new TaskCompletionSource<bool>();
                var opts = new Epic.OnlineServices.Connect.LogoutOptions { LocalUserId = puid };
                connect.Logout(ref opts, null,
                    (ref Epic.OnlineServices.Connect.LogoutCallbackInfo _) => tcs.TrySetResult(true));

                float started = clock();
                while (!tcs.Task.IsCompleted)
                {
                    if (ct.IsCancellationRequested) break;
                    if (clock() - started > config.LogoutTimeoutSeconds) break;
                    await frameYield();
                }
                log($"[EosBootstrap] connect logout done completed={tcs.Task.IsCompleted}");
            }
            catch (Exception e)
            {
                // Failure is tolerated: a DeviceId login can still succeed after a soft-stale session.
                log($"[EosBootstrap] connect logout best-effort failed: {e.Message}");
            }
            finally
            {
                LocalUser = default;
            }
        }

        static bool EnsurePlatform(out string error)
        {
            error = null;
            try
            {
                if (EOS.GetPlatformInterface() != null) return true;
                error = "eos-platform-init-failed";
            }
            catch (Exception e)
            {
                error = "eos-platform-init-threw:" + e.Message;
            }
            return false;
        }

        bool Fail(string reason)
        {
            LastError = reason;
            log($"[EosBootstrap] init failed: {reason}");
            AuthFailed?.Invoke(reason);
            return false;
        }

        bool Succeed(string puid, bool raiseAuthenticated)
        {
            LocalUser = new PlayerId(puid);
            LastError = null;
            if (raiseAuthenticated) Authenticated?.Invoke(LocalUser);
            return true;
        }
    }
}
