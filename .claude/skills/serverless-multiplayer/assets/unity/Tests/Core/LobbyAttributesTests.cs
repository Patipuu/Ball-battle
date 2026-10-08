using System.Collections.Generic;
using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// Unit tests for the lobby-attribute contract: room/member scope tagging, the
    /// dense-stable join-order algorithm, JSON round-trip, and the EOS size budget incl.
    /// a max-player (4) mapping. Pure EditMode; no device/EOS.
    /// </summary>
    public class LobbyAttributesTests
    {
        [Test]
        public void Scope_tagging_matches_room_vs_member_setters()
        {
            // Room (host-authored lobby attribute)
            Assert.IsTrue(LobbyKeys.IsRoom(LobbyKeys.MatchEpoch), "MATCH_EPOCH is room-wide");
            Assert.IsTrue(LobbyKeys.IsRoom(LobbyKeys.JoinOrderMapping));
            Assert.IsTrue(LobbyKeys.IsRoom(LobbyKeys.RoomPhase));
            Assert.IsTrue(LobbyKeys.IsRoom(LobbyKeys.FormerMembers));
            // Member (each peer writes its own)
            Assert.IsTrue(LobbyKeys.IsMember(LobbyKeys.LoadEpoch), "LOAD_EPOCH is per-member");
            Assert.IsTrue(LobbyKeys.IsMember(LobbyKeys.ClientPhase));
            Assert.IsTrue(LobbyKeys.IsMember(LobbyKeys.UserId));
        }

        [Test]
        public void Scope_registry_has_the_expected_room_and_member_counts()
        {
            int room = 0, member = 0;
            foreach (var scope in LobbyKeys.All.Values)
            {
                if (scope == LobbyAttributeScope.Room) room++;
                else member++;
            }
            Assert.AreEqual(9, room, "9 protocol room attributes");
            Assert.AreEqual(3, member, "3 protocol member attributes");
        }

        private sealed class GameSchema : ILobbyKeySchema
        {
            public bool TryGetScope(string key, out LobbyAttributeScope scope)
            {
                switch (key)
                {
                    case "level": scope = LobbyAttributeScope.Room; return true;
                    case "skin": scope = LobbyAttributeScope.Member; return true;
                    default: scope = default; return false;
                }
            }

            public IReadOnlyList<string> MemberSnapshotKeys { get; } = new[] { "skin", "nick" };
        }

        [Test]
        public void Key_scopes_resolve_protocol_keys_first_then_the_game_schema()
        {
            var schema = new GameSchema();
            Assert.IsTrue(LobbyKeyScopes.TryResolve(schema, LobbyKeys.MatchEpoch, out var s1));
            Assert.AreEqual(LobbyAttributeScope.Room, s1);
            Assert.IsTrue(LobbyKeyScopes.TryResolve(schema, "skin", out var s2));
            Assert.AreEqual(LobbyAttributeScope.Member, s2);
            Assert.IsFalse(LobbyKeyScopes.TryResolve(schema, "unknown", out _), "unknown key fails closed");
            Assert.IsFalse(LobbyKeyScopes.TryResolve(null, "skin", out _), "no schema => protocol keys only");
        }

        [Test]
        public void Snapshot_decodes_protocol_fields_and_copies_declared_game_keys()
        {
            var attrs = new Dictionary<string, string>
            {
                [LobbyKeys.UserId] = "u1", [LobbyKeys.LoadEpoch] = "3", ["skin"] = "duck", ["ignored"] = "x",
            };
            var snap = LobbyMemberSnapshot.FromAttributes(
                new PlayerId("p"), k => attrs.TryGetValue(k, out var v) ? v : null, new GameSchema());
            Assert.AreEqual("u1", snap.Uid);
            Assert.AreEqual(3, snap.LoadEpoch);
            Assert.AreEqual("duck", snap.Get("skin"));
            Assert.IsNull(snap.Get("nick"), "declared but not replicated yet => absent");
            Assert.IsNull(snap.Get("ignored"), "undeclared keys are not copied");

            var bare = LobbyMemberSnapshot.FromAttributes(new PlayerId("p"), _ => null);
            Assert.AreEqual("", bare.Uid);
            Assert.AreEqual(0, bare.LoadEpoch);
        }

        [Test]
        public void Truthy_attribute_tolerates_one_and_case_insensitive_true()
        {
            Assert.IsTrue(LobbyMemberSnapshot.IsTruthyAttribute("1"));
            Assert.IsTrue(LobbyMemberSnapshot.IsTruthyAttribute("True"));
            Assert.IsFalse(LobbyMemberSnapshot.IsTruthyAttribute("0"));
            Assert.IsFalse(LobbyMemberSnapshot.IsTruthyAttribute(null));
        }

        [Test]
        public void ComputeJoinOrders_assigns_dense_order_to_fresh_members_in_input_order()
        {
            var result = JoinOrderMapping.Compute(new Dictionary<string, int>(), new[] { "a", "b", "c" });

            Assert.AreEqual(0, result["a"]);
            Assert.AreEqual(1, result["b"]);
            Assert.AreEqual(2, result["c"]);
        }

        [Test]
        public void ComputeJoinOrders_preserves_known_relative_order_and_appends_newcomer()
        {
            var existing = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1 };

            // reported in a different order + a newcomer c
            var result = JoinOrderMapping.Compute(existing, new[] { "b", "a", "c" });

            Assert.AreEqual(0, result["a"], "a kept ahead of b (prior order)");
            Assert.AreEqual(1, result["b"]);
            Assert.AreEqual(2, result["c"], "newcomer appended last");
        }

        [Test]
        public void ComputeJoinOrders_redensifies_after_a_dropout()
        {
            var existing = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 2 };

            // b left; a and c remain
            var result = JoinOrderMapping.Compute(existing, new[] { "a", "c" });

            Assert.AreEqual(0, result["a"]);
            Assert.AreEqual(1, result["c"], "gap from b is closed (dense 0..N-1)");
            Assert.IsFalse(result.ContainsKey("b"));
        }

        [Test]
        public void ComputeJoinOrders_redensifies_gappy_existing_orders()
        {
            var existing = new Dictionary<string, int> { ["a"] = 5, ["b"] = 9 };

            var result = JoinOrderMapping.Compute(existing, new[] { "a", "b" });

            Assert.AreEqual(0, result["a"], "prior order preserved (5<9) but densified");
            Assert.AreEqual(1, result["b"]);
        }

        [Test]
        public void ComputeJoinOrders_skips_duplicate_and_empty_uids()
        {
            var result = JoinOrderMapping.Compute(new Dictionary<string, int>(), new[] { "", "a", "a", "b" });

            Assert.AreEqual(2, result.Count);
            Assert.AreEqual(0, result["a"]);
            Assert.AreEqual(1, result["b"]);
        }

        [Test]
        public void JoinOrder_json_round_trips()
        {
            var mapping = JoinOrderMapping.Compute(new Dictionary<string, int>(), new[] { "a", "b", "c" });

            string json = JoinOrderMapping.Serialize(mapping);
            var back = JoinOrderMapping.Deserialize(json);

            CollectionAssert.AreEquivalent(mapping, back);
        }

        [Test]
        public void JoinOrder_serialize_empty_and_deserialize_edge_cases()
        {
            Assert.AreEqual("{}", JoinOrderMapping.Serialize(new Dictionary<string, int>()));
            Assert.AreEqual(0, JoinOrderMapping.Deserialize(null).Count);
            Assert.AreEqual(0, JoinOrderMapping.Deserialize("").Count);
            Assert.AreEqual(0, JoinOrderMapping.Deserialize("{}").Count);
        }

        [Test]
        public void GetJoinOrder_returns_fallback_for_unknown_uid()
        {
            var mapping = new Dictionary<string, int> { ["a"] = 0 };
            Assert.AreEqual(0, JoinOrderMapping.GetJoinOrder(mapping, "a"));
            Assert.AreEqual(999, JoinOrderMapping.GetJoinOrder(mapping, "missing"));
            Assert.AreEqual(-1, JoinOrderMapping.GetJoinOrder(mapping, "missing", fallback: -1));
        }

        [Test]
        public void Budget_room_attribute_names_and_count_are_within_eos_limits()
        {
            int roomCount = 0;
            foreach (var kv in LobbyKeys.All)
            {
                Assert.IsTrue(LobbyBudget.KeyNameWithinLimit(kv.Key), $"key '{kv.Key}' name within EOS 64 cap");
                if (kv.Value == LobbyAttributeScope.Room) roomCount++;
            }
            Assert.IsTrue(LobbyBudget.AttributeCountWithinLimit(roomCount), "room attr count under EOS 64 cap");
        }

        [Test]
        public void JoinOrderMapping_at_max_players_serializes_and_round_trips()
        {
            // Realistic 32-hex user ids at a 4-player cap.
            var uids = new[]
            {
                "0002aaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                "0002bbbbbbbbbbbbbbbbbbbbbbbbbbbb",
                "0002bbbb0000000000000000000000cd",
                "0002cccc0000000000000000000000ef",
            };
            var mapping = JoinOrderMapping.Compute(new Dictionary<string, int>(), uids);

            string json = JoinOrderMapping.Serialize(mapping);
            var back = JoinOrderMapping.Deserialize(json);

            Assert.AreEqual(4, back.Count);
            CollectionAssert.AreEquivalent(mapping, back);
            // No EOS value-length cap applies (64 caps the NAME); record the size for reference.
            Assert.Less(json.Length, 256, "4-player mapping stays compact");
        }

        [Test]
        public void ComputeJoinOrders_handles_simultaneous_leave_and_join()
        {
            // The realistic in-match case: b leaves and d joins in the same recompute.
            var existing = new Dictionary<string, int> { ["a"] = 0, ["b"] = 1, ["c"] = 2 };

            var result = JoinOrderMapping.Compute(existing, new[] { "a", "c", "d" });

            Assert.AreEqual(0, result["a"], "a keeps front");
            Assert.AreEqual(1, result["c"], "c densified up into b's freed slot, order preserved (0<2)");
            Assert.AreEqual(2, result["d"], "newcomer d appended last");
            Assert.IsFalse(result.ContainsKey("b"), "departed member dropped");
        }

        [Test]
        public void Serialize_pins_the_wire_format_for_a_single_entry()
        {
            // Deterministic (single key) so the exact string shape is locked: quoted key,
            // colon, bare int, no spaces. Multi-entry order is intentionally not asserted
            // (Dictionary iteration order is not guaranteed; the map semantics don't need it).
            var mapping = new Dictionary<string, int> { ["abc"] = 3 };

            Assert.AreEqual("{\"abc\":3}", JoinOrderMapping.Serialize(mapping));
        }
    }
}
