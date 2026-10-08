using System.Threading.Tasks;
using TeamNet.Multiplayer.Core;

namespace TeamNet.Multiplayer.Eos
{
    /// <summary>
    /// Ownership token for a leave that is fenced before its backend submission. The token is
    /// intentionally opaque outside the lobby adapter: production carries the exact EOS pending
    /// leave/operation, while tests carry the equivalent membership generation.
    /// </summary>
    public sealed class DeferredLobbyLeave
    {
        public PlayerId LocalUser { get; }
        public string LobbyId { get; }
        public object Owner { get; }
        public object Token { get; }

        public DeferredLobbyLeave(PlayerId localUser, string lobbyId, object owner, object token)
        {
            LocalUser = localUser;
            LobbyId = lobbyId;
            Owner = owner;
            Token = token;
        }
    }

    /// <summary>
    /// Optional production leave fence used when another uncancellable lobby mutation must drain
    /// first. Reserving creates the same-PUID/same-lobby pending-leave barrier immediately;
    /// submitting later must use that exact token rather than targeting whatever membership is
    /// current at submission time.
    /// </summary>
    public interface IDeferredLobbyLeaveService
    {
        bool TryReserveLeave(PlayerId localUser, string lobbyId, out DeferredLobbyLeave reservation);

        Task SubmitReservedLeaveAsync(DeferredLobbyLeave reservation);
    }

    /// <summary>Opaque certificate for cleanup that may outlive the session component that created
    /// the membership. The lobby adapter validates that the exact captured backend membership still
    /// owns the room before granting a scoped mutation lease.</summary>
    public sealed class CapturedLobbyMembership
    {
        public PlayerId LocalUser { get; }
        public string LobbyId { get; }
        public object Owner { get; }
        public object Token { get; }

        public CapturedLobbyMembership(PlayerId localUser, string lobbyId, object owner, object token)
        {
            LocalUser = localUser;
            LobbyId = lobbyId;
            Owner = owner;
            Token = token;
        }
    }

    /// <summary>
    /// Opaque ownership lease for one compensating room mutation. The production adapter holds
    /// same-PUID/same-lobby entry behind this lease until the submitted callback and its bounded
    /// confirmation retire, closing the capture-validation-to-write race.
    /// </summary>
    public sealed class CapturedLobbyMutation
    {
        public object Owner { get; }
        public object Token { get; }

        public CapturedLobbyMutation(object owner, object token)
        {
            Owner = owner;
            Token = token;
        }
    }

    public interface ILobbyMembershipLeaseService
    {
        bool TryCaptureMembership(PlayerId localUser, string lobbyId, out CapturedLobbyMembership membership);

        bool IsCapturedMembershipCurrent(CapturedLobbyMembership membership);

        bool TryAcquireCapturedMutation(CapturedLobbyMembership membership, out CapturedLobbyMutation mutation);

        void CompleteCapturedMutation(CapturedLobbyMutation mutation);

        /// <summary>
        /// Atomically replaces a timed-out mutation lease with an exact old-membership leave
        /// fence, then drives that leave to its bounded caller outcome. The fence itself survives
        /// a missing backend callback.
        /// </summary>
        Task<bool> TransferCapturedMutationToLeaveAsync(CapturedLobbyMutation mutation);
    }
}
