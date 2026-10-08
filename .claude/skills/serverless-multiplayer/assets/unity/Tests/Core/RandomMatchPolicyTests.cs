using TeamNet.Multiplayer.Core;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>Random-match candidate filter and the create-after-miss rule.</summary>
    public class RandomMatchPolicyTests
    {
        static RandomMatchPolicy.Candidate Room(uint slots, string started = "0", string mode = "TwoPlayers", string bundle = "1.2.3")
            => new RandomMatchPolicy.Candidate(slots, started, mode, bundle);

        [Test]
        public void Random_match_joins_open_room_of_same_mode_and_build()
        {
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(Room(1), "TwoPlayers", "1.2.3", out _));
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(Room(1, mode: "twoplayers"), "TwoPlayers", "1.2.3", out _),
                "mode compare is case-insensitive");
        }

        [Test]
        public void Random_match_skips_full_room()
        {
            // A full room in the shared bucket used to be joined blindly and EOS answered
            // LobbyTooManyPlayers for every Random Match.
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(Room(0), "TwoPlayers", "1.2.3", out string why));
            Assert.AreEqual("full", why);
        }

        [Test]
        public void Random_match_skips_started_room()
        {
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(Room(1, started: "1"), "TwoPlayers", "1.2.3", out string why));
            Assert.AreEqual("started", why);
        }

        [Test]
        public void Random_match_skips_other_mode_and_modeless_gym_rooms()
        {
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(Room(3, mode: "FourPlayers"), "TwoPlayers", "1.2.3", out _));
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(Room(3, mode: null), "TwoPlayers", "1.2.3", out _),
                "gym / harness lobbies never write a mode and must not be matched");
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(Room(3, mode: null), "", "1.2.3", out _),
                "no wanted mode → any room");
        }

        [Test]
        public void Random_match_skips_other_build_but_tolerates_missing_bundle()
        {
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(Room(1, bundle: "1.2.2"), "TwoPlayers", "1.2.3", out _));
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(Room(1, bundle: null), "TwoPlayers", "1.2.3", out _));
        }

        [TestCase("not-found", true)]
        [TestCase("LobbyTooManyPlayers", true)]
        [TestCase("NotFound", true)]
        [TestCase("cancelled", false)]
        [TestCase("timeout", false)]
        [TestCase("InvalidAuth", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void Random_match_hosts_a_room_only_after_a_matchmaking_miss(string error, bool expected)
            => Assert.AreEqual(expected, RandomMatchPolicy.ShouldCreateAfterJoinFailure(error));

        [Test]
        public void Random_match_skips_private_rooms_but_accepts_unflagged_and_explicitly_public()
        {
            var priv = new RandomMatchPolicy.Candidate(1, "0", "TwoPlayers", "1.2.3", isPrivate: "1");
            Assert.IsFalse(RandomMatchPolicy.IsJoinable(priv, "TwoPlayers", "1.2.3", out string why));
            Assert.AreEqual("private", why);
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(
                new RandomMatchPolicy.Candidate(1, "0", "TwoPlayers", "1.2.3", isPrivate: "0"), "TwoPlayers", "1.2.3", out _));
            Assert.IsTrue(RandomMatchPolicy.IsJoinable(Room(1), "TwoPlayers", "1.2.3", out _),
                "a room that never wrote the flag stays joinable");
        }

        [Test]
        public void Private_value_encoding_round_trips_through_the_policy()
        {
            Assert.AreEqual("1", LobbyKeys.PrivateValue(true));
            Assert.AreEqual("0", LobbyKeys.PrivateValue(false));
            Assert.IsTrue(LobbyKeys.IsRoom(LobbyKeys.Private));
        }
    }
}
