using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// In-memory attribute-backed <see cref="ILobbyService"/> double. Stores room and per-member
    /// attributes so the epoch/phase coordinator can be driven against real state. Test-only seeders
    /// (<see cref="RegisterMember"/>, <see cref="SetMemberAttr"/>) simulate EOS replication arriving;
    /// <see cref="SetAttributesAsync"/> writes the acting user's own member attributes and room
    /// attributes exactly like EOS (scope from <see cref="LobbyKeys"/>).
    /// </summary>
    public sealed class FakeLobbyService : ILobbyService
    {
        private readonly PlayerId _owner;
        private readonly Dictionary<string, string> _roomAttrs = new();
        private readonly List<PlayerId> _members = new();
        private readonly Dictionary<PlayerId, Dictionary<string, string>> _memberAttrs = new();

        public FakeLobbyService(PlayerId owner) => _owner = owner;

        public PlayerId OwnerPuid => _owner;
        public RoomVisibility LastVisibility { get; private set; } = RoomVisibility.Advertised;

        public void RegisterMember(PlayerId member)
        {
            if (_memberAttrs.ContainsKey(member)) return;
            _members.Add(member);
            _memberAttrs[member] = new Dictionary<string, string>();
        }

        public void SetMemberAttr(PlayerId member, string key, string value)
        {
            RegisterMember(member);
            _memberAttrs[member][key] = value;
        }

        public IReadOnlyList<LobbyMemberSnapshot> GetMembers()
        {
            var result = new List<LobbyMemberSnapshot>(_members.Count);
            foreach (PlayerId member in _members)
            {
                Dictionary<string, string> a = _memberAttrs[member];
                result.Add(LobbyMemberSnapshot.FromAttributes(member,
                    key => a.TryGetValue(key, out string value) ? value : null));
            }
            return result;
        }

        public Task<bool> SetAttributesAsync(PlayerId localUser, IEnumerable<KeyValuePair<string, string>> attributes)
        {
            PlayerId actor = localUser.IsValid ? localUser : _owner;
            foreach (KeyValuePair<string, string> attr in attributes)
            {
                if (LobbyKeys.IsMember(attr.Key))
                    SetMemberAttr(actor, attr.Key, attr.Value);
                else
                    _roomAttrs[attr.Key] = attr.Value;
            }
            return Task.FromResult(true);
        }

        public bool TryGetRoomAttribute(string key, out string value) => _roomAttrs.TryGetValue(key, out value);

        public Task<bool> SetRoomVisibilityAsync(PlayerId localUser, RoomVisibility visibility)
        {
            LastVisibility = visibility;
            return Task.FromResult(true);
        }

        public bool TryGetMaxMembers(out uint maxMembers)
        {
            maxMembers = 0;
            return false;
        }

        public Task<LobbyResult> CreateAsync(PlayerId localUser, uint maxMembers, LobbyConfig config) =>
            Task.FromResult(LobbyResult.Success("fake-lobby-1", localUser));

        public Task<LobbyResult> JoinByIdAsync(PlayerId localUser, string lobbyId) =>
            Task.FromResult(LobbyResult.Success(lobbyId, _owner));

        public Task<LobbyResult> JoinRandomAsync(PlayerId localUser, LobbyConfig config) =>
            Task.FromResult(LobbyResult.Success("fake-lobby-1", _owner));

        public Task<LobbyResult> JoinByCodeAsync(PlayerId localUser, string code) =>
            Task.FromResult(LobbyResult.Success("fake-lobby-for-" + code, _owner));

        public Task LeaveAsync(PlayerId localUser, string lobbyId) => Task.CompletedTask;
    }

    /// <summary>
    /// In-memory <see cref="IP2PBoundary"/> double: stable monotonically increasing ClientIds per PUID
    /// and a configurable liveness verdict.
    /// </summary>
    public sealed class FakeP2PBoundary : IP2PBoundary
    {
        private readonly Dictionary<PlayerId, int> _ids = new();
        private int _next = 1;

        public bool Alive { get; set; } = true;
        public PlayerId ConnectedHost { get; set; }
        public bool IsStarted { get; set; } = true;

        public Task<bool> ProbeAliveAsync(PlayerId peer, TimeSpan timeout) => Task.FromResult(Alive);

        public int StableClientIdFor(PlayerId peer)
        {
            if (!_ids.TryGetValue(peer, out int id))
            {
                id = _next++;
                _ids[peer] = id;
            }
            return id;
        }
    }
}
