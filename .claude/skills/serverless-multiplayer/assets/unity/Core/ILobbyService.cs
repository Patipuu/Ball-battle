using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>
    /// A point-in-time view of one lobby member, decoupled from EOS <c>HLobbyMember</c>. Protocol fields
    /// (<see cref="Uid"/>, <see cref="LoadEpoch"/>) are first-class; every game-specific member
    /// attribute (display name, skin, ready flag, ...) is in <see cref="Attributes"/>, keyed by the
    /// names the game declared in <see cref="ILobbyKeySchema.MemberSnapshotKeys"/>. A key that has not
    /// replicated yet is simply absent from <see cref="Attributes"/>.
    /// </summary>
    public readonly struct LobbyMemberSnapshot
    {
        private static readonly IReadOnlyDictionary<string, string> NoAttributes =
            new Dictionary<string, string>();

        /// <summary>The member's EOS ProductUserId.</summary>
        public PlayerId Member { get; }
        /// <summary>Game user id (<see cref="LobbyKeys.UserId"/>); empty until replicated.</summary>
        public string Uid { get; }
        /// <summary>The match epoch this member reports having loaded; 0 when unset. The match-loaded
        /// quorum gates on every member's value == MATCH_EPOCH.</summary>
        public int LoadEpoch { get; }
        /// <summary>Game-declared member attributes that have replicated so far.</summary>
        public IReadOnlyDictionary<string, string> Attributes { get; }

        public LobbyMemberSnapshot(PlayerId member, string uid, int loadEpoch = 0,
            IReadOnlyDictionary<string, string> attributes = null)
        {
            Member = member;
            Uid = uid ?? "";
            LoadEpoch = loadEpoch;
            Attributes = attributes ?? NoAttributes;
        }

        /// <summary>Value of a game attribute, or null when it has not replicated.</summary>
        public string Get(string key) =>
            key != null && Attributes.TryGetValue(key, out string value) ? value : null;

        /// <summary>
        /// Builds a snapshot via an accessor (return null for a not-yet-replicated key). Single home for
        /// the string-decoding rules so production EOS reads and test fakes can never drift apart.
        /// </summary>
        public static LobbyMemberSnapshot FromAttributes(
            PlayerId member, Func<string, string> getAttr, ILobbyKeySchema schema = null)
        {
            Dictionary<string, string> extra = null;
            if (schema != null)
            {
                foreach (string key in schema.MemberSnapshotKeys)
                {
                    string value = getAttr(key);
                    if (value == null) continue;
                    extra ??= new Dictionary<string, string>();
                    extra[key] = value;
                }
            }
            return new LobbyMemberSnapshot(
                member,
                getAttr(LobbyKeys.UserId) ?? "",
                int.TryParse(getAttr(LobbyKeys.LoadEpoch), out int epoch) ? epoch : 0,
                extra);
        }

        /// <summary>Loose truthiness for string-encoded flags (member attributes are UTF-8 strings,
        /// so tolerate "1" / case-insensitive "true").</summary>
        public static bool IsTruthyAttribute(string value) =>
            value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Outcome of a lobby operation, decoupled from raw EOS callback shapes so invariant components and
    /// their fakes never touch EOS types.
    /// </summary>
    public readonly struct LobbyResult
    {
        public bool Ok { get; }
        public string LobbyId { get; }
        public PlayerId Owner { get; }
        /// <summary>Human-readable failure reason; null when <see cref="Ok"/> is true.</summary>
        public string Error { get; }

        private LobbyResult(bool ok, string lobbyId, PlayerId owner, string error)
        {
            Ok = ok;
            LobbyId = lobbyId;
            Owner = owner;
            Error = error;
        }

        public static LobbyResult Success(string lobbyId, PlayerId owner) => new(true, lobbyId, owner, null);
        public static LobbyResult Fail(string error) => new(false, null, default, error);
    }

    /// <summary>What a room is opened — and searched for — as.</summary>
    public readonly struct LobbyConfig
    {
        public string BucketId { get; }

        /// <summary>
        /// Privacy intent. The EOS permission level is <c>Publicadvertised</c> either way, so code join
        /// and join-by-id keep working. <c>false</c> means the session owner must write
        /// <c>LobbyKeys.Private = LobbyKeys.PrivateValue(true)</c> in the room's first attribute batch;
        /// <see cref="RandomMatchPolicy"/> then skips the room. The flag persists across matches.
        /// </summary>
        public bool IsPublic { get; }

        /// <summary>Mode label a random-match search must match (<see cref="LobbyKeys.Mode"/>); empty means "any".</summary>
        public string ModeLabel { get; }

        /// <summary>Build identity a random-match search must match (<see cref="LobbyKeys.Bundle"/>); empty means "any".</summary>
        public string Bundle { get; }

        public LobbyConfig(string bucketId, bool isPublic = true, string modeLabel = "", string bundle = "")
        {
            BucketId = bucketId;
            IsPublic = isPublic;
            ModeLabel = modeLabel ?? "";
            Bundle = bundle ?? "";
        }
    }

    /// <summary>
    /// Abstraction over the EOS Lobby. Invariant components depend on this seam rather than on
    /// PlayEveryWare / Epic types directly, which is what makes fake-backed unit tests possible.
    /// </summary>
    public interface ILobbyService
    {
        Task<LobbyResult> CreateAsync(PlayerId localUser, uint maxMembers, LobbyConfig config);
        Task<LobbyResult> JoinByIdAsync(PlayerId localUser, string lobbyId);

        /// <summary>Find and enter an advertised room matching <paramref name="config"/>.</summary>
        Task<LobbyResult> JoinRandomAsync(PlayerId localUser, LobbyConfig config);

        /// <summary>Find and enter the advertised room whose <see cref="LobbyKeys.Code"/> equals <paramref name="code"/>.</summary>
        Task<LobbyResult> JoinByCodeAsync(PlayerId localUser, string code);

        Task LeaveAsync(PlayerId localUser, string lobbyId);

        /// <summary>
        /// Snapshot of the current lobby members. Read-only, synchronous, side-effect-free so a waiter
        /// can poll it across bounded retries while EOS finishes replicating member attributes.
        /// Empty when no lobby is joined.
        /// </summary>
        IReadOnlyList<LobbyMemberSnapshot> GetMembers();

        /// <summary>Live lobby capacity (<c>LobbyDetailsInfo.MaxMembers</c>). False when not in a lobby.</summary>
        bool TryGetMaxMembers(out uint maxMembers);

        /// <summary>
        /// Write a batch of attributes to the current lobby in one update. Each key's room-vs-member scope
        /// comes from <see cref="LobbyKeys"/> / the game's <see cref="ILobbyKeySchema"/>: member keys are
        /// written as the acting user's own member attributes, room keys as lobby attributes (host-only —
        /// EOS rejects non-owner room writes).
        /// </summary>
        Task<bool> SetAttributesAsync(PlayerId localUser, IEnumerable<KeyValuePair<string, string>> attributes);

        /// <summary>
        /// Host-only: set how discoverable the room is. Hidden on match start so a stranger can neither
        /// Random Match into a live match nor read its roster; advertised again on return. A hidden room
        /// stays joinable by room id, which is what lets a former member rejoin.
        /// </summary>
        Task<bool> SetRoomVisibilityAsync(PlayerId localUser, RoomVisibility visibility);

        /// <summary>Read a room (lobby-wide) attribute value. False (value=null) when the key is unset.</summary>
        bool TryGetRoomAttribute(string key, out string value);

        /// <summary>Lobby-owner identity surfaced to the transport as the host address. Default until a lobby is created/joined; implementations must refresh it on every roster poll (EOS can promote a new owner).</summary>
        PlayerId OwnerPuid { get; }
    }
}
