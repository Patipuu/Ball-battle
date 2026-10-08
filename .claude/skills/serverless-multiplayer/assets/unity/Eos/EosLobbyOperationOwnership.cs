using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamNet.Multiplayer.Core;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// Cross-service ownership for EOS membership operations. It survives session-owner
    /// replacement when domain reload is disabled, so a new service cannot rejoin the same
    /// PUID/lobby while an older backend leave is unresolved.
    /// </summary>
    internal static class EosLobbyOperationOwnership
    {
        internal sealed class LifecycleCertificate
        {
            internal readonly long Epoch;
            internal readonly WeakReference<GenerationGuard> Guard;

            internal LifecycleCertificate(long epoch, GenerationGuard guard)
            {
                Epoch = epoch;
                Guard = new WeakReference<GenerationGuard>(guard);
            }
        }

        internal sealed class EntryReservation
        {
            internal readonly long Operation;
            internal readonly TaskCompletionSource<bool> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal EntryReservation(long operation) => Operation = operation;
        }

        internal sealed class PendingLeave
        {
            internal readonly string Key;
            internal readonly long Operation;
            internal readonly long MembershipOperation;
            internal readonly Task MutationPrerequisite;
            internal bool SubmissionClaimed;
            internal bool RecoveryWorkerClaimed;
            internal bool LateObserverAttached;
            internal bool RecoveryRequested;
            internal readonly TaskCompletionSource<bool> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);
            internal readonly List<LeaveObserver> Observers = new();

            internal PendingLeave(
                string key,
                long operation,
                long membershipOperation,
                Task mutationPrerequisite)
            {
                Key = key;
                Operation = operation;
                MembershipOperation = membershipOperation;
                MutationPrerequisite = mutationPrerequisite ?? Task.CompletedTask;
            }
        }

        internal sealed class LeaveObserver
        {
            internal readonly WeakReference<EosLobbyService> Service;
            internal readonly string LobbyId;

            internal LeaveObserver(EosLobbyService service, string lobbyId)
            {
                Service = new WeakReference<EosLobbyService>(service);
                LobbyId = lobbyId;
            }
        }

        internal sealed class CleanupLease
        {
            internal readonly string Key;
            internal readonly CleanupDebt Debt;
            internal readonly LifecycleCertificate Certificate;
            internal readonly TaskCompletionSource<EosLobbyService.CleanupRetryOutcome> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal long Operation => Debt.Operation;

            internal CleanupLease(
                string key,
                CleanupDebt debt,
                LifecycleCertificate certificate)
            {
                Key = key;
                Debt = debt;
                Certificate = certificate;
            }
        }

        internal sealed class CleanupDebt
        {
            internal long Operation;
            internal int RemainingAttempts;
            internal bool Passive;
            internal bool Adoptable;

            internal CleanupDebt(long operation, int remainingAttempts)
            {
                Operation = operation;
                RemainingAttempts = remainingAttempts;
            }
        }

        internal sealed class MembershipMutationLease
        {
            internal readonly string Key;
            internal readonly long Operation;
            internal readonly PlayerId LocalUser;
            internal readonly string LobbyId;
            internal readonly TaskCompletionSource<bool> Completion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            internal MembershipMutationLease(
                string key, long operation, PlayerId localUser, string lobbyId)
            {
                Key = key;
                Operation = operation;
                LocalUser = localUser;
                LobbyId = lobbyId;
            }
        }

        static readonly object Sync = new();
        static readonly Dictionary<string, long> CommittedMemberships = new();
        static readonly Dictionary<string, List<EntryReservation>> EntryReservations = new();
        static readonly Dictionary<string, List<PendingLeave>> PendingLeaves = new();
        static readonly Dictionary<string, CleanupDebt> CleanupDebts = new();
        static readonly Dictionary<string, CleanupLease> CleanupLeases = new();
        static readonly Dictionary<string, MembershipMutationLease>
            MembershipMutationLeases = new();
        static readonly HashSet<LifecycleCertificate> IssuedCertificates = new();
        static long s_operation;
        static long s_lifecycleEpoch = 1;
        static long s_minAcceptedOperation = 1;

        internal static LifecycleCertificate IssueLifecycleCertificate(
            GenerationGuard guard)
        {
            if (guard == null) throw new ArgumentNullException(nameof(guard));
            lock (Sync)
            {
                if (guard.Invalidated)
                    return null;
                var certificate =
                    new LifecycleCertificate(s_lifecycleEpoch, guard);
                IssuedCertificates.Add(certificate);
                return certificate;
            }
        }

        internal static void RevokeLifecycleCertificate(
            LifecycleCertificate certificate)
        {
            if (certificate == null) return;
            lock (Sync)
                IssuedCertificates.Remove(certificate);
        }

        static bool IsCertificateCurrentLocked(LifecycleCertificate certificate)
        {
            return certificate != null &&
                   IssuedCertificates.Contains(certificate) &&
                   certificate.Epoch == s_lifecycleEpoch &&
                   certificate.Guard.TryGetTarget(out GenerationGuard guard) &&
                   !guard.Invalidated;
        }

        internal static long NextOperation()
        {
            lock (Sync)
            {
                if (!TryAdvanceOperation(s_operation, out long next))
                    throw new InvalidOperationException(
                        "EOS lobby operation sequence exhausted.");
                s_operation = next;
                return next;
            }
        }

        internal static bool TryAdvanceOperation(long current, out long next)
        {
            // long.MaxValue is a permanent unallocatable floor sentinel. Reserving it guarantees
            // ResetLifecycle can always advance past the final real token without signed wrap.
            if (current >= long.MaxValue - 1)
            {
                next = current;
                return false;
            }
            next = current + 1;
            return true;
        }

        internal static long LifecycleEpoch
        {
            get
            {
                lock (Sync)
                    return s_lifecycleEpoch;
            }
        }

        internal static bool IsOperationCurrent(long operation)
        {
            lock (Sync)
                return operation >= s_minAcceptedOperation;
        }

        internal static bool TryCommitMembership(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
            {
                if (operation < s_minAcceptedOperation ||
                    HasNewerReservationLocked(key, operation) ||
                    PendingLeaves.ContainsKey(key) ||
                    MembershipMutationLeases.ContainsKey(key))
                    return false;
                if (!CommittedMemberships.TryGetValue(key, out long current) ||
                    operation >= current)
                {
                    CommittedMemberships[key] = operation;
                    CleanupDebts.Remove(key);
                    if (CleanupLeases.TryGetValue(key, out CleanupLease cleanup))
                    {
                        CleanupLeases.Remove(key);
                        cleanup.Completion.TrySetResult(
                            EosLobbyService.CleanupRetryOutcome.OwnershipLost);
                    }
                    return true;
                }
                return false;
            }
        }

        internal static EntryReservation ReserveEntry(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return null;
            var reservation = new EntryReservation(operation);
            lock (Sync)
            {
                if (operation < s_minAcceptedOperation)
                    return null;
                if (!EntryReservations.TryGetValue(
                        key, out List<EntryReservation> reservations))
                {
                    reservations = new List<EntryReservation>();
                    EntryReservations[key] = reservations;
                }
                reservations.Add(reservation);
            }
            return reservation;
        }

        internal static void ReleaseEntryReservation(
            PlayerId localUser,
            string lobbyId,
            EntryReservation reservation,
            bool committed)
        {
            if (reservation == null ||
                !TryKey(localUser, lobbyId, out string key))
                return;
            lock (Sync)
            {
                if (EntryReservations.TryGetValue(
                        key, out List<EntryReservation> reservations))
                {
                    reservations.Remove(reservation);
                    if (reservations.Count == 0)
                        EntryReservations.Remove(key);
                }
            }
            reservation.Completion.TrySetResult(committed);
        }

        internal static Task WaitForNewerEntriesAsync(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key))
                return Task.CompletedTask;
            lock (Sync)
            {
                if (!EntryReservations.TryGetValue(
                        key, out List<EntryReservation> reservations))
                    return Task.CompletedTask;
                var waits = new List<Task>();
                foreach (EntryReservation reservation in reservations)
                {
                    if (reservation.Operation > operation)
                        waits.Add(reservation.Completion.Task);
                }
                return waits.Count switch
                {
                    0 => Task.CompletedTask,
                    1 => waits[0],
                    _ => Task.WhenAll(waits),
                };
            }
        }

        internal static Task WaitForConflictingEntriesAsync(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key))
                return Task.CompletedTask;
            lock (Sync)
            {
                if (!EntryReservations.TryGetValue(
                        key, out List<EntryReservation> reservations))
                    return Task.CompletedTask;
                var waits = new List<Task>();
                foreach (EntryReservation reservation in reservations)
                {
                    if (reservation.Operation != operation)
                        waits.Add(reservation.Completion.Task);
                }
                return waits.Count switch
                {
                    0 => Task.CompletedTask,
                    1 => waits[0],
                    _ => Task.WhenAll(waits),
                };
            }
        }

        internal static void ClearMembershipThrough(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return;
            lock (Sync)
            {
                if (CommittedMemberships.TryGetValue(key, out long current) &&
                    current <= operation)
                {
                    CommittedMemberships.Remove(key);
                }
            }
        }

        internal static bool HasNewerCommittedMembership(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return CommittedMemberships.TryGetValue(key, out long committed)
                       && committed > operation;
        }

        internal static bool HasCommittedMembershipOtherThan(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return CommittedMemberships.TryGetValue(key, out long committed) &&
                       committed != operation;
        }

        internal static bool TryAcquireMembershipMutation(
            PlayerId localUser,
            string lobbyId,
            long operation,
            out MembershipMutationLease lease)
        {
            lease = null;
            if (!TryKey(localUser, lobbyId, out string key))
                return false;
            lock (Sync)
            {
                // This is the linearization point between stale compensation and every newer
                // same-room admission. The exact captured membership must still be the committed
                // owner, and no entry/leave/cleanup may already own a conflicting mutation.
                if (operation < s_minAcceptedOperation ||
                    !CommittedMemberships.TryGetValue(key, out long committed) ||
                    committed != operation ||
                    EntryReservations.ContainsKey(key) ||
                    PendingLeaves.ContainsKey(key) ||
                    CleanupLeases.ContainsKey(key) ||
                    CleanupDebts.ContainsKey(key) ||
                    MembershipMutationLeases.ContainsKey(key))
                {
                    return false;
                }

                lease = new MembershipMutationLease(
                    key, operation, localUser, lobbyId);
                MembershipMutationLeases[key] = lease;
                return true;
            }
        }

        internal static void CompleteMembershipMutation(MembershipMutationLease lease)
        {
            if (lease == null) return;
            lock (Sync)
            {
                if (MembershipMutationLeases.TryGetValue(
                        lease.Key, out MembershipMutationLease active) &&
                    ReferenceEquals(active, lease))
                {
                    MembershipMutationLeases.Remove(lease.Key);
                }
            }
            lease.Completion.TrySetResult(true);
        }

        internal static bool TryQueueLeaveBehindMembershipMutation(
            PlayerId localUser,
            string lobbyId,
            long expectedMembershipOperation,
            long leaveOperation,
            out PendingLeave pending,
            out bool ownsSubmission)
        {
            pending = null;
            ownsSubmission = false;
            if (!TryKey(localUser, lobbyId, out string key))
                return false;
            lock (Sync)
            {
                if (leaveOperation < s_minAcceptedOperation ||
                    !MembershipMutationLeases.TryGetValue(
                        key, out MembershipMutationLease mutation) ||
                    mutation.Operation != expectedMembershipOperation ||
                    !CommittedMemberships.TryGetValue(key, out long committed) ||
                    committed != expectedMembershipOperation ||
                    HasNewerReservationLocked(key, leaveOperation))
                {
                    return false;
                }
                if (PendingLeaves.TryGetValue(key, out List<PendingLeave> existing) &&
                    existing.Count > 0)
                {
                    pending = existing[0];
                    return true;
                }
                pending = AddPendingLeaveLocked(
                    key, leaveOperation, expectedMembershipOperation,
                    mutation.Completion.Task);
                ownsSubmission = true;
                return true;
            }
        }

        internal static bool TryTransferMembershipMutationToLeave(
            MembershipMutationLease mutation,
            long leaveOperation,
            out PendingLeave pending,
            out bool ownsSubmission)
        {
            pending = null;
            ownsSubmission = false;
            if (mutation == null) return false;
            bool completeMutation = false;
            lock (Sync)
            {
                if (!MembershipMutationLeases.TryGetValue(
                        mutation.Key, out MembershipMutationLease active) ||
                    !ReferenceEquals(active, mutation))
                {
                    return false;
                }
                if (PendingLeaves.TryGetValue(
                        mutation.Key, out List<PendingLeave> existing) &&
                    existing.Count > 0)
                {
                    pending = existing[0];
                }
                else
                {
                    pending = AddPendingLeaveLocked(
                        mutation.Key, leaveOperation, mutation.Operation,
                        mutation.Completion.Task);
                    ownsSubmission = true;
                }
                MembershipMutationLeases.Remove(mutation.Key);
                completeMutation = true;
            }
            if (completeMutation)
                mutation.Completion.TrySetResult(true);
            return true;
        }

        internal static bool TryClaimLeaveSubmission(PendingLeave pending)
        {
            if (pending == null) return false;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || pending.SubmissionClaimed)
                    return false;
                pending.SubmissionClaimed = true;
                // Claiming a real backend attempt consumes any persistent late-failure wakeup.
                pending.RecoveryRequested = false;
                return true;
            }
        }

        internal static bool TryClaimLeaveRecoveryWorker(PendingLeave pending)
        {
            if (pending == null) return false;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || pending.RecoveryWorkerClaimed)
                {
                    return false;
                }
                pending.RecoveryWorkerClaimed = true;
                return true;
            }
        }

        internal static bool RetireLeaveRecoveryWorkerOrRetainRequest(
            PendingLeave pending)
        {
            if (pending == null) return false;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || !pending.RecoveryWorkerClaimed)
                {
                    return false;
                }
                if (pending.RecoveryRequested)
                {
                    // Retention is not consumption. Only a successful real submission claim may
                    // clear this wakeup; pre-submission awaits may still fail after this loop.
                    return true;
                }
                pending.RecoveryWorkerClaimed = false;
                return false;
            }
        }

        internal static void ReleaseLeaveSubmissionClaim(PendingLeave pending)
        {
            if (pending == null) return;
            lock (Sync)
            {
                if (PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) &&
                    leaves.Contains(pending) && !pending.Completion.Task.IsCompleted)
                {
                    pending.SubmissionClaimed = false;
                    pending.LateObserverAttached = false;
                }
            }
        }

        internal static bool TryAttachLateLeaveObserver(PendingLeave pending)
        {
            if (pending == null) return false;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || !pending.SubmissionClaimed ||
                    pending.LateObserverAttached)
                {
                    return false;
                }
                pending.LateObserverAttached = true;
                return true;
            }
        }

        internal static bool RequestRecoveryAfterLateLeaveFailure(
            PendingLeave pending,
            out bool ownsRecoveryWorker)
        {
            ownsRecoveryWorker = false;
            if (pending == null) return false;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || !pending.SubmissionClaimed ||
                    !pending.LateObserverAttached)
                {
                    return false;
                }
                pending.SubmissionClaimed = false;
                pending.LateObserverAttached = false;
                pending.RecoveryRequested = true;
                if (!pending.RecoveryWorkerClaimed)
                {
                    pending.RecoveryWorkerClaimed = true;
                    pending.RecoveryRequested = false;
                    ownsRecoveryWorker = true;
                }
                return true;
            }
        }

        internal static bool CompleteLateLeaveSuccess(PendingLeave pending)
        {
            if (pending == null) return false;
            List<LeaveObserver> observers;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves) ||
                    !leaves.Contains(pending) || !pending.SubmissionClaimed ||
                    !pending.LateObserverAttached)
                {
                    return false;
                }
                leaves.Remove(pending);
                if (leaves.Count == 0)
                    PendingLeaves.Remove(pending.Key);
                pending.SubmissionClaimed = false;
                pending.LateObserverAttached = false;
                pending.RecoveryWorkerClaimed = false;
                pending.RecoveryRequested = false;
                pending.Completion.TrySetResult(true);
                observers = new List<LeaveObserver>(pending.Observers);
                pending.Observers.Clear();
            }
            foreach (LeaveObserver observer in observers)
            {
                if (observer.Service.TryGetTarget(out EosLobbyService service))
                    service.ReconcileCompletedLeave(
                        observer.LobbyId, pending.Operation);
            }
            return true;
        }

        internal static Task WaitForOlderMembershipMutationsAsync(
            PlayerId localUser, string lobbyId, long entryOperation)
        {
            if (!TryKey(localUser, lobbyId, out string key))
                return Task.CompletedTask;
            lock (Sync)
            {
                return MembershipMutationLeases.TryGetValue(
                           key, out MembershipMutationLease lease) &&
                       lease.Operation < entryOperation
                    ? lease.Completion.Task
                    : Task.CompletedTask;
            }
        }

        internal static async Task<bool> WaitForOlderMembershipMutationsOrCancellationAsync(
            PlayerId localUser,
            string lobbyId,
            long entryOperation,
            Task cancellation)
        {
            Task fence = WaitForOlderMembershipMutationsAsync(
                localUser, lobbyId, entryOperation);
            if (fence.IsCompleted)
                return true;
            Task completed = await Task.WhenAny(
                fence, cancellation ?? Task.CompletedTask).ConfigureAwait(false);
            return completed == fence;
        }

        internal static bool TryAcquireCleanupLease(
            PlayerId localUser,
            string lobbyId,
            long operation,
            LifecycleCertificate certificate,
            out CleanupLease lease,
            out bool ownsWorker)
        {
            lease = null;
            ownsWorker = false;
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
            {
                if (!IsCertificateCurrentLocked(certificate) ||
                    operation < s_minAcceptedOperation ||
                    MembershipMutationLeases.ContainsKey(key))
                    return false;
                if (CleanupLeases.TryGetValue(key, out lease))
                    return true;
                if (CleanupDebts.TryGetValue(key, out CleanupDebt existing))
                {
                    if (!existing.Adoptable ||
                        existing.Passive ||
                        existing.RemainingAttempts <= 0)
                    {
                        return false;
                    }
                    existing.Adoptable = false;
                    existing.Operation = operation;
                    lease = new CleanupLease(key, existing, certificate);
                    CleanupLeases[key] = lease;
                    ownsWorker = true;
                    return true;
                }
                var debt = new CleanupDebt(operation, remainingAttempts: 3);
                lease = new CleanupLease(key, debt, certificate);
                CleanupLeases[key] = lease;
                CleanupDebts[key] = debt;
                ownsWorker = true;
                return true;
            }
        }

        internal static bool TryTransitionLateCleanup(
            CleanupLease priorLease,
            LifecycleCertificate certificate,
            bool workerAvailable,
            out long operation,
            out CleanupLease resumed)
        {
            operation = 0;
            resumed = null;
            if (priorLease == null) return false;
            lock (Sync)
            {
                // Exact resume belongs to the certificate that acquired the timed-out lease.
                // Another current service must wait until the original observer atomically makes
                // the debt adoptable, then use TryAcquireCleanupLease as a distinct handoff.
                if (!ReferenceEquals(priorLease.Certificate, certificate))
                    return false;
                if (!workerAvailable || !IsCertificateCurrentLocked(certificate))
                {
                    MakeCleanupDebtAdoptableLocked(priorLease);
                    return false;
                }
                if (CleanupLeases.ContainsKey(priorLease.Key) ||
                    MembershipMutationLeases.ContainsKey(priorLease.Key) ||
                    !CleanupDebts.TryGetValue(
                        priorLease.Key, out CleanupDebt debt) ||
                    !ReferenceEquals(debt, priorLease.Debt))
                {
                    return false;
                }
                if (debt.RemainingAttempts <= 0 || debt.Passive)
                {
                    debt.Passive = true;
                    debt.Adoptable = false;
                    return false;
                }
                if (!TryAdvanceOperation(s_operation, out operation))
                    return false;
                s_operation = operation;
                if (operation < s_minAcceptedOperation ||
                    CleanupLeases.ContainsKey(priorLease.Key) ||
                    MembershipMutationLeases.ContainsKey(priorLease.Key) ||
                    !CleanupDebts.TryGetValue(
                        priorLease.Key, out debt) ||
                    !ReferenceEquals(debt, priorLease.Debt))
                {
                    return false;
                }
                debt.Operation = operation;
                debt.Adoptable = false;
                resumed = new CleanupLease(priorLease.Key, debt, certificate);
                CleanupLeases[priorLease.Key] = resumed;
                return true;
            }
        }

        internal static bool ConsumeCleanupAttemptForAcceptedSubmission(
            CleanupLease lease)
        {
            if (lease == null) return false;
            lock (Sync)
            {
                if (lease.Debt.RemainingAttempts <= 0)
                {
                    lease.Debt.Passive = true;
                    return false;
                }
                lease.Debt.RemainingAttempts--;
                return true;
            }
        }

        internal static int CleanupAttemptsRemaining(CleanupLease lease)
        {
            if (lease == null) return 0;
            lock (Sync)
                return lease.Debt.RemainingAttempts;
        }

        internal static bool CleanupDebtIsPassiveForTests(CleanupLease lease)
        {
            if (lease == null) return false;
            lock (Sync)
                return lease.Debt.Passive;
        }

        internal static bool CleanupDebtIsAdoptableForTests(CleanupLease lease)
        {
            if (lease == null) return false;
            lock (Sync)
                return lease.Debt.Adoptable;
        }

        internal static bool MakeCleanupDebtAdoptable(CleanupLease priorLease)
        {
            if (priorLease == null) return false;
            lock (Sync)
                return MakeCleanupDebtAdoptableLocked(priorLease);
        }

        static bool MakeCleanupDebtAdoptableLocked(CleanupLease priorLease)
        {
            if (priorLease == null) return false;
            if (CleanupLeases.ContainsKey(priorLease.Key) ||
                !CleanupDebts.TryGetValue(
                    priorLease.Key, out CleanupDebt debt) ||
                !ReferenceEquals(debt, priorLease.Debt) ||
                debt.Passive)
            {
                return false;
            }
            if (debt.RemainingAttempts <= 0)
            {
                debt.Passive = true;
                debt.Adoptable = false;
                return true;
            }
            debt.Adoptable = true;
            return true;
        }

        internal static void CompleteCleanupLease(
            CleanupLease lease,
            EosLobbyService.CleanupRetryOutcome outcome,
            bool clearDebt)
        {
            if (lease == null) return;
            lock (Sync)
            {
                if (CleanupLeases.TryGetValue(lease.Key, out CleanupLease current) &&
                    ReferenceEquals(current, lease))
                {
                    CleanupLeases.Remove(lease.Key);
                    if (clearDebt &&
                        CleanupDebts.TryGetValue(
                            lease.Key, out CleanupDebt owner) &&
                        ReferenceEquals(owner, lease.Debt))
                    {
                        CleanupDebts.Remove(lease.Key);
                    }
                    else if (outcome == EosLobbyService.CleanupRetryOutcome.Exhausted)
                    {
                        lease.Debt.Passive = true;
                        lease.Debt.Adoptable = false;
                    }
                    else if (outcome == EosLobbyService.CleanupRetryOutcome.LocalFailure)
                    {
                        lease.Debt.Adoptable = true;
                    }
                }
            }
            lease.Completion.TrySetResult(outcome);
        }

        internal static void RegisterCleanupDebt(
            PlayerId localUser,
            string lobbyId,
            long operation,
            LifecycleCertificate certificate)
        {
            TryAcquireCleanupLease(
                localUser, lobbyId, operation, certificate, out _, out _);
        }

        internal static void ClearCleanupDebt(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return;
            lock (Sync)
            {
                if (CleanupDebts.TryGetValue(key, out CleanupDebt owner) &&
                    owner.Operation == operation)
                    CleanupDebts.Remove(key);
            }
        }

        internal static bool HasCleanupDebt(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return CleanupDebts.TryGetValue(key, out CleanupDebt owner) &&
                       owner.Operation == operation;
        }

        internal static bool CanIssueEntry(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
            {
                if (operation < s_minAcceptedOperation)
                    return false;
                bool newerCommit =
                    CommittedMemberships.TryGetValue(key, out long committed) &&
                    committed > operation;
                return !newerCommit &&
                       !HasNewerReservationLocked(key, operation) &&
                       !HasNewerPendingLeaveLocked(key, operation) &&
                       !MembershipMutationLeases.ContainsKey(key);
            }
        }

        internal static bool TryBeginOwnedLeave(
            PlayerId localUser,
            string lobbyId,
            long operation,
            out PendingLeave pending)
        {
            pending = null;
            if (!TryKey(localUser, lobbyId, out string key))
                return false;

            lock (Sync)
            {
                if (operation < s_minAcceptedOperation ||
                    (CommittedMemberships.TryGetValue(key, out long committed) &&
                     committed > operation) ||
                    HasNewerReservationLocked(key, operation) ||
                    HasConflictingPendingLeaveLocked(key, operation))
                {
                    return false;
                }
                if (MembershipMutationLeases.TryGetValue(
                        key, out MembershipMutationLease mutation))
                {
                    if (!CommittedMemberships.TryGetValue(key, out committed) ||
                        committed != mutation.Operation)
                        return false;
                    pending = AddPendingLeaveLocked(
                        key, operation, mutation.Operation, mutation.Completion.Task);
                    return true;
                }
                pending = AddPendingLeaveLocked(key, operation);
                return true;
            }
        }

        internal static bool TryAcquireOwnedLeave(
            PlayerId localUser,
            string lobbyId,
            long operation,
            out PendingLeave pending,
            out bool ownsSubmission)
        {
            pending = null;
            ownsSubmission = false;
            if (!TryKey(localUser, lobbyId, out string key))
                return false;
            lock (Sync)
            {
                if (operation < s_minAcceptedOperation ||
                    (CommittedMemberships.TryGetValue(key, out long committed) &&
                     committed > operation) ||
                    HasNewerReservationLocked(key, operation))
                {
                    return false;
                }
                if (PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves) &&
                    leaves.Count > 0)
                {
                    pending = leaves[0];
                    return true;
                }
                if (MembershipMutationLeases.TryGetValue(
                        key, out MembershipMutationLease mutation))
                {
                    if (!CommittedMemberships.TryGetValue(key, out committed) ||
                        committed != mutation.Operation)
                        return false;
                    pending = AddPendingLeaveLocked(
                        key, operation, mutation.Operation, mutation.Completion.Task);
                }
                else
                {
                    pending = AddPendingLeaveLocked(key, operation);
                }
                ownsSubmission = true;
                return true;
            }
        }

        internal static bool TryBeginOwnedCleanupLeave(
            PlayerId localUser,
            string lobbyId,
            long operation,
            out PendingLeave pending)
        {
            pending = null;
            if (!TryKey(localUser, lobbyId, out string key))
                return false;

            lock (Sync)
            {
                // Cleanup is compensating a stale success. It must never evict membership accepted
                // by another service/operation, regardless of which operation number is larger.
                if (operation < s_minAcceptedOperation ||
                    HasConflictingReservationLocked(key, operation) ||
                    (CommittedMemberships.TryGetValue(key, out long committed) &&
                     committed != operation) ||
                    HasConflictingPendingLeaveLocked(key, operation) ||
                    MembershipMutationLeases.ContainsKey(key))
                {
                    return false;
                }

                pending = AddPendingLeaveLocked(key, operation);
                return true;
            }
        }

        internal static bool TryBeginCertifiedCleanupLeave(
            PlayerId localUser,
            string lobbyId,
            long operation,
            CleanupLease lease,
            LifecycleCertificate certificate,
            out PendingLeave pending)
        {
            pending = null;
            if (lease == null ||
                !TryKey(localUser, lobbyId, out string key) ||
                key != lease.Key)
            {
                return false;
            }

            lock (Sync)
            {
                if (!IsCertificateCurrentLocked(certificate) ||
                    !ReferenceEquals(lease.Certificate, certificate) ||
                    !CleanupLeases.TryGetValue(key, out CleanupLease active) ||
                    !ReferenceEquals(active, lease) ||
                    operation != lease.Operation ||
                    operation < s_minAcceptedOperation ||
                    HasConflictingReservationLocked(key, operation) ||
                    (CommittedMemberships.TryGetValue(key, out long committed) &&
                     committed != operation) ||
                    HasConflictingPendingLeaveLocked(key, operation) ||
                    MembershipMutationLeases.ContainsKey(key))
                {
                    return false;
                }

                pending = AddPendingLeaveLocked(key, operation);
                return true;
            }
        }

        internal static Task WaitForOlderLeavesAsync(
            PlayerId localUser, string lobbyId, long entryOperation)
        {
            if (!TryKey(localUser, lobbyId, out string key))
                return Task.CompletedTask;

            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves))
                    return Task.CompletedTask;

                var waits = new List<Task>();
                foreach (PendingLeave leave in leaves)
                {
                    if (leave.Operation < entryOperation)
                        waits.Add(leave.Completion.Task);
                }
                return waits.Count switch
                {
                    0 => Task.CompletedTask,
                    1 => waits[0],
                    _ => Task.WhenAll(waits),
                };
            }
        }

        internal static Task WaitForConflictingLeavesAsync(
            PlayerId localUser, string lobbyId, long operation)
        {
            if (!TryKey(localUser, lobbyId, out string key))
                return Task.CompletedTask;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves))
                    return Task.CompletedTask;
                var waits = new List<Task>();
                foreach (PendingLeave leave in leaves)
                {
                    if (leave.Operation != operation)
                        waits.Add(leave.Completion.Task);
                }
                return waits.Count switch
                {
                    0 => Task.CompletedTask,
                    1 => waits[0],
                    _ => Task.WhenAll(waits),
                };
            }
        }

        internal static void SubscribeToLeaveCompletion(
            PendingLeave pending,
            EosLobbyService service,
            string lobbyId)
        {
            if (pending == null || service == null || string.IsNullOrEmpty(lobbyId))
                return;
            bool reconcileNow = false;
            lock (Sync)
            {
                if (pending.Completion.Task.IsCompleted)
                {
                    reconcileNow =
                        pending.Completion.Task.Status == TaskStatus.RanToCompletion &&
                        pending.Completion.Task.Result;
                }
                else
                {
                    bool alreadySubscribed = false;
                    for (int i = pending.Observers.Count - 1; i >= 0; i--)
                    {
                        LeaveObserver observer = pending.Observers[i];
                        if (!observer.Service.TryGetTarget(out EosLobbyService current))
                        {
                            pending.Observers.RemoveAt(i);
                        }
                        else if (ReferenceEquals(current, service))
                        {
                            alreadySubscribed = true;
                        }
                    }
                    if (!alreadySubscribed)
                        pending.Observers.Add(new LeaveObserver(service, lobbyId));
                }
            }
            if (reconcileNow)
                service.ReconcileCompletedLeave(lobbyId, pending.Operation);
        }

        internal static void CompleteLeave(PendingLeave pending)
        {
            if (pending == null) return;
            List<LeaveObserver> observers;
            lock (Sync)
            {
                if (PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves))
                {
                    leaves.Remove(pending);
                    if (leaves.Count == 0)
                        PendingLeaves.Remove(pending.Key);
                }
                pending.SubmissionClaimed = false;
                pending.LateObserverAttached = false;
                pending.RecoveryWorkerClaimed = false;
                pending.RecoveryRequested = false;
                pending.Completion.TrySetResult(true);
                observers = new List<LeaveObserver>(pending.Observers);
                pending.Observers.Clear();
            }
            foreach (LeaveObserver observer in observers)
            {
                if (observer.Service.TryGetTarget(out EosLobbyService service))
                    service.ReconcileCompletedLeave(
                        observer.LobbyId, pending.Operation);
            }
        }

        internal static void FailLeave(PendingLeave pending)
            => RetireLeave(pending, succeeded: false);

        internal static void AbortLeaveSubmission(PendingLeave pending)
            => RetireLeave(pending, succeeded: false);

        static void RetireLeave(PendingLeave pending, bool succeeded)
        {
            if (pending == null) return;
            lock (Sync)
            {
                if (PendingLeaves.TryGetValue(
                        pending.Key, out List<PendingLeave> leaves))
                {
                    leaves.Remove(pending);
                    if (leaves.Count == 0)
                        PendingLeaves.Remove(pending.Key);
                }
                pending.SubmissionClaimed = false;
                pending.LateObserverAttached = false;
                pending.RecoveryWorkerClaimed = false;
                pending.RecoveryRequested = false;
                pending.Completion.TrySetResult(succeeded);
                pending.Observers.Clear();
            }
        }

        internal static async Task<bool> WaitForOlderLeavesOrCancellationAsync(
            PlayerId localUser,
            string lobbyId,
            long entryOperation,
            Task cancellation)
        {
            Task fence = WaitForOlderLeavesAsync(localUser, lobbyId, entryOperation);
            if (fence.IsCompleted)
                return true;
            Task completed = await Task.WhenAny(
                fence, cancellation ?? Task.CompletedTask).ConfigureAwait(false);
            return completed == fence;
        }

        internal static int EntryReservationCountForTests(
            PlayerId localUser, string lobbyId)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return 0;
            lock (Sync)
                return EntryReservations.TryGetValue(
                    key, out List<EntryReservation> reservations)
                    ? reservations.Count
                    : 0;
        }

        internal static bool HasMembershipMutationForTests(
            PlayerId localUser, string lobbyId)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return MembershipMutationLeases.ContainsKey(key);
        }

        internal static PendingLeave PendingLeaveForTests(
            PlayerId localUser, string lobbyId)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return null;
            lock (Sync)
                return PendingLeaves.TryGetValue(
                           key, out List<PendingLeave> leaves) && leaves.Count > 0
                    ? leaves[0]
                    : null;
        }

        internal static PendingLeave FindOlderPendingLeave(
            PlayerId localUser, string lobbyId, long entryOperation)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return null;
            lock (Sync)
            {
                if (!PendingLeaves.TryGetValue(
                        key, out List<PendingLeave> leaves))
                    return null;
                foreach (PendingLeave leave in leaves)
                {
                    if (leave.Operation < entryOperation)
                        return leave;
                }
                return null;
            }
        }

        internal static bool HasLeaveRecoveryWorkerForTests(
            PlayerId localUser, string lobbyId)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return PendingLeaves.TryGetValue(
                           key, out List<PendingLeave> leaves) &&
                       leaves.Exists(leave => leave.RecoveryWorkerClaimed);
        }

        internal static int IssuedCertificateCountForTests()
        {
            lock (Sync)
                return IssuedCertificates.Count;
        }

        internal static void ResetLifecycle()
        {
            List<EntryReservation> reservations = new();
            lock (Sync)
            {
                if (s_lifecycleEpoch == long.MaxValue)
                    throw new InvalidOperationException(
                        "EOS lobby lifecycle sequence exhausted.");
                foreach (List<EntryReservation> entries in EntryReservations.Values)
                    reservations.AddRange(entries);
                EntryReservations.Clear();
                CommittedMemberships.Clear();
                CleanupDebts.Clear();
                foreach (CleanupLease lease in CleanupLeases.Values)
                {
                    lease.Completion.TrySetResult(
                        EosLobbyService.CleanupRetryOutcome.OwnershipLost);
                }
                CleanupLeases.Clear();
                IssuedCertificates.Clear();
                // Admitted leaves (including mutation-queued leaves) and submitted membership
                // mutations are uncancellable ownership decisions. Keep both fences across
                // replacement lifecycles until backend retirement is confirmed; only
                // not-yet-admitted captures are invalidated by the new operation floor.
                s_minAcceptedOperation = s_operation + 1;
                s_lifecycleEpoch++;
            }
            foreach (EntryReservation reservation in reservations)
                reservation.Completion.TrySetResult(false);
        }

        internal static bool HasPendingLeave(
            PlayerId localUser, string lobbyId)
        {
            if (!TryKey(localUser, lobbyId, out string key)) return false;
            lock (Sync)
                return PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves)
                       && leaves.Count > 0;
        }

        internal static void RunWithRegistryLockForTests(Action action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            lock (Sync)
                action();
        }

        internal static void GetOperationStateForTests(
            out long operation,
            out long minAcceptedOperation)
        {
            lock (Sync)
            {
                operation = s_operation;
                minAcceptedOperation = s_minAcceptedOperation;
            }
        }

        internal static void SetOperationStateForTests(
            long operation,
            long minAcceptedOperation)
        {
            lock (Sync)
            {
                s_operation = operation;
                s_minAcceptedOperation = minAcceptedOperation;
            }
        }

        static bool HasNewerReservationLocked(string key, long operation)
        {
            if (!EntryReservations.TryGetValue(
                    key, out List<EntryReservation> reservations))
                return false;
            foreach (EntryReservation reservation in reservations)
            {
                if (reservation.Operation > operation)
                    return true;
            }
            return false;
        }

        static bool HasConflictingReservationLocked(string key, long operation)
        {
            if (!EntryReservations.TryGetValue(
                    key, out List<EntryReservation> reservations))
                return false;
            foreach (EntryReservation reservation in reservations)
            {
                if (reservation.Operation != operation)
                    return true;
            }
            return false;
        }

        static bool HasNewerPendingLeaveLocked(string key, long operation)
        {
            if (!PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves))
                return false;
            foreach (PendingLeave leave in leaves)
            {
                if (leave.Operation > operation)
                    return true;
            }
            return false;
        }

        static bool HasConflictingPendingLeaveLocked(string key, long operation)
        {
            if (!PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves))
                return false;
            foreach (PendingLeave leave in leaves)
            {
                if (leave.Operation != operation)
                    return true;
            }
            return false;
        }

        static PendingLeave AddPendingLeaveLocked(
            string key,
            long operation,
            long membershipOperation = 0,
            Task mutationPrerequisite = null)
        {
            if (membershipOperation <= 0 &&
                CommittedMemberships.TryGetValue(key, out long committed))
                membershipOperation = committed;
            if (membershipOperation <= 0)
                membershipOperation = operation;
            var pending = new PendingLeave(
                key, operation, membershipOperation, mutationPrerequisite);
            if (!PendingLeaves.TryGetValue(key, out List<PendingLeave> leaves))
            {
                leaves = new List<PendingLeave>();
                PendingLeaves[key] = leaves;
            }
            leaves.Add(pending);
            return pending;
        }

        static bool TryKey(PlayerId localUser, string lobbyId, out string key)
        {
            key = null;
            if (!localUser.IsValid || string.IsNullOrEmpty(lobbyId))
                return false;
            key = localUser.Value + "\n" + lobbyId;
            return true;
        }
    }
}
