using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace TeamNet.Multiplayer.Core
{
    /// <summary>Where an EOS lobby attribute lives: room-wide vs per-member.</summary>
    public enum LobbyAttributeScope
    {
        /// <summary>Set via EOS <c>AddAttribute</c> — host-owned, one value per lobby.</summary>
        Room,
        /// <summary>Set via EOS <c>AddMemberAttribute</c> — one value per member.</summary>
        Member,
    }

    /// <summary>
    /// The PROTOCOL keys the match choreography depends on (epoch / phase / ledger / code). These are
    /// the same for every game. Game-specific keys (display name, skin, level, ...) are NOT here: the
    /// game declares them through <see cref="ILobbyKeySchema"/>.
    /// </summary>
    public static class LobbyKeys
    {
        // --- Room attributes (host-authored) ---
        public const string Mode = "mode";                          // room mode label; random-match filter
        public const string Bundle = "bundle";                      // build identity; random-match filter
        public const string IsRoomStarted = "is_room_started";      // "1" while a match runs
        public const string RoomPhase = "room_phase";               // value: Phase.Lobby | Phase.InMatch
        public const string MatchEpoch = "match_epoch";             // room-wide match counter (host bumps)
        public const string JoinOrderMapping = "join_order_mapping"; // JSON {userId:order}, see JoinOrderMapping
        public const string Code = "code";                          // short typeable join code (join-by-code)
        public const string FormerMembers = "former_members";       // epoch-stamped CSV of admitted participant PUIDs
        public const string Private = "private";                    // "1" = hidden from random match, still joinable by code/id

        // --- Member attributes (each peer authors its own) ---
        public const string UserId = "user_id";                     // game user id; empty until replicated
        public const string ClientPhase = "client_phase";           // value: Phase.Lobby | Phase.InMatch
        public const string LoadEpoch = "load_epoch";               // per-member: epoch this client has loaded

        /// <summary>Encodes a room's privacy as the value of <see cref="Private"/>.</summary>
        public static string PrivateValue(bool isPrivate) => isPrivate ? "1" : "0";

        /// <summary>Values for <see cref="RoomPhase"/> / <see cref="ClientPhase"/>.</summary>
        public static class Phase
        {
            public const string Lobby = "LOBBY";
            public const string InMatch = "IN_MATCH";
        }

        private static readonly Dictionary<string, LobbyAttributeScope> _scope = new()
        {
            [Mode] = LobbyAttributeScope.Room,
            [Bundle] = LobbyAttributeScope.Room,
            [IsRoomStarted] = LobbyAttributeScope.Room,
            [RoomPhase] = LobbyAttributeScope.Room,
            [MatchEpoch] = LobbyAttributeScope.Room,
            [JoinOrderMapping] = LobbyAttributeScope.Room,
            [Code] = LobbyAttributeScope.Room,
            [FormerMembers] = LobbyAttributeScope.Room,
            [Private] = LobbyAttributeScope.Room,

            [UserId] = LobbyAttributeScope.Member,
            [ClientPhase] = LobbyAttributeScope.Member,
            [LoadEpoch] = LobbyAttributeScope.Member,
        };

        public static IReadOnlyDictionary<string, LobbyAttributeScope> All => _scope;
        public static bool IsProtocolKey(string key) => key != null && _scope.ContainsKey(key);
        public static LobbyAttributeScope ScopeOf(string key) => _scope[key];
        public static bool IsRoom(string key) => _scope[key] == LobbyAttributeScope.Room;
        public static bool IsMember(string key) => _scope[key] == LobbyAttributeScope.Member;
    }

    /// <summary>
    /// Game-supplied declaration of the lobby attributes beyond <see cref="LobbyKeys"/>. The lobby
    /// adapter asks it (a) which scope a key is written to and (b) which member keys to copy into
    /// <see cref="LobbyMemberSnapshot.Attributes"/> when the roster is read.
    /// </summary>
    public interface ILobbyKeySchema
    {
        /// <summary>Scope of a GAME key; false when the key is unknown to the game.</summary>
        bool TryGetScope(string key, out LobbyAttributeScope scope);

        /// <summary>Game member keys copied into every snapshot (display name, skin, ready flag, ...).</summary>
        IReadOnlyList<string> MemberSnapshotKeys { get; }
    }

    /// <summary>Schema with no game keys: protocol keys only.</summary>
    public sealed class EmptyLobbyKeySchema : ILobbyKeySchema
    {
        public static readonly EmptyLobbyKeySchema Instance = new();
        private static readonly string[] None = new string[0];

        public bool TryGetScope(string key, out LobbyAttributeScope scope)
        {
            scope = default;
            return false;
        }

        public IReadOnlyList<string> MemberSnapshotKeys => None;
    }

    /// <summary>Resolves an attribute key to a scope: protocol keys first, then the game schema.</summary>
    public static class LobbyKeyScopes
    {
        public static bool TryResolve(ILobbyKeySchema schema, string key, out LobbyAttributeScope scope)
        {
            if (key != null && LobbyKeys.All.TryGetValue(key, out scope))
                return true;
            scope = default;
            return schema != null && key != null && schema.TryGetScope(key, out scope);
        }
    }

    /// <summary>
    /// EOS lobby limits (from <c>LobbyInterface</c> constants) and budget checks.
    /// <see cref="MaxAttributeNameLength"/> caps the attribute NAME, not the value.
    /// </summary>
    public static class LobbyBudget
    {
        public const int MaxAttributes = 64;          // EOS LOBBYMODIFICATION_MAX_ATTRIBUTES
        public const int MaxAttributeNameLength = 64;  // EOS LOBBYMODIFICATION_MAX_ATTRIBUTE_LENGTH (name)

        public static bool KeyNameWithinLimit(string key)
            => !string.IsNullOrEmpty(key) && key.Length <= MaxAttributeNameLength;

        public static bool AttributeCountWithinLimit(int count) => count <= MaxAttributes;
    }

    /// <summary>
    /// The <see cref="LobbyKeys.FormerMembers"/> payload: the set of PUIDs admitted as participants of ONE
    /// match, stamped with that match's epoch — <c>"&lt;epoch&gt;|&lt;puid&gt;,&lt;puid&gt;,..."</c>.
    ///
    /// The match barriers scope to this set, so a peer that merely joined the EOS lobby (a stranger holding the
    /// room code) cannot hold the load/return quorum false. It is written as a **snapshot at match start** and
    /// rewritten for each new match; <see cref="Append"/> exists for admitting a rejoiner mid-match —
    /// the session owner calls it when <see cref="RejoinAdmission"/> lets a late peer in.
    ///
    /// The epoch stamp is what makes a leftover ledger safe: a value from a previous match no longer matches the
    /// current <see cref="LobbyKeys.MatchEpoch"/>, so readers treat it as absent and fall back to counting every
    /// replicated member rather than governing the wrong roster. PUIDs are alphanumeric, so no escaping is needed.
    /// </summary>
    public static class FormerMembers
    {
        /// <summary>Serialize an epoch-stamped ledger. Null/empty and duplicate PUIDs are skipped.</summary>
        public static string Serialize(int epoch, IEnumerable<string> puids)
        {
            var seen = new HashSet<string>();
            var sb = new StringBuilder();
            sb.Append(epoch.ToString(CultureInfo.InvariantCulture)).Append('|');
            bool first = true;
            foreach (string p in puids)
            {
                if (string.IsNullOrEmpty(p) || !seen.Add(p)) continue;
                if (!first) sb.Append(',');
                sb.Append(p);
                first = false;
            }
            return sb.ToString();
        }

        /// <summary>
        /// Parse an epoch-stamped ledger. False (and an empty set) for null/empty, a malformed stamp, or a
        /// ledger carrying no PUIDs — every one of which a caller must treat as "no ledger" and fall back.
        /// </summary>
        public static bool TryParse(string value, out int epoch, out HashSet<string> puids)
        {
            epoch = 0;
            puids = new HashSet<string>();
            if (string.IsNullOrEmpty(value))
                return false;

            int bar = value.IndexOf('|');
            if (bar <= 0)
                return false;
            if (!int.TryParse(value.Substring(0, bar), NumberStyles.Integer, CultureInfo.InvariantCulture, out epoch))
                return false;

            foreach (string p in value.Substring(bar + 1).Split(','))
                if (!string.IsNullOrEmpty(p))
                    puids.Add(p);
            return puids.Count > 0;
        }

        /// <summary>
        /// Union an existing ledger with more PUIDs for <paramref name="epoch"/>, re-serialized. A ledger from a
        /// different epoch is discarded rather than carried forward. For mid-match rejoin admission.
        /// </summary>
        public static string Append(string existingValue, int epoch, IEnumerable<string> puids)
        {
            HashSet<string> set = TryParse(existingValue, out int existingEpoch, out HashSet<string> existing)
                && existingEpoch == epoch
                    ? existing
                    : new HashSet<string>();
            foreach (string p in puids)
                if (!string.IsNullOrEmpty(p))
                    set.Add(p);
            return Serialize(epoch, set);
        }
    }

    /// <summary>
    /// The <see cref="LobbyKeys.JoinOrderMapping"/> payload: a dense, stable
    /// user-id → join-order map. Same algorithm as the original Godot <c>LobbyLogic.compute_join_orders</c>:
    /// members already known keep their relative order, newcomers append, then the whole
    /// set is re-densified 0..N-1 so a mid-session leave never leaves a gap.
    /// </summary>
    public static class JoinOrderMapping
    {
        /// <summary>
        /// Recompute the mapping. Known uids keep their prior relative order; fresh uids
        /// append; result is dense 0..N-1. Null/empty and duplicate uids are skipped.
        /// </summary>
        public static Dictionary<string, int> Compute(
            IReadOnlyDictionary<string, int> existing, IEnumerable<string> currentUserIds)
        {
            var known = new List<string>();
            var fresh = new List<string>();
            var seen = new HashSet<string>();

            foreach (var uid in currentUserIds)
            {
                if (string.IsNullOrEmpty(uid) || !seen.Add(uid))
                    continue; // defensive: empty or duplicate uid must not break density
                if (existing != null && existing.ContainsKey(uid))
                    known.Add(uid);
                else
                    fresh.Add(uid);
            }

            known.Sort((a, b) => existing[a].CompareTo(existing[b]));

            var result = new Dictionary<string, int>();
            int order = 0;
            foreach (var uid in known) result[uid] = order++;
            foreach (var uid in fresh) result[uid] = order++;
            return result;
        }

        /// <summary>Join order for a uid, or <paramref name="fallback"/> (default 999, matching Godot) if absent.</summary>
        public static int GetJoinOrder(IReadOnlyDictionary<string, int> mapping, string uid, int fallback = 999)
            => mapping != null && mapping.TryGetValue(uid, out int order) ? order : fallback;

        /// <summary>
        /// Serialize to <c>{"uid":order,...}</c>. UID keys are EOS PUIDs / game user-ids
        /// (alphanumeric, no escaping needed) and values are non-negative ints, so this is a
        /// deliberately minimal serializer for exactly this shape — not a general JSON writer.
        /// </summary>
        public static string Serialize(IReadOnlyDictionary<string, int> mapping)
        {
            var sb = new StringBuilder("{");
            bool first = true;
            foreach (var kv in mapping)
            {
                if (!first) sb.Append(',');
                first = false;
                sb.Append('"').Append(kv.Key).Append("\":").Append(kv.Value);
            }
            return sb.Append('}').ToString();
        }

        /// <summary>
        /// Parse the shape produced by <see cref="Serialize"/>. Not a general JSON parser;
        /// tolerates surrounding whitespace, returns empty for null/empty/"{}".
        /// </summary>
        public static Dictionary<string, int> Deserialize(string json)
        {
            var result = new Dictionary<string, int>();
            if (string.IsNullOrEmpty(json))
                return result;

            string trimmed = json.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                return result;

            string body = trimmed.Substring(1, trimmed.Length - 2).Trim();
            if (body.Length == 0)
                return result;

            foreach (string entry in body.Split(','))
            {
                int colon = entry.LastIndexOf(':');
                if (colon < 0)
                    continue;
                string key = entry.Substring(0, colon).Trim().Trim('"');
                if (int.TryParse(entry.Substring(colon + 1).Trim(), out int order))
                    result[key] = order;
            }
            return result;
        }
    }
}
