using System;
using TeamNet.Multiplayer.Core;
using UnityEngine;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// Subsystem-level reset for the static lobby ownership ledger. Statics survive Play Mode when domain
    /// reload is disabled (and any subsystem reset), so committed memberships, entry reservations,
    /// cleanup debts, operation floors and issued lifecycle certificates of the PREVIOUS session would
    /// otherwise leak into the next one and can block or misroute entry.
    ///
    /// The session owner calls <see cref="RegisterActiveGuard"/> with the guard it hands to
    /// <see cref="EosLobbyService"/>. On subsystem registration this invalidates that guard (terminal; a
    /// later BeginOperation cannot reopen it) and then resets the ownership ledger. On normal teardown
    /// the owner calls <c>guard.Invalidate(reason)</c> followed by
    /// <see cref="EosLobbyService.RevokeLifecycleCertificate"/>.
    /// </summary>
    public static class EosLifecycle
    {
        private static GenerationGuard s_activeGuard;

        /// <summary>Remember the guard of the live session so a subsystem reset can invalidate it.</summary>
        public static void RegisterActiveGuard(GenerationGuard guard) => s_activeGuard = guard;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnSubsystemRegistration() => Reset();

        /// <summary>Invalidate the registered guard and reset the ownership ledger. Also callable from tests.</summary>
        public static void Reset()
        {
            s_activeGuard?.Invalidate("eos-subsystem-reset");
            s_activeGuard = null;
            EosLobbyOperationOwnership.ResetLifecycle();
        }
    }
}
