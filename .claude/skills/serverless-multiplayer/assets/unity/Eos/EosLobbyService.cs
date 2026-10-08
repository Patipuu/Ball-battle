using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using Epic.OnlineServices.RTC;
using FishNet.Plugins.FishyEOS.Util;
using TeamNet.Multiplayer.Core;
// Epic.OnlineServices.Lobby.Attribute collides with System.Attribute; alias the EOS one.
using EosAttribute = Epic.OnlineServices.Lobby.Attribute;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// EOS-backed <see cref="ILobbyService"/>. Ports the connection spike's proven (W0)
    /// create / search-join / leave against the raw <c>LobbyInterface</c> into the seam:
    /// EOS callbacks are wrapped as <see cref="Task"/>s, every op is routed through
    /// <see cref="GenerationGuard"/> so a result landing after a teardown / scene change is
    /// dropped, and the room/member attribute write+read path (<see cref="LobbyKeys"/>) is
    /// added. <see cref="PlayerId"/> ↔ <c>ProductUserId</c> mapping happens only here, at the
    /// EOS boundary. EOS callbacks fire on the main thread during the platform tick, so the
    /// <see cref="TaskCompletionSource{TResult}"/> completions are main-thread safe.
    /// </summary>
    public sealed class EosLobbyService : ILobbyService, IDeferredLobbyLeaveService,
        ILobbyMembershipLeaseService
    {
        /// <summary>Default bucket; a per-call bucket in <see cref="LobbyConfig"/> overrides it.</summary>
        public const string DefaultBucketId = "default-lobby";
        internal const float PendingLeaveEntryWaitSeconds = 8f;
        internal const float JoinCallbackTimeoutSeconds = 30f;
        internal const float CleanupCallbackTimeoutSeconds = 15f;
        internal const float LeaveCallbackTimeoutSeconds = 15f;
        internal const int LeaveRecoveryAttempts = 3;

        private readonly GenerationGuard _guard;
        private readonly EosLobbyOperationOwnership.LifecycleCertificate
            _lifecycleCertificate;
        private readonly Action<string> _log;
        private readonly ILobbyKeySchema _keySchema;
        private readonly ILobbyRtcHook _rtc;

        private string _currentLobbyId;
        private PlayerId _ownerPuid;
        private long _currentMembershipOperation;
        // Cached so the read-only GetMembers() can copy lobby details without threading
        // a localUser through every call. Set on each successful create/join.
        private PlayerId _localUser;
        internal Func<PlayerId, string, Task<bool>> LeaveBackendOverrideForTests;
        internal Func<int, Task> LeaveRetryDelayOverrideForTests;
        internal Func<Task> LeaveCallbackTimeoutOverrideForTests;
        internal Func<Task> LeaveWorkerBeforeSubmissionOverrideForTests;
        internal Func<Task> LeaveWorkerAfterSubmissionClaimFailureOverrideForTests;

        /// <param name="guard">Stale-operation guard shared with the session owner.</param>
        /// <param name="log">Optional diagnostic sink.</param>
        /// <param name="keySchema">The game's lobby attributes beyond <see cref="LobbyKeys"/>
        /// (scope + member snapshot keys). Null = protocol keys only.</param>
        /// <param name="rtc">Optional voice hook. Null = lobbies are created without an RTC room.</param>
        public EosLobbyService(GenerationGuard guard, Action<string> log = null,
            ILobbyKeySchema keySchema = null, ILobbyRtcHook rtc = null)
        {
            _guard = guard ?? throw new ArgumentNullException(nameof(guard));
            _lifecycleCertificate =
                EosLobbyOperationOwnership.IssueLifecycleCertificate(_guard);
            _log = log;
            _keySchema = keySchema ?? EmptyLobbyKeySchema.Instance;
            _rtc = rtc;
        }

        // Rooms are always created Publicadvertised so join-by-code keeps working. "Private" is the
        // LobbyKeys.Private room attribute (written by the session owner), which RandomMatchPolicy rejects.
        // SetRoomVisibilityAsync(Advertised) restores Publicadvertised; the flag persists, so a private
        // room is never random-matched after its first match either.

        /// <summary>Current lobby owner. Set on create/join and refreshed on every <see cref="GetMembers"/> poll.</summary>
        public PlayerId OwnerPuid => _ownerPuid;
        public string CurrentLobbyId => _currentLobbyId;

        /// <summary>
        /// Teardown: call after <c>guard.Invalidate(reason)</c> so the ownership ledger stops holding this
        /// service's lifecycle certificate. See <see cref="EosLifecycle"/>.
        /// </summary>
        public void RevokeLifecycleCertificate()
            => EosLobbyOperationOwnership.RevokeLifecycleCertificate(
                _lifecycleCertificate);

        bool ILobbyMembershipLeaseService.TryCaptureMembership(
            PlayerId localUser,
            string lobbyId,
            out CapturedLobbyMembership membership)
        {
            membership = null;
            if (!localUser.IsValid || string.IsNullOrEmpty(lobbyId) ||
                _currentMembershipOperation <= 0 ||
                _localUser != localUser ||
                !string.Equals(_currentLobbyId, lobbyId, StringComparison.Ordinal))
                return false;
            membership = new CapturedLobbyMembership(
                localUser, lobbyId, this, _currentMembershipOperation);
            return true;
        }

        bool ILobbyMembershipLeaseService.IsCapturedMembershipCurrent(
            CapturedLobbyMembership membership)
        {
            if (membership == null || !ReferenceEquals(membership.Owner, this) ||
                membership.Token is not long operation)
                return false;
            return operation > 0 && operation == _currentMembershipOperation &&
                   EosLobbyOperationOwnership.IsOperationCurrent(operation) &&
                   _localUser == membership.LocalUser &&
                   string.Equals(
                       _currentLobbyId, membership.LobbyId, StringComparison.Ordinal) &&
                   !EosLobbyOperationOwnership.HasNewerCommittedMembership(
                       membership.LocalUser, membership.LobbyId, operation);
        }

        bool ILobbyMembershipLeaseService.TryAcquireCapturedMutation(
            CapturedLobbyMembership membership,
            out CapturedLobbyMutation mutation)
        {
            mutation = null;
            if (membership == null || !ReferenceEquals(membership.Owner, this) ||
                membership.Token is not long operation || operation <= 0 ||
                operation != _currentMembershipOperation ||
                _localUser != membership.LocalUser ||
                !string.Equals(
                    _currentLobbyId, membership.LobbyId, StringComparison.Ordinal) ||
                !EosLobbyOperationOwnership.TryAcquireMembershipMutation(
                    membership.LocalUser, membership.LobbyId, operation,
                    out EosLobbyOperationOwnership.MembershipMutationLease lease))
            {
                return false;
            }

            mutation = new CapturedLobbyMutation(this, lease);
            return true;
        }

        void ILobbyMembershipLeaseService.CompleteCapturedMutation(
            CapturedLobbyMutation mutation)
        {
            if (mutation == null || !ReferenceEquals(mutation.Owner, this) ||
                mutation.Token is not EosLobbyOperationOwnership.MembershipMutationLease lease)
                return;
            EosLobbyOperationOwnership.CompleteMembershipMutation(lease);
        }

        async Task<bool> ILobbyMembershipLeaseService.TransferCapturedMutationToLeaveAsync(
            CapturedLobbyMutation mutation)
        {
            if (mutation == null || !ReferenceEquals(mutation.Owner, this) ||
                mutation.Token is not EosLobbyOperationOwnership.MembershipMutationLease lease ||
                !EosLobbyOperationOwnership.TryTransferMembershipMutationToLeave(
                    lease,
                    EosLobbyOperationOwnership.NextOperation(),
                    out EosLobbyOperationOwnership.PendingLeave pending,
                    out _))
            {
                return false;
            }

            EosLobbyOperationOwnership.SubscribeToLeaveCompletion(
                pending, this, lease.LobbyId);

            var caller = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _ = SubmitPendingLeaveAfterMutationAsync(
                lease.LocalUser,
                lease.LobbyId,
                pending,
                "timed-out-match-mutation-leave",
                caller);

            try
            {
                await caller.Task.ConfigureAwait(true);
                return true;
            }
            catch (Exception e)
            {
                _log?.Invoke(
                    $"[EosLobby] timed-out match mutation leave bounded outcome: {e.Message}");
                // Ownership already moved from mutation to the durable pending-leave fence.
                return true;
            }
        }

        // The hook is integrator code: it runs AFTER the operation result is published and a throw is
        // contained, so it can never strand a committed create/join.
        private void NotifyLobbyEntered(PlayerId localUser, string lobbyId, bool manualAudioInput)
        {
            if (_rtc == null) return;
            try { _rtc.OnLobbyEntered(localUser, lobbyId, manualAudioInput); }
            catch (Exception e) { _log?.Invoke($"[EosLobby] rtc hook threw: {e.GetType().Name}: {e.Message}"); }
        }

        private static LobbyInterface Lobby => EOS.GetPlatformInterface().GetLobbyInterface();
        private static ProductUserId Puid(PlayerId id) => ProductUserId.FromString(id.Value);

        // ---- ILobbyService: lifecycle -------------------------------------------------

        public Task<LobbyResult> CreateAsync(PlayerId localUser, uint maxMembers, LobbyConfig config)
        {
            int gen = _guard.BeginOperation("create-lobby");
            long ownershipOp = EosLobbyOperationOwnership.NextOperation();
            var tcs = new TaskCompletionSource<LobbyResult>();

            // Optional voice: the hook decides whether the lobby gets an RTC room and how its input behaves.
            bool rtcEnabled = _rtc != null && _rtc.EnableRtcRoom;
            bool manualAudioInput = _rtc != null && _rtc.UseManualAudioInput;
            var options = new CreateLobbyOptions
            {
                LocalUserId = Puid(localUser),
                MaxLobbyMembers = maxMembers,
                PermissionLevel = LobbyPermissionLevel.Publicadvertised,
                BucketId = string.IsNullOrEmpty(config.BucketId) ? DefaultBucketId : config.BucketId,
                PresenceEnabled = false,
                AllowInvites = true,
                EnableRTCRoom = rtcEnabled,
            };
            if (rtcEnabled)
            {
                options.LocalRTCOptions = new LocalRTCOptions
                {
                    Flags = (uint)JoinRoomFlags.EnableDatachannel,
                    UseManualAudioInput = manualAudioInput,
                    UseManualAudioOutput = false,
                    LocalAudioDeviceInputStartsMuted = _rtc.LocalAudioInputStartsMuted,
                };
            }

            Lobby.CreateLobby(ref options, null, (ref CreateLobbyCallbackInfo info) =>
            {
                if (info.ResultCode != Result.Success)
                {
                    tcs.SetResult(LobbyResult.Fail(info.ResultCode.ToString()));
                    return;
                }

                // Created on EOS. If the op is stale (teardown/scene-change happened while we
                // waited), abandon it AND leave the orphan lobby so it does not leak.
                if (_guard.IsStale(gen, "create-lobby-result"))
                {
                    _ = SubmitOwnedCleanupWhenSafeAsync(
                        localUser,
                        info.LobbyId,
                        EosLobbyOperationOwnership.NextOperation(),
                        "stale-create");
                    tcs.SetResult(LobbyResult.Fail("stale"));
                    return;
                }

                if (!EosLobbyOperationOwnership.TryCommitMembership(
                        localUser, info.LobbyId, ownershipOp))
                {
                    _ = SubmitOwnedCleanupWhenSafeAsync(
                        localUser,
                        info.LobbyId,
                        EosLobbyOperationOwnership.NextOperation(),
                        "superseded-create");
                    tcs.SetResult(LobbyResult.Fail("stale-ownership"));
                    return;
                }
                _currentLobbyId = info.LobbyId;
                _ownerPuid = localUser;
                _localUser = localUser;
                _currentMembershipOperation = ownershipOp;
                _log?.Invoke($"[EosLobby] created {info.LobbyId}");
                tcs.SetResult(LobbyResult.Success(info.LobbyId, localUser));
                NotifyLobbyEntered(localUser, info.LobbyId, manualAudioInput);
            });

            return tcs.Task;
        }

        public Task<LobbyResult> JoinByIdAsync(PlayerId localUser, string lobbyId)
        {
            int gen = _guard.BeginOperation("join-by-id");
            long ownershipOp = EosLobbyOperationOwnership.NextOperation();
            var tcs = new TaskCompletionSource<LobbyResult>();

            var searchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
            if (Lobby.CreateLobbySearch(ref searchOptions, out LobbySearch search) != Result.Success)
            {
                tcs.SetResult(LobbyResult.Fail("create-search-failed"));
                return tcs.Task;
            }

            var idOptions = new LobbySearchSetLobbyIdOptions { LobbyId = lobbyId };
            if (search.SetLobbyId(ref idOptions) != Result.Success)
            {
                search.Release();
                tcs.SetResult(LobbyResult.Fail("set-lobby-id-failed"));
                return tcs.Task;
            }

            FindThenJoinFirst(search, localUser, gen, ownershipOp, tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Random match: search the bucket for rooms with a free slot, then join the first one that
        /// <see cref="RandomMatchPolicy"/> accepts (not started, same mode, same build). Candidates
        /// are tried in order so a slot lost to a race moves on to the next room instead of failing
        /// the whole match. "not-found" when nothing joinable exists — the caller creates a room.
        /// </summary>
        public Task<LobbyResult> JoinRandomAsync(PlayerId localUser, LobbyConfig config)
        {
            int gen = _guard.BeginOperation("join-random");
            var tcs = new TaskCompletionSource<LobbyResult>();

            var searchOptions = new CreateLobbySearchOptions { MaxResults = RandomMatchMaxResults };
            if (Lobby.CreateLobbySearch(ref searchOptions, out LobbySearch search) != Result.Success)
            {
                tcs.SetResult(LobbyResult.Fail("create-search-failed"));
                return tcs.Task;
            }

            var bucketOptions = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = new AttributeData
                {
                    Key = "bucket",
                    Value = new AttributeDataValue
                    {
                        AsUtf8 = string.IsNullOrEmpty(config.BucketId) ? DefaultBucketId : config.BucketId,
                    },
                },
            };
            if (search.SetParameter(ref bucketOptions) != Result.Success)
            {
                search.Release();
                tcs.SetResult(LobbyResult.Fail("set-parameter-failed"));
                return tcs.Task;
            }
            // Server-side slot filter: a full room never even reaches the candidate list.
            var slotOptions = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Greaterthanorequal,
                Parameter = new AttributeData
                {
                    Key = LobbyInterface.SEARCH_MINSLOTSAVAILABLE,
                    Value = new AttributeDataValue { AsInt64 = 1 },
                },
            };
            if (search.SetParameter(ref slotOptions) != Result.Success)
                _log?.Invoke("[EosLobby] random match: min-slots search parameter rejected — filtering client-side only");

            var findOptions = new LobbySearchFindOptions { LocalUserId = Puid(localUser) };
            search.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo findInfo) =>
            {
                try
                {
                if (findInfo.ResultCode != Result.Success)
                {
                    tcs.SetResult(LobbyResult.Fail(findInfo.ResultCode.ToString()));
                    return;
                }
                var countOptions = new LobbySearchGetSearchResultCountOptions();
                uint count = search.GetSearchResultCount(ref countOptions);
                var candidates = new List<(LobbyDetails details, string lobbyId, ProductUserId owner)>();
                for (uint i = 0; i < count; i++)
                {
                    var copyOptions = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = i };
                    if (search.CopySearchResultByIndex(ref copyOptions, out LobbyDetails details) != Result.Success)
                        continue;
                    var infoOptions = new LobbyDetailsCopyInfoOptions();
                    details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info);
                    var candidate = new RandomMatchPolicy.Candidate(
                        info?.AvailableSlots ?? 0,
                        ReadRoomAttribute(details, LobbyKeys.IsRoomStarted),
                        ReadRoomAttribute(details, LobbyKeys.Mode),
                        ReadRoomAttribute(details, LobbyKeys.Bundle),
                        ReadRoomAttribute(details, LobbyKeys.Private));
                    string lobbyId = info?.LobbyId;
                    if (!RandomMatchPolicy.IsJoinable(candidate, config.ModeLabel, config.Bundle, out string reason))
                    {
                        _log?.Invoke($"[EosLobby] random match: skip {Short(lobbyId)} ({reason})");
                        details.Release();
                        continue;
                    }
                    var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
                    candidates.Add((details, lobbyId, details.GetLobbyOwner(ref ownerOptions)));
                }
                _log?.Invoke($"[EosLobby] random match: {count} room(s) in bucket, {candidates.Count} joinable");
                _ = JoinFirstJoinableAsync(candidates, localUser, gen, tcs);
                }
                finally
                {
                    // Candidates hold their own details handles; the search handle is done.
                    search.Release();
                }
            });
            return tcs.Task;
        }

        private const uint RandomMatchMaxResults = 20;

        private async Task JoinFirstJoinableAsync(
            List<(LobbyDetails details, string lobbyId, ProductUserId owner)> candidates,
            PlayerId localUser, int gen, TaskCompletionSource<LobbyResult> tcs)
        {
            try
            {
            string lastError = "not-found";
            foreach (var c in candidates)
            {
                var attempt = new TaskCompletionSource<LobbyResult>();
                TryJoin(c.details, localUser, c.lobbyId, c.owner, gen,
                    EosLobbyOperationOwnership.NextOperation(), allowLeaveRetry: true, attempt);
                LobbyResult r = await attempt.Task.ConfigureAwait(true);
                if (r.Ok)
                {
                    tcs.TrySetResult(r);
                    return;
                }
                lastError = r.Error ?? "join-failed";
                _log?.Invoke($"[EosLobby] random match: join {Short(c.lobbyId)} failed ({lastError}) — trying next");
                // A stale/cancelled op must not fall through to another room.
                if (lastError == "stale" || lastError == "cancelled")
                    break;
            }
            tcs.TrySetResult(LobbyResult.Fail(candidates.Count == 0 ? "not-found" : lastError));
            }
            finally
            {
                // Every attempt (including its retries) has settled by now; free all candidate handles.
                foreach (var c in candidates)
                    c.details.Release();
            }
        }

        private static string ReadRoomAttribute(LobbyDetails details, string key)
        {
            var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
            if (details.CopyAttributeByKey(ref options, out EosAttribute? attr) != Result.Success || attr?.Data == null)
                return null;
            return attr.Value.Data.Value.Value.AsUtf8;
        }

        private static string Short(string lobbyId)
            => string.IsNullOrEmpty(lobbyId) ? "?" : (lobbyId.Length > 8 ? lobbyId.Substring(0, 8) : lobbyId);

        // Same attribute search as JoinRandomAsync, filtered on the room code instead of the bucket:
        // resolves a typed short code to its lobby, so a player joins a specific room without the
        // 32-char id. The code is an advertised (Public) room attribute (see SetAttributesAsync).
        public Task<LobbyResult> JoinByCodeAsync(PlayerId localUser, string code)
        {
            int gen = _guard.BeginOperation("join-by-code");
            long ownershipOp = EosLobbyOperationOwnership.NextOperation();
            var tcs = new TaskCompletionSource<LobbyResult>();

            var searchOptions = new CreateLobbySearchOptions { MaxResults = 1 };
            if (Lobby.CreateLobbySearch(ref searchOptions, out LobbySearch search) != Result.Success)
            {
                tcs.SetResult(LobbyResult.Fail("create-search-failed"));
                return tcs.Task;
            }

            var paramOptions = new LobbySearchSetParameterOptions
            {
                ComparisonOp = ComparisonOp.Equal,
                Parameter = new AttributeData
                {
                    Key = LobbyKeys.Code,
                    Value = new AttributeDataValue { AsUtf8 = code },
                },
            };
            if (search.SetParameter(ref paramOptions) != Result.Success)
            {
                search.Release();
                tcs.SetResult(LobbyResult.Fail("set-parameter-failed"));
                return tcs.Task;
            }

            FindThenJoinFirst(search, localUser, gen, ownershipOp, tcs);
            return tcs.Task;
        }

        public Task LeaveAsync(PlayerId localUser, string lobbyId)
        {
            var tcs = new TaskCompletionSource<bool>();
            _guard.BeginOperation("leave");
            if (_guard.Invalidated)
            {
                tcs.SetResult(true);
                return tcs.Task;
            }
            long ownershipOp = EosLobbyOperationOwnership.NextOperation();
            if (!EosLobbyOperationOwnership.TryAcquireOwnedLeave(
                    localUser,
                    lobbyId,
                    ownershipOp,
                    out var pending,
                    out _))
            {
                tcs.SetResult(true);
                return tcs.Task;
            }
            EosLobbyOperationOwnership.SubscribeToLeaveCompletion(
                pending, this, lobbyId);
            _ = SubmitPendingLeaveAfterMutationAsync(
                localUser, lobbyId, pending, "leave", tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Create the pending-leave ownership fence now, without dispatching EOS LeaveLobby yet.
        /// Same-PUID/same-lobby entry waits on this exact operation, so a delayed cleanup can never
        /// be created after and accidentally target a newer rejoin.
        /// </summary>
        bool IDeferredLobbyLeaveService.TryReserveLeave(
            PlayerId localUser,
            string lobbyId,
            out DeferredLobbyLeave reservation)
        {
            reservation = null;
            _guard.BeginOperation("reserve-deferred-leave");
            if (_guard.Invalidated)
                return false;

            long ownershipOp = EosLobbyOperationOwnership.NextOperation();
            if (!EosLobbyOperationOwnership.TryBeginOwnedLeave(
                    localUser, lobbyId, ownershipOp, out var pending))
                return false;

            EosLobbyOperationOwnership.SubscribeToLeaveCompletion(
                pending, this, lobbyId);
            reservation = new DeferredLobbyLeave(
                localUser, lobbyId, this,
                new ReservedLeaveToken(ownershipOp, pending));
            return true;
        }

        Task IDeferredLobbyLeaveService.SubmitReservedLeaveAsync(
            DeferredLobbyLeave reservation)
        {
            if (reservation == null || !ReferenceEquals(reservation.Owner, this) ||
                reservation.Token is not ReservedLeaveToken token ||
                !reservation.LocalUser.IsValid || string.IsNullOrEmpty(reservation.LobbyId))
                throw new ArgumentException("invalid deferred leave reservation", nameof(reservation));

            var tcs = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _ = SubmitPendingLeaveAfterMutationAsync(
                reservation.LocalUser,
                reservation.LobbyId,
                token.Pending,
                "deferred-leave",
                tcs);
            return tcs.Task;
        }

        sealed class ReservedLeaveToken
        {
            internal long Operation { get; }
            internal EosLobbyOperationOwnership.PendingLeave Pending { get; }

            internal ReservedLeaveToken(
                long operation, EosLobbyOperationOwnership.PendingLeave pending)
            {
                Operation = operation;
                Pending = pending;
            }
        }

        async Task CompleteCoalescedLeaveAsync(
            EosLobbyOperationOwnership.PendingLeave pending,
            Task timeout,
            TaskCompletionSource<bool> tcs)
        {
            bool? result = await AwaitPendingLeaveForCallerAsync(
                pending, timeout).ConfigureAwait(true);
            if (!result.HasValue)
            {
                tcs.TrySetException(new TimeoutException("leave-callback-timeout"));
                return;
            }
            if (!result.Value)
            {
                tcs.TrySetException(new InvalidOperationException("leave-failed:coalesced"));
                return;
            }
            tcs.TrySetResult(true);
        }

        async Task SubmitPendingLeaveAfterMutationAsync(
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.PendingLeave pending,
            string label,
            TaskCompletionSource<bool> caller)
        {
            try
            {
                await pending.MutationPrerequisite.ConfigureAwait(true);
                if (!EosLobbyOperationOwnership.TryClaimLeaveRecoveryWorker(pending))
                {
                    await CompleteCoalescedLeaveAsync(
                        pending,
                        Task.Delay(TimeSpan.FromSeconds(LeaveCallbackTimeoutSeconds)),
                        caller).ConfigureAwait(true);
                    return;
                }
                await RunClaimedPendingLeaveRecoveryAsync(
                    localUser, lobbyId, pending, label, caller).ConfigureAwait(true);
            }
            catch (Exception e)
            {
                caller.TrySetException(new InvalidOperationException(
                    $"{label}-submit-failed", e));
            }
        }

        async Task RunClaimedPendingLeaveRecoveryAsync(
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.PendingLeave pending,
            string label,
            TaskCompletionSource<bool> caller)
        {
            int retainedWakeupFailures = 0;
            while (true)
            {
                bool recovered;
                try
                {
                    recovered = await RunPendingLeaveRecoveryWorkerAsync(
                        localUser, lobbyId, pending, label).ConfigureAwait(true);
                }
                catch (Exception e)
                {
                    if (EosLobbyOperationOwnership
                            .RetireLeaveRecoveryWorkerOrRetainRequest(pending))
                    {
                        retainedWakeupFailures++;
                        _log?.Invoke(
                            $"[EosLobby] {label} recovery worker retained persistent " +
                            $"wakeup after error: {e.Message}");
                        await DelayPersistentRecoveryAfterErrorAsync(
                                retainedWakeupFailures, label)
                            .ConfigureAwait(true);
                        continue;
                    }
                    caller.TrySetException(new InvalidOperationException(
                        $"{label}-worker-failed", e));
                    return;
                }

                if (recovered)
                {
                    caller.TrySetResult(true);
                    return;
                }
                if (!EosLobbyOperationOwnership
                        .RetireLeaveRecoveryWorkerOrRetainRequest(pending))
                {
                    caller.TrySetException(new InvalidOperationException(
                        $"{label}-recovery-exhausted"));
                    return;
                }
                retainedWakeupFailures = 0;
                _log?.Invoke(
                    $"[EosLobby] {label} recovery worker retained persistent wakeup");
            }
        }

        async Task<bool> RunPendingLeaveRecoveryWorkerAsync(
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.PendingLeave pending,
            string label)
        {
            for (int attempt = 1; attempt <= LeaveRecoveryAttempts; attempt++)
            {
                if (LeaveWorkerBeforeSubmissionOverrideForTests != null)
                {
                    await (LeaveWorkerBeforeSubmissionOverrideForTests() ??
                           Task.CompletedTask).ConfigureAwait(true);
                }
                if (!EosLobbyOperationOwnership.TryClaimLeaveSubmission(pending))
                {
                    if (LeaveWorkerAfterSubmissionClaimFailureOverrideForTests != null)
                    {
                        await (LeaveWorkerAfterSubmissionClaimFailureOverrideForTests() ??
                               Task.CompletedTask).ConfigureAwait(true);
                    }
                    return pending.Completion.Task.IsCompletedSuccessfully &&
                           pending.Completion.Task.Result;
                }

                Task<bool> backend;
                try
                {
                    backend = StartLeaveBackendAsync(localUser, lobbyId) ??
                              Task.FromException<bool>(
                                  new InvalidOperationException("leave-backend-returned-null"));
                }
                catch (Exception e)
                {
                    EosLobbyOperationOwnership.ReleaseLeaveSubmissionClaim(pending);
                    _log?.Invoke(
                        $"[EosLobby] {label} leave submit threw attempt={attempt}: {e.Message}");
                    if (attempt < LeaveRecoveryAttempts)
                        await DelayLeaveRetryAsync(attempt).ConfigureAwait(true);
                    continue;
                }

                Task callbackBound = CreateLeaveCallbackTimeoutTask();
                Task completed = await Task.WhenAny(backend, callbackBound)
                    .ConfigureAwait(true);
                if (completed != backend)
                {
                    // Callback absence is ambiguous: EOS may still apply the leave later. Retain
                    // both the per-attempt claim and durable fence, and never double-submit.
                    bool observerAttached =
                        EosLobbyOperationOwnership.TryAttachLateLeaveObserver(pending);
                    if (observerAttached)
                    {
                        _ = ObserveLatePendingLeaveBackendAsync(
                            backend, localUser, lobbyId, pending, label, attempt);
                    }
                    _log?.Invoke(
                        $"[EosLobby] {label} leave callback timed out attempt={attempt}; " +
                        "durable old-membership fence retained");
                    return false;
                }

                bool succeeded;
                try
                {
                    succeeded = await backend.ConfigureAwait(true);
                }
                catch (Exception e)
                {
                    EosLobbyOperationOwnership.ReleaseLeaveSubmissionClaim(pending);
                    _log?.Invoke(
                        $"[EosLobby] {label} leave task faulted attempt={attempt}: {e.Message}");
                    if (attempt < LeaveRecoveryAttempts)
                        await DelayLeaveRetryAsync(attempt).ConfigureAwait(true);
                    continue;
                }

                if (succeeded)
                {
                    EosLobbyOperationOwnership.CompleteLeave(pending);
                    EosLobbyOperationOwnership.ClearMembershipThrough(
                        localUser, lobbyId, pending.MembershipOperation);
                    return true;
                }

                // A negative callback is not authoritative proof of membership retirement.
                // Preserve the fence and retry the exact captured membership under one worker.
                EosLobbyOperationOwnership.ReleaseLeaveSubmissionClaim(pending);
                _log?.Invoke(
                    $"[EosLobby] {label} leave rejected attempt={attempt}; retrying safely");
                if (attempt < LeaveRecoveryAttempts)
                    await DelayLeaveRetryAsync(attempt).ConfigureAwait(true);
            }

            _log?.Invoke(
                $"[EosLobby] {label} leave recovery exhausted; durable old-membership " +
                "fence remains and a later leave/entry recovery may re-arm it");
            return false;
        }

        Task CreateLeaveCallbackTimeoutTask()
        {
            if (LeaveCallbackTimeoutOverrideForTests != null)
                return LeaveCallbackTimeoutOverrideForTests() ?? Task.CompletedTask;
            return Task.Delay(TimeSpan.FromSeconds(LeaveCallbackTimeoutSeconds));
        }

        async Task ObserveLatePendingLeaveBackendAsync(
            Task<bool> backend,
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.PendingLeave pending,
            string label,
            int attempt)
        {
            bool succeeded;
            try
            {
                succeeded = await backend.ConfigureAwait(true);
            }
            catch (Exception e)
            {
                if (EosLobbyOperationOwnership.RequestRecoveryAfterLateLeaveFailure(
                        pending, out bool ownsRecoveryWorker))
                {
                    _log?.Invoke(
                        $"[EosLobby] {label} late leave task faulted attempt={attempt}: " +
                        $"{e.Message}; re-arming recovery");
                    if (ownsRecoveryWorker)
                    {
                        StartClaimedPendingLeaveRecovery(
                            localUser, lobbyId, pending, $"{label}-late-fault");
                    }
                }
                return;
            }

            if (succeeded)
            {
                if (EosLobbyOperationOwnership.CompleteLateLeaveSuccess(pending))
                {
                    EosLobbyOperationOwnership.ClearMembershipThrough(
                        localUser, lobbyId, pending.MembershipOperation);
                    _log?.Invoke(
                        $"[EosLobby] {label} late leave success retired old membership " +
                        $"attempt={attempt}");
                }
                return;
            }

            if (EosLobbyOperationOwnership.RequestRecoveryAfterLateLeaveFailure(
                    pending, out bool ownsRecoveryWorkerAfterRejection))
            {
                _log?.Invoke(
                    $"[EosLobby] {label} late leave rejected attempt={attempt}; " +
                    "re-arming recovery");
                if (ownsRecoveryWorkerAfterRejection)
                {
                    StartClaimedPendingLeaveRecovery(
                        localUser, lobbyId, pending, $"{label}-late-rejected");
                }
            }
        }

        void StartClaimedPendingLeaveRecovery(
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.PendingLeave pending,
            string label)
        {
            var caller = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _ = RunClaimedPendingLeaveRecoveryAsync(
                localUser, lobbyId, pending, label, caller);
            _ = ObserveEntryLeaveRecoveryAsync(caller.Task, lobbyId);
        }

        Task DelayLeaveRetryAsync(int attempt)
        {
            if (LeaveRetryDelayOverrideForTests != null)
                return LeaveRetryDelayOverrideForTests(attempt) ?? Task.CompletedTask;
            double milliseconds = Math.Min(1000d, 250d * Math.Max(1, attempt));
            return Task.Delay(TimeSpan.FromMilliseconds(milliseconds));
        }

        async Task DelayPersistentRecoveryAfterErrorAsync(int attempt, string label)
        {
            try
            {
                await DelayLeaveRetryAsync(attempt).ConfigureAwait(true);
            }
            catch (Exception e)
            {
                _log?.Invoke(
                    $"[EosLobby] {label} recovery backoff override failed: {e.Message}");
                await Task.Delay(TimeSpan.FromMilliseconds(250)).ConfigureAwait(true);
            }
        }

        Task<bool> StartLeaveBackendAsync(PlayerId localUser, string lobbyId)
        {
            if (LeaveBackendOverrideForTests != null)
                return LeaveBackendOverrideForTests(localUser, lobbyId);
            var callback = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var options = new LeaveLobbyOptions
                { LocalUserId = Puid(localUser), LobbyId = lobbyId };
            Lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
                callback.TrySetResult(info.ResultCode == Result.Success));
            return callback.Task;
        }

        internal static async Task<bool?> AwaitPendingLeaveForCallerAsync(
            EosLobbyOperationOwnership.PendingLeave pending,
            Task timeout)
        {
            if (pending == null) throw new ArgumentNullException(nameof(pending));
            Task<bool> backend = pending.Completion.Task;
            Task completed = await Task.WhenAny(
                backend, timeout ?? Task.CompletedTask).ConfigureAwait(true);
            return completed == backend
                ? await backend.ConfigureAwait(true)
                : null;
        }

        internal void ReconcileCompletedLeave(string lobbyId, long operation)
        {
            if (!ShouldClearMembershipAfterLeave(
                    _currentMembershipOperation <= operation,
                    _currentLobbyId,
                    lobbyId))
            {
                return;
            }
            _currentLobbyId = null;
            _ownerPuid = default;
            _currentMembershipOperation = 0;
        }

        internal void ConfigureMembershipForTests(
            PlayerId localUser, string lobbyId, long operation)
        {
            _localUser = localUser;
            _currentLobbyId = lobbyId;
            _ownerPuid = localUser;
            _currentMembershipOperation = operation;
        }

        /// <summary>
        /// Pure leave-callback policy shared with seam tests. Lobby id equality alone is not
        /// enough: the callback must also own the local membership generation it is clearing.
        /// Backend safety is enforced separately by <see cref="EosLobbyOperationOwnership"/>,
        /// which fences same-room entry until the submitted leave callback completes.
        /// </summary>
        internal static bool ShouldClearMembershipAfterLeave(
            bool ownsLocalMembership, string currentLobbyId, string completedLobbyId)
        {
            return ownsLocalMembership
                && !string.IsNullOrEmpty(completedLobbyId)
                && string.Equals(currentLobbyId, completedLobbyId, StringComparison.Ordinal);
        }

        // ---- Join helpers (ported from the spike, proven W0) --------------------------

        void EnsurePendingLeaveRecoveryForEntry(
            PlayerId localUser,
            string lobbyId,
            long entryOperation)
        {
            Task recovery = BeginPendingLeaveRecoveryForEntry(
                localUser, lobbyId, entryOperation);
            if (recovery != null)
                _ = ObserveEntryLeaveRecoveryAsync(recovery, lobbyId);
        }

        Task BeginPendingLeaveRecoveryForEntry(
            PlayerId localUser,
            string lobbyId,
            long entryOperation)
        {
            EosLobbyOperationOwnership.PendingLeave pending =
                EosLobbyOperationOwnership.FindOlderPendingLeave(
                    localUser, lobbyId, entryOperation);
            if (pending == null)
                return null;
            EosLobbyOperationOwnership.SubscribeToLeaveCompletion(
                pending, this, lobbyId);
            var caller = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _ = SubmitPendingLeaveAfterMutationAsync(
                localUser, lobbyId, pending, "entry-old-membership-recovery", caller);
            return caller.Task;
        }

        internal Task RecoverOlderPendingLeaveForEntryForTests(
            PlayerId localUser,
            string lobbyId,
            long entryOperation) =>
            BeginPendingLeaveRecoveryForEntry(localUser, lobbyId, entryOperation) ??
            Task.CompletedTask;

        async Task ObserveEntryLeaveRecoveryAsync(Task recovery, string lobbyId)
        {
            try
            {
                await recovery.ConfigureAwait(true);
            }
            catch (Exception e)
            {
                _log?.Invoke(
                    $"[EosLobby] entry recovery remains fenced lobby={lobbyId}: {e.Message}");
            }
        }

        private void FindThenJoinFirst(
            LobbySearch search, PlayerId localUser, int gen, long ownershipOp,
            TaskCompletionSource<LobbyResult> tcs)
        {
            var findOptions = new LobbySearchFindOptions { LocalUserId = Puid(localUser) };
            search.Find(ref findOptions, null, (ref LobbySearchFindCallbackInfo findInfo) =>
            {
                try
                {
                if (findInfo.ResultCode != Result.Success)
                {
                    tcs.SetResult(LobbyResult.Fail(findInfo.ResultCode.ToString()));
                    return;
                }

                var countOptions = new LobbySearchGetSearchResultCountOptions();
                if (search.GetSearchResultCount(ref countOptions) == 0)
                {
                    tcs.SetResult(LobbyResult.Fail("not-found"));
                    return;
                }

                var copyOptions = new LobbySearchCopySearchResultByIndexOptions { LobbyIndex = 0 };
                if (search.CopySearchResultByIndex(ref copyOptions, out LobbyDetails details) != Result.Success)
                {
                    tcs.SetResult(LobbyResult.Fail("copy-search-result-failed"));
                    return;
                }

                var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
                ProductUserId owner = details.GetLobbyOwner(ref ownerOptions);

                var infoOptions = new LobbyDetailsCopyInfoOptions();
                details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info);
                string lobbyId = info?.LobbyId;

                TryJoin(
                    details, localUser, lobbyId, owner, gen, ownershipOp,
                    allowLeaveRetry: true, tcs);
                // The join (and its already-exists retry) settles through tcs; free the handle then.
                _ = ReleaseDetailsWhenDoneAsync(tcs.Task, details);
                }
                finally
                {
                    search.Release();
                }
            });
        }

        private static async Task ReleaseDetailsWhenDoneAsync(Task done, LobbyDetails details)
        {
            try { await done.ConfigureAwait(true); }
            catch { /* the caller observes the failure; this only frees the native handle */ }
            finally { details.Release(); }
        }

        private void TryJoin(LobbyDetails details, PlayerId localUser, string lobbyId,
            ProductUserId owner, int gen, long ownershipOp, bool allowLeaveRetry,
            TaskCompletionSource<LobbyResult> tcs,
            EosLobbyOperationOwnership.EntryReservation reservation = null)
        {
            _ = TryJoinAfterPendingLeaveAsync(
                details, localUser, lobbyId, owner, gen, ownershipOp, allowLeaveRetry, tcs,
                reservation);
        }

        async Task TryJoinAfterPendingLeaveAsync(
            LobbyDetails details, PlayerId localUser, string lobbyId,
            ProductUserId owner, int gen, long ownershipOp, bool allowLeaveRetry,
            TaskCompletionSource<LobbyResult> tcs,
            EosLobbyOperationOwnership.EntryReservation reservation)
        {
            reservation ??=
                EosLobbyOperationOwnership.ReserveEntry(localUser, lobbyId, ownershipOp);
            EnsurePendingLeaveRecoveryForEntry(localUser, lobbyId, ownershipOp);
            Task entryFenceDeadline =
                Task.Delay(TimeSpan.FromSeconds(PendingLeaveEntryWaitSeconds));
            bool fenceOpened = await EosLobbyOperationOwnership
                .WaitForOlderLeavesOrCancellationAsync(
                    localUser,
                    lobbyId,
                    ownershipOp,
                    entryFenceDeadline)
                .ConfigureAwait(true);
            if (fenceOpened)
            {
                fenceOpened = await EosLobbyOperationOwnership
                    .WaitForOlderMembershipMutationsOrCancellationAsync(
                        localUser, lobbyId, ownershipOp, entryFenceDeadline)
                    .ConfigureAwait(true);
            }
            if (fenceOpened)
            {
                // A timed-out mutation transfers atomically into a pending-leave fence. Sample
                // leaves again after the mutation wait so entry cannot slip through the handoff.
                fenceOpened = await EosLobbyOperationOwnership
                    .WaitForOlderLeavesOrCancellationAsync(
                        localUser, lobbyId, ownershipOp, entryFenceDeadline)
                    .ConfigureAwait(true);
            }
            if (!fenceOpened)
            {
                EosLobbyOperationOwnership.ReleaseEntryReservation(
                    localUser, lobbyId, reservation, committed: false);
                tcs.TrySetResult(LobbyResult.Fail("pending-membership-mutation"));
                return;
            }
            if (_guard.IsStale(gen, "join-after-pending-leave"))
            {
                EosLobbyOperationOwnership.ReleaseEntryReservation(
                    localUser, lobbyId, reservation, committed: false);
                tcs.TrySetResult(LobbyResult.Fail("stale"));
                return;
            }
            if (!EosLobbyOperationOwnership.CanIssueEntry(
                    localUser, lobbyId, ownershipOp))
            {
                EosLobbyOperationOwnership.ReleaseEntryReservation(
                    localUser, lobbyId, reservation, committed: false);
                tcs.TrySetResult(LobbyResult.Fail("stale-ownership"));
                return;
            }

            try
            {
                // Same optional voice policy as the create path.
                bool rtc = _rtc != null && _rtc.EnableRtcRoom;
                bool manualAudioInput = _rtc != null && _rtc.UseManualAudioInput;
                var joinOptions = new JoinLobbyOptions
                {
                    LobbyDetailsHandle = details,
                    LocalUserId = Puid(localUser),
                    PresenceEnabled = false,
                    // Auto-join the lobby RTC room when voice is enabled.
                    RTCRoomJoinActionType = rtc
                        ? LobbyRTCRoomJoinActionType.AutomaticJoin
                        : LobbyRTCRoomJoinActionType.ManualJoin,
                };
                if (rtc)
                {
                    joinOptions.LocalRTCOptions = new LocalRTCOptions
                    {
                        Flags = (uint)JoinRoomFlags.EnableDatachannel,
                        UseManualAudioInput = manualAudioInput,
                        UseManualAudioOutput = false,
                        LocalAudioDeviceInputStartsMuted = _rtc.LocalAudioInputStartsMuted,
                    };
                }
                var submission = new JoinSubmissionGate();
                Lobby.JoinLobby(ref joinOptions, null, (ref JoinLobbyCallbackInfo joinInfo) =>
                {
                if (!submission.TryAcceptCallback())
                {
                    // The service-side watchdog already retired the reservation. A genuinely late
                    // success still created backend membership, so clean it through the same
                    // ownership protocol; duplicate callbacks after a normal callback do nothing.
                    if (submission.TimedOut &&
                        joinInfo.ResultCode == Result.Success &&
                        lobbyId != null)
                    {
                        _ = SubmitOwnedCleanupWhenSafeAsync(
                            localUser,
                            lobbyId,
                            EosLobbyOperationOwnership.NextOperation(),
                            "late-join-after-timeout");
                    }
                    return;
                }
                // Check staleness before AlreadyExists recovery. An old callback must never submit
                // a raw leave that can remove a newer same-room membership.
                bool stale = _guard.IsStale(gen, "join-result");
                if (stale)
                {
                    EosLobbyOperationOwnership.ReleaseEntryReservation(
                        localUser, lobbyId, reservation, committed: false);
                    if (joinInfo.ResultCode == Result.Success && lobbyId != null)
                        _ = SubmitOwnedCleanupWhenSafeAsync(
                            localUser,
                            lobbyId,
                            EosLobbyOperationOwnership.NextOperation(),
                            "stale-join");
                    tcs.TrySetResult(LobbyResult.Fail("stale"));
                    return;
                }

                // Ungraceful kill leaves this PUID a stale member server-side, so the rejoin is
                // rejected as "already exists". Leave once, then join again (spike-proven, C5).
                if (ShouldAttemptAlreadyExistsRecovery(
                        stale,
                        joinInfo.ResultCode == Result.LobbyLobbyAlreadyExists,
                        allowLeaveRetry,
                        lobbyId))
                {
                    if (TrySubmitOwnedCleanup(
                        localUser,
                        lobbyId,
                        ownershipOp,
                        "already-exists-recovery",
                        () =>
                        {
                            if (_guard.IsStale(gen, "already-exists-retry"))
                            {
                                EosLobbyOperationOwnership.ReleaseEntryReservation(
                                    localUser, lobbyId, reservation, committed: false);
                                tcs.TrySetResult(LobbyResult.Fail("stale"));
                                return;
                            }
                            TryJoin(
                                details, localUser, lobbyId, owner, gen, ownershipOp,
                                allowLeaveRetry: false, tcs, reservation);
                        },
                        error =>
                        {
                            EosLobbyOperationOwnership.ReleaseEntryReservation(
                                localUser, lobbyId, reservation, committed: false);
                            tcs.TrySetResult(LobbyResult.Fail(error));
                        }) != CleanupSubmitResult.Accepted)
                    {
                        EosLobbyOperationOwnership.ReleaseEntryReservation(
                            localUser, lobbyId, reservation, committed: false);
                        tcs.TrySetResult(LobbyResult.Fail("stale-ownership"));
                    }
                    return;
                }

                if (joinInfo.ResultCode != Result.Success)
                {
                    EosLobbyOperationOwnership.ReleaseEntryReservation(
                        localUser, lobbyId, reservation, committed: false);
                    tcs.SetResult(LobbyResult.Fail(joinInfo.ResultCode.ToString()));
                    return;
                }

                if (!EosLobbyOperationOwnership.TryCommitMembership(
                        localUser, lobbyId, ownershipOp))
                {
                    EosLobbyOperationOwnership.ReleaseEntryReservation(
                        localUser, lobbyId, reservation, committed: false);
                    _ = SubmitOwnedCleanupWhenSafeAsync(
                        localUser,
                        lobbyId,
                        EosLobbyOperationOwnership.NextOperation(),
                        "superseded-join");
                    tcs.TrySetResult(LobbyResult.Fail("stale-ownership"));
                    return;
                }
                EosLobbyOperationOwnership.ReleaseEntryReservation(
                    localUser, lobbyId, reservation, committed: true);
                _currentLobbyId = lobbyId;
                _ownerPuid = new PlayerId(owner?.ToString());
                _localUser = localUser;
                _currentMembershipOperation = ownershipOp;
                _log?.Invoke($"[EosLobby] joined {lobbyId}, owner {_ownerPuid}");
                    tcs.SetResult(LobbyResult.Success(lobbyId, _ownerPuid));
                    NotifyLobbyEntered(localUser, lobbyId, manualAudioInput);
                });
                _ = WatchJoinSubmissionAsync(
                    submission,
                    Task.Delay(TimeSpan.FromSeconds(JoinCallbackTimeoutSeconds)),
                    localUser,
                    lobbyId,
                    reservation,
                    tcs);
            }
            catch (Exception e)
            {
                EosLobbyOperationOwnership.ReleaseEntryReservation(
                    localUser, lobbyId, reservation, committed: false);
                tcs.TrySetResult(LobbyResult.Fail($"join-submit-failed:{e.GetType().Name}"));
            }
        }

        async Task WatchJoinSubmissionAsync(
            JoinSubmissionGate submission,
            Task timeout,
            PlayerId localUser,
            string lobbyId,
            EosLobbyOperationOwnership.EntryReservation reservation,
            TaskCompletionSource<LobbyResult> tcs)
        {
            await RunJoinWatchdogAsync(
                submission,
                timeout,
                () =>
                {
                    EosLobbyOperationOwnership.ReleaseEntryReservation(
                        localUser, lobbyId, reservation, committed: false);
                    tcs.TrySetResult(LobbyResult.Fail("join-callback-timeout"));
                }).ConfigureAwait(true);
        }

        internal static async Task<bool> RunJoinWatchdogAsync(
            JoinSubmissionGate submission,
            Task timeout,
            Action onTimeout)
        {
            if (submission == null) throw new ArgumentNullException(nameof(submission));
            if (timeout == null) throw new ArgumentNullException(nameof(timeout));
            Task completed = await Task.WhenAny(
                timeout, submission.Completion).ConfigureAwait(true);
            if (completed != timeout || !submission.TryTimeout())
                return false;
            onTimeout?.Invoke();
            return true;
        }

        internal sealed class JoinSubmissionGate
        {
            // 0 = awaiting EOS, 1 = callback owns completion, 2 = watchdog owns completion.
            int _state;
            readonly TaskCompletionSource<bool> _completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal bool TimedOut => Volatile.Read(ref _state) == 2;
            internal Task Completion => _completion.Task;

            internal bool TryAcceptCallback()
            {
                if (Interlocked.CompareExchange(ref _state, 1, 0) != 0)
                    return false;
                _completion.TrySetResult(true);
                return true;
            }

            internal bool TryTimeout()
            {
                if (Interlocked.CompareExchange(ref _state, 2, 0) != 0)
                    return false;
                _completion.TrySetResult(true);
                return true;
            }
        }

        internal static bool ShouldAttemptAlreadyExistsRecovery(
            bool operationIsStale,
            bool resultIsAlreadyExists,
            bool allowLeaveRetry,
            string lobbyId)
            => !operationIsStale
               && resultIsAlreadyExists
               && allowLeaveRetry
               && !string.IsNullOrEmpty(lobbyId);

        CleanupSubmitResult TrySubmitOwnedCleanup(
            PlayerId localUser,
            string lobbyId,
            long ownershipOp,
            string reason,
            Action onCompleted = null,
            Action<string> onFailed = null,
            Action onSubmitted = null,
            Action<string> onSubmissionRejected = null,
            EosLobbyOperationOwnership.CleanupLease cleanupLease = null)
        {
            bool ownsLeave = cleanupLease == null
                ? EosLobbyOperationOwnership.TryBeginOwnedCleanupLeave(
                    localUser, lobbyId, ownershipOp, out var pending)
                : EosLobbyOperationOwnership.TryBeginCertifiedCleanupLeave(
                    localUser,
                    lobbyId,
                    ownershipOp,
                    cleanupLease,
                    _lifecycleCertificate,
                    out pending);
            if (!ownsLeave)
            {
                _log?.Invoke(
                    $"[EosLobby] skip {reason} leave for newer membership lobby={lobbyId}");
                return CleanupSubmitResult.OwnershipRejected;
            }

            try
            {
                var options = new LeaveLobbyOptions
                    { LocalUserId = Puid(localUser), LobbyId = lobbyId };
                Lobby.LeaveLobby(ref options, null, (ref LeaveLobbyCallbackInfo info) =>
                {
                    if (info.ResultCode != Result.Success)
                    {
                        EosLobbyOperationOwnership.FailLeave(pending);
                        onFailed?.Invoke($"cleanup-leave-failed:{info.ResultCode}");
                        return;
                    }
                    EosLobbyOperationOwnership.CompleteLeave(pending);
                    EosLobbyOperationOwnership.ClearMembershipThrough(
                        localUser, lobbyId, ownershipOp);
                    EosLobbyOperationOwnership.ClearCleanupDebt(
                        localUser, lobbyId, ownershipOp);
                    onCompleted?.Invoke();
                });
                onSubmitted?.Invoke();
                return CleanupSubmitResult.Accepted;
            }
            catch (Exception e)
            {
                EosLobbyOperationOwnership.AbortLeaveSubmission(pending);
                _log?.Invoke($"[EosLobby] {reason} leave submit failed: {e.Message}");
                (onSubmissionRejected ?? onFailed)?.Invoke(
                    "cleanup-leave-submit-failed");
                return CleanupSubmitResult.LocalFailure;
            }
        }

        async Task SubmitOwnedCleanupWhenSafeAsync(
            PlayerId localUser,
            string lobbyId,
            long ownershipOp,
            string reason,
            EosLobbyOperationOwnership.CleanupLease preacquiredLease = null)
        {
            EosLobbyOperationOwnership.CleanupLease transferred = preacquiredLease;
            while (true)
            {
                EosLobbyOperationOwnership.CleanupLease lease;
                bool ownsWorker;
                if (transferred != null)
                {
                    lease = transferred;
                    transferred = null;
                    ownsWorker = true;
                }
                else if (!EosLobbyOperationOwnership.TryAcquireCleanupLease(
                             localUser,
                             lobbyId,
                             ownershipOp,
                             _lifecycleCertificate,
                             out lease,
                             out ownsWorker))
                {
                    await TryQueueCleanupBehindMutationAsync(
                        localUser, lobbyId, ownershipOp, reason).ConfigureAwait(true);
                    return;
                }
                if (!ownsWorker)
                {
                    await lease.Completion.Task.ConfigureAwait(true);
                    return;
                }

                int remaining =
                    EosLobbyOperationOwnership.CleanupAttemptsRemaining(lease);
                if (remaining <= 0)
                {
                    EosLobbyOperationOwnership.CompleteCleanupLease(
                        lease, CleanupRetryOutcome.Exhausted, clearDebt: false);
                    return;
                }
                CleanupRetryOutcome outcome = await RunOwnedCleanupRetryAsync(
                    remaining,
                    async attempt =>
                    {
                        if (!EosLobbyOperationOwnership.IsOperationCurrent(ownershipOp))
                            return CleanupAttemptOutcome.OwnershipLost;
                        await EosLobbyOperationOwnership.WaitForConflictingEntriesAsync(
                            localUser, lobbyId, ownershipOp).ConfigureAwait(true);
                        await EosLobbyOperationOwnership.WaitForConflictingLeavesAsync(
                            localUser, lobbyId, ownershipOp).ConfigureAwait(true);
                        if (!EosLobbyOperationOwnership.IsOperationCurrent(ownershipOp) ||
                            EosLobbyOperationOwnership.HasCommittedMembershipOtherThan(
                                localUser, lobbyId, ownershipOp))
                        {
                            return CleanupAttemptOutcome.OwnershipLost;
                        }

                        var completion = new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                        CleanupSubmitResult submitResult = TrySubmitOwnedCleanup(
                                localUser,
                                lobbyId,
                                ownershipOp,
                                $"{reason}-attempt-{attempt}",
                                () => completion.TrySetResult(true),
                                _ => completion.TrySetResult(false),
                                () => EosLobbyOperationOwnership
                                    .ConsumeCleanupAttemptForAcceptedSubmission(lease),
                                _ => { },
                                cleanupLease: lease);
                        if (submitResult != CleanupSubmitResult.Accepted)
                        {
                            if (submitResult == CleanupSubmitResult.LocalFailure)
                                return CleanupAttemptOutcome.LocalFailure;
                            return !EosLobbyOperationOwnership.IsOperationCurrent(ownershipOp) ||
                                   EosLobbyOperationOwnership.HasCommittedMembershipOtherThan(
                                       localUser, lobbyId, ownershipOp)
                                ? CleanupAttemptOutcome.OwnershipLost
                                : CleanupAttemptOutcome.LocalFailure;
                        }

                        Task<bool> callback = completion.Task;
                        CleanupAttemptOutcome callbackOutcome =
                            await AwaitCleanupCallbackOrCancellationAsync(
                            callback,
                            Task.Delay(TimeSpan.FromSeconds(CleanupCallbackTimeoutSeconds)),
                            lease.Completion.Task).ConfigureAwait(true);
                        if (callbackOutcome == CleanupAttemptOutcome.TimedOut ||
                            callbackOutcome == CleanupAttemptOutcome.OwnershipLost)
                        {
                            _log?.Invoke(
                                $"[EosLobby] cleanup worker released outcome={callbackOutcome} " +
                                $"reason={reason} lobby={lobbyId}");
                            _ = ObserveLateCleanupCallbackAsync(
                                callback,
                                new WeakReference<EosLobbyService>(this),
                                localUser,
                                lobbyId,
                                reason,
                                lease,
                                _lifecycleCertificate);
                        }
                        return callbackOutcome;
                    }).ConfigureAwait(true);

                bool clearDebt =
                    outcome == CleanupRetryOutcome.Succeeded ||
                    outcome == CleanupRetryOutcome.OwnershipLost;
                EosLobbyOperationOwnership.CompleteCleanupLease(
                    lease, outcome, clearDebt);
                if (outcome == CleanupRetryOutcome.Exhausted)
                {
                    _log?.Invoke(
                        $"[EosLobby] cleanup debt retained outcome={outcome} " +
                        $"reason={reason} lobby={lobbyId}");
                }
                return;
            }
        }

        async Task<bool> TryQueueCleanupBehindMutationAsync(
            PlayerId localUser,
            string lobbyId,
            long ownershipOp,
            string reason)
        {
            long leaveOperation = EosLobbyOperationOwnership.NextOperation();
            if (!EosLobbyOperationOwnership.TryQueueLeaveBehindMembershipMutation(
                    localUser,
                    lobbyId,
                    ownershipOp,
                    leaveOperation,
                    out EosLobbyOperationOwnership.PendingLeave pending,
                    out _))
            {
                return false;
            }

            EosLobbyOperationOwnership.SubscribeToLeaveCompletion(
                pending, this, lobbyId);

            var caller = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _ = SubmitPendingLeaveAfterMutationAsync(
                localUser,
                lobbyId,
                pending,
                $"{reason}-queued-behind-match-mutation",
                caller);
            try
            {
                await caller.Task.ConfigureAwait(true);
                return true;
            }
            catch (Exception e)
            {
                _log?.Invoke(
                    $"[EosLobby] queued cleanup bounded outcome reason={reason}: {e.Message}");
                return true;
            }
        }

        internal Task SubmitOwnedCleanupWhenSafeForTests(
            PlayerId localUser, string lobbyId, long ownershipOp)
            => SubmitOwnedCleanupWhenSafeAsync(
                localUser, lobbyId, ownershipOp, "test-cleanup-intent");

        internal static async Task<CleanupAttemptOutcome>
            AwaitCleanupCallbackOrCancellationAsync(
                Task<bool> callback,
                Task timeout,
                Task<CleanupRetryOutcome> leaseCompletion)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (timeout == null) throw new ArgumentNullException(nameof(timeout));
            if (leaseCompletion == null)
                throw new ArgumentNullException(nameof(leaseCompletion));
            Task completed = await Task.WhenAny(
                callback, leaseCompletion, timeout).ConfigureAwait(true);
            if (completed == callback)
            {
                return await callback.ConfigureAwait(true)
                    ? CleanupAttemptOutcome.Succeeded
                    : CleanupAttemptOutcome.Failed;
            }
            if (completed == leaseCompletion)
                return CleanupAttemptOutcome.OwnershipLost;
            return CleanupAttemptOutcome.TimedOut;
        }

        internal static async Task ObserveLateCleanupCallbackAsync(
            Task<bool> callback,
            WeakReference<EosLobbyService> serviceReference,
            PlayerId localUser,
            string lobbyId,
            string reason,
            EosLobbyOperationOwnership.CleanupLease priorLease,
            EosLobbyOperationOwnership.LifecycleCertificate certificate)
        {
            await ObserveLateCleanupResultAsync(
                callback,
                () =>
                {
                    bool serviceIsLive =
                        serviceReference.TryGetTarget(out EosLobbyService service);
                    if (!TryPrepareLateCleanupResume(
                            priorLease,
                            certificate,
                            serviceIsLive,
                            out long resumedOperation,
                            out var resumed))
                        return;
                    _ = service.SubmitOwnedCleanupWhenSafeAsync(
                        localUser,
                        lobbyId,
                        resumedOperation,
                        $"{reason}-late-failure",
                        resumed);
                }).ConfigureAwait(true);
        }

        internal static bool TryPrepareLateCleanupResume(
            EosLobbyOperationOwnership.CleanupLease priorLease,
            EosLobbyOperationOwnership.LifecycleCertificate certificate,
            bool workerAvailable,
            out long resumedOperation,
            out EosLobbyOperationOwnership.CleanupLease resumed)
        {
            return EosLobbyOperationOwnership.TryTransitionLateCleanup(
                priorLease,
                certificate,
                workerAvailable,
                out resumedOperation,
                out resumed);
        }

        internal static async Task ObserveLateCleanupResultAsync(
            Task<bool> callback,
            Action onLateFailure)
        {
            if (!await callback.ConfigureAwait(true))
                onLateFailure?.Invoke();
        }

        internal enum CleanupAttemptOutcome
        {
            Succeeded,
            Failed,
            LocalFailure,
            TimedOut,
            OwnershipLost,
        }

        enum CleanupSubmitResult
        {
            Accepted,
            OwnershipRejected,
            LocalFailure,
        }

        internal enum CleanupRetryOutcome
        {
            Succeeded,
            Exhausted,
            LocalFailure,
            TimedOut,
            OwnershipLost,
        }

        internal static async Task<CleanupRetryOutcome> RunOwnedCleanupRetryAsync(
            int maxAttempts,
            Func<int, Task<CleanupAttemptOutcome>> submitAttempt)
        {
            if (maxAttempts <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxAttempts));
            if (submitAttempt == null)
                throw new ArgumentNullException(nameof(submitAttempt));

            const int maxLocalFailures = 3;
            int acceptedFailures = 0;
            int localFailures = 0;
            int invocation = 0;
            while (acceptedFailures < maxAttempts)
            {
                CleanupAttemptOutcome result =
                    await submitAttempt(++invocation).ConfigureAwait(true);
                switch (result)
                {
                    case CleanupAttemptOutcome.Succeeded:
                        return CleanupRetryOutcome.Succeeded;
                    case CleanupAttemptOutcome.TimedOut:
                        // The submitted backend leave is uncancellable. Do not submit a duplicate;
                        // its late callback still owns the pending fence and clears debt on success.
                        return CleanupRetryOutcome.TimedOut;
                    case CleanupAttemptOutcome.OwnershipLost:
                        return CleanupRetryOutcome.OwnershipLost;
                    case CleanupAttemptOutcome.Failed:
                        acceptedFailures++;
                        localFailures = 0;
                        await Task.Yield();
                        break;
                    case CleanupAttemptOutcome.LocalFailure:
                        localFailures++;
                        if (localFailures >= maxLocalFailures)
                            return CleanupRetryOutcome.LocalFailure;
                        await Task.Yield();
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
            return CleanupRetryOutcome.Exhausted;
        }

        // ---- Kick (host) --------------------------------------------------------------

        /// <summary>
        /// Host-only: remove <paramref name="target"/> from the current lobby (EOS KickMember).
        /// Used for a host-initiated kick (e.g. removing a ghost member that never loaded the match).
        /// </summary>
        public Task<bool> KickMemberAsync(PlayerId localUser, PlayerId target)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (string.IsNullOrEmpty(_currentLobbyId) || !localUser.IsValid || !target.IsValid)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            int gen = _guard.BeginOperation("kick-member");
            var options = new KickMemberOptions
            {
                LobbyId = _currentLobbyId,
                LocalUserId = Puid(localUser),
                TargetUserId = Puid(target),
            };
            Lobby.KickMember(ref options, null, (ref KickMemberCallbackInfo info) =>
            {
                if (_guard.IsStale(gen, "kick-member-result"))
                {
                    tcs.SetResult(false);
                    return;
                }
                bool ok = info.ResultCode == Result.Success;
                if (!ok)
                    _log?.Invoke($"[EosLobby] kick failed: {info.ResultCode}");
                tcs.SetResult(ok);
            });
            return tcs.Task;
        }

        // ---- Attribute write (room + member) ------------------------------------------

        /// <summary>
        /// Apply a batch of attributes to the current lobby in one modification/update.
        /// Each key's room-vs-member scope is taken from <see cref="LobbyKeys"/> / the game schema; values are
        /// written as EOS string attributes. Host-only (EOS rejects non-owner room writes).
        /// </summary>
        public Task<bool> SetAttributesAsync(PlayerId localUser, IEnumerable<KeyValuePair<string, string>> attributes)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            var modOptions = new UpdateLobbyModificationOptions { LocalUserId = Puid(localUser), LobbyId = _currentLobbyId };
            if (Lobby.UpdateLobbyModification(ref modOptions, out LobbyModification mod) != Result.Success)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            foreach (var attr in attributes)
            {
                if (!LobbyKeyScopes.TryResolve(_keySchema, attr.Key, out LobbyAttributeScope scope))
                {
                    // Unknown key: fail closed rather than guess a scope and write it to the wrong lane.
                    _log?.Invoke($"[EosLobby] SetAttributes: key '{attr.Key}' is in neither the protocol keys nor the game schema");
                    mod.Release();
                    tcs.SetResult(false);
                    return tcs.Task;
                }
                var data = new AttributeData { Key = attr.Key, Value = new AttributeDataValue { AsUtf8 = attr.Value } };
                if (scope == LobbyAttributeScope.Member)
                {
                    var memberOpts = new LobbyModificationAddMemberAttributeOptions
                    {
                        Attribute = data,
                        Visibility = LobbyAttributeVisibility.Public,
                    };
                    if (mod.AddMemberAttribute(ref memberOpts) != Result.Success)
                    {
                        mod.Release();
                        tcs.SetResult(false);
                        return tcs.Task;
                    }
                }
                else
                {
                    var roomOpts = new LobbyModificationAddAttributeOptions
                    {
                        Attribute = data,
                        Visibility = LobbyAttributeVisibility.Public,
                    };
                    if (mod.AddAttribute(ref roomOpts) != Result.Success)
                    {
                        mod.Release();
                        tcs.SetResult(false);
                        return tcs.Task;
                    }
                }
            }

            var updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = mod };
            Lobby.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo info) =>
                tcs.SetResult(info.ResultCode == Result.Success));
            mod.Release();
            return tcs.Task;
        }

        /// <summary>
        /// Host: set the room's discoverability. Hidden (Joinviapresence) on match start so a
        /// stranger's Random Match cannot land in a live match; advertised (Publicadvertised) again
        /// on return. Host-only — EOS rejects
        /// a non-owner permission change.
        ///
        /// Joinviapresence rather than Inviteonly is load-bearing and was measured, not assumed: an
        /// Inviteonly lobby rejects a non-member's JoinById with SessionsNotAllowed, which would
        /// block the very rejoin this hides the room for. See <see cref="RoomVisibility"/>.
        /// </summary>
        public Task<bool> SetRoomVisibilityAsync(PlayerId localUser, RoomVisibility visibility)
        {
            var tcs = new TaskCompletionSource<bool>();
            if (string.IsNullOrEmpty(_currentLobbyId))
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            var modOptions = new UpdateLobbyModificationOptions { LocalUserId = Puid(localUser), LobbyId = _currentLobbyId };
            if (Lobby.UpdateLobbyModification(ref modOptions, out LobbyModification mod) != Result.Success)
            {
                tcs.SetResult(false);
                return tcs.Task;
            }

            var permOptions = new LobbyModificationSetPermissionLevelOptions
            {
                PermissionLevel = visibility == RoomVisibility.Advertised
                    ? LobbyPermissionLevel.Publicadvertised
                    : LobbyPermissionLevel.Joinviapresence,
            };
            if (mod.SetPermissionLevel(ref permOptions) != Result.Success)
            {
                mod.Release();
                tcs.SetResult(false);
                return tcs.Task;
            }

            var updateOptions = new UpdateLobbyOptions { LobbyModificationHandle = mod };
            Lobby.UpdateLobby(ref updateOptions, null, (ref UpdateLobbyCallbackInfo info) =>
                tcs.SetResult(info.ResultCode == Result.Success));
            // UpdateLobby has copied the modification handle by the time it returns; release ours.
            mod.Release();
            return tcs.Task;
        }

        // ---- Roster read (ILobbyService) ----------------------------------------------

        /// <summary>
        /// Enumerate the current lobby's members and copy the roster-relevant member
        /// attributes into <see cref="LobbyMemberSnapshot"/>s. Not-yet-replicated attributes
        /// surface as null/empty so the roster waiter treats that member as still settling.
        /// Uses the cached <see cref="_localUser"/> to copy lobby details (host-agnostic read).
        /// </summary>
        public IReadOnlyList<LobbyMemberSnapshot> GetMembers()
        {
            var members = new List<LobbyMemberSnapshot>();
            if (!TryGetDetails(_localUser, out LobbyDetails details))
                return members;

            try
            {
            // Owner is re-read on every poll from the same details handle: EOS may promote a new owner
            // after the host leaves, and a value cached at create/join would go stale (KT bug: cached once).
            var ownerOptions = new LobbyDetailsGetLobbyOwnerOptions();
            ProductUserId currentOwner = details.GetLobbyOwner(ref ownerOptions);
            if (currentOwner != null)
                _ownerPuid = new PlayerId(currentOwner.ToString());

            var countOptions = new LobbyDetailsGetMemberCountOptions();
            uint count = details.GetMemberCount(ref countOptions);
            for (uint i = 0; i < count; i++)
            {
                var indexOptions = new LobbyDetailsGetMemberByIndexOptions { MemberIndex = i };
                ProductUserId puid = details.GetMemberByIndex(ref indexOptions);
                if (puid == null)
                    continue;

                // Attribute decoding (epoch parse, schema keys) lives in
                // LobbyMemberSnapshot.FromAttributes, shared with the test fakes.
                members.Add(LobbyMemberSnapshot.FromAttributes(new PlayerId(puid.ToString()), key =>
                {
                    ReadMemberAttribute(details, puid, key, out string value);
                    return value;
                }, _keySchema));
            }
            }
            finally { details.Release(); }
            return members;
        }

        /// <inheritdoc />
        public bool TryGetMaxMembers(out uint maxMembers)
        {
            maxMembers = 0;
            if (!TryGetDetails(_localUser, out LobbyDetails details))
                return false;

            try
            {
                var infoOptions = new LobbyDetailsCopyInfoOptions();
                if (details.CopyInfo(ref infoOptions, out LobbyDetailsInfo? info) != Result.Success ||
                    info == null)
                    return false;

                maxMembers = info.Value.MaxMembers;
                return maxMembers > 0;
            }
            finally { details.Release(); }
        }

        // ---- Attribute read (room + member) -------------------------------------------

        /// <summary>ILobbyService room read, using the cached local user (see GetMembers).</summary>
        public bool TryGetRoomAttribute(string key, out string value) =>
            TryReadRoomAttribute(_localUser, key, out value);

        public bool TryReadRoomAttribute(PlayerId localUser, string key, out string value)
        {
            value = null;
            if (!TryGetDetails(localUser, out LobbyDetails details))
                return false;

            try
            {
                var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = key };
                if (details.CopyAttributeByKey(ref options, out EosAttribute? attr) != Result.Success || attr?.Data == null)
                    return false;

                value = attr.Value.Data.Value.Value.AsUtf8;
                return true;
            }
            finally { details.Release(); }
        }

        public bool TryReadMemberAttribute(PlayerId localUser, PlayerId member, string key, out string value)
        {
            value = null;
            if (!TryGetDetails(localUser, out LobbyDetails details))
                return false;
            try { return ReadMemberAttribute(details, Puid(member), key, out value); }
            finally { details.Release(); }
        }

        /// <summary>Copy a single member attribute off an already-open details handle.
        /// Returns false (value=null) when the attribute has not replicated yet.</summary>
        private static bool ReadMemberAttribute(LobbyDetails details, ProductUserId member, string key, out string value)
        {
            value = null;
            var options = new LobbyDetailsCopyMemberAttributeByKeyOptions { TargetUserId = member, AttrKey = key };
            if (details.CopyMemberAttributeByKey(ref options, out EosAttribute? attr) != Result.Success || attr?.Data == null)
                return false;

            value = attr.Value.Data.Value.Value.AsUtf8;
            return true;
        }

        private bool TryGetDetails(PlayerId localUser, out LobbyDetails details)
        {
            details = null;
            if (string.IsNullOrEmpty(_currentLobbyId))
                return false;
            var options = new CopyLobbyDetailsHandleOptions { LobbyId = _currentLobbyId, LocalUserId = Puid(localUser) };
            return Lobby.CopyLobbyDetailsHandle(ref options, out details) == Result.Success && details != null;
        }
    }
}
