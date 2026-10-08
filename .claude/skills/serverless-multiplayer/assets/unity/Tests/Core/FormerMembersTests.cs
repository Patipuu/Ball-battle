using System.Collections.Generic;
using NUnit.Framework;

namespace TeamNet.Multiplayer.Core.Tests
{
    /// <summary>
    /// The epoch-stamped former-members ledger codec. The stamp is what makes a leftover ledger safe: readers
    /// compare it against the current MATCH_EPOCH and fall back when it does not match, rather than governing
    /// the wrong roster. Malformed / empty payloads must read as "no ledger" for the same reason.
    /// </summary>
    public class FormerMembersTests
    {
        [Test]
        public void Round_trips_epoch_and_puids()
        {
            string v = FormerMembers.Serialize(3, new[] { "puid-a", "puid-b" });

            Assert.IsTrue(FormerMembers.TryParse(v, out int epoch, out HashSet<string> puids));
            Assert.AreEqual(3, epoch);
            CollectionAssert.AreEquivalent(new[] { "puid-a", "puid-b" }, puids);
        }

        [Test]
        public void Serialize_skips_duplicates_and_empty_entries()
        {
            string v = FormerMembers.Serialize(1, new[] { "a", "a", "", null, "b" });

            Assert.IsTrue(FormerMembers.TryParse(v, out _, out HashSet<string> puids));
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, puids);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("no-bar")]        // missing the epoch stamp entirely
        [TestCase("|a,b")]          // empty epoch
        [TestCase("abc|a,b")]       // non-numeric epoch
        [TestCase("1|")]            // stamped but carries no PUID
        public void Malformed_or_empty_payloads_read_as_no_ledger(string payload)
        {
            Assert.IsFalse(FormerMembers.TryParse(payload, out _, out HashSet<string> puids),
                "callers must treat this as absent and fall back to counting every member");
            CollectionAssert.IsEmpty(puids);
        }

        [Test]
        public void Append_adds_to_the_same_epoch()
        {
            string v = FormerMembers.Serialize(2, new[] { "a" });

            string appended = FormerMembers.Append(v, 2, new[] { "b" });

            Assert.IsTrue(FormerMembers.TryParse(appended, out int epoch, out HashSet<string> puids));
            Assert.AreEqual(2, epoch);
            CollectionAssert.AreEquivalent(new[] { "a", "b" }, puids);
        }

        [Test]
        public void Append_discards_a_ledger_from_a_different_epoch()
        {
            string stale = FormerMembers.Serialize(1, new[] { "old-a", "old-b" });

            string appended = FormerMembers.Append(stale, 2, new[] { "new-a" });

            Assert.IsTrue(FormerMembers.TryParse(appended, out int epoch, out HashSet<string> puids));
            Assert.AreEqual(2, epoch);
            CollectionAssert.AreEquivalent(new[] { "new-a" }, puids,
                "a previous match's roster must not be carried into this one");
        }
    }
}
