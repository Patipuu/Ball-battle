using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using BallBattle.PvpServer;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Pvp;
using BallBattle.Sim.Weapons;
using NUnit.Framework;

namespace BallBattle.SimTests
{
    public class PvpServerTests
    {
        static JsonElement Json(object value) => JsonSerializer.SerializeToElement(value);

        /// <summary>21/10 live PvP: pick (state "fight"), then send the presses (none by default) and get the settled result.</summary>
        /// <summary>21/10 teams: <paramref name="first"/> then two more legal balls (never the test bans aegis, serpent, blade-banned...).</summary>
        static string[] Team(string first) =>
            new[] { first }.Concat(new[] { "rig", "volley", "pike", "fang", "venom" }.Where(b => b != first)).Take(3).ToArray();

        static JsonElement PickAndFight(PvpStore store, string id, string challengeId, string ball, int[] taps = null)
        {
            var fight = Json(store.Pick(id, challengeId, new PickRequest { BallIds = Team(ball) }));
            Assert.That(fight.GetProperty("state").GetString(), Is.EqualTo("fight"));
            return Json(store.Finish(id, challengeId, new FightRequest { RulesVersion = BallBattle.Sim.SimVersion.Rules, Taps = taps }));
        }

        [Test]
        public void FourRealAccountsBlindBanAndOneSettlementSurviveRestart()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "data.json");
            try
            {
                var store = new PvpStore(path);
                var accounts = Enumerable.Range(0, 5).Select(_ => Json(store.Register())).ToArray();
                var ids = accounts.Select(a => a.GetProperty("account").GetProperty("id").GetString()).ToArray();
                var bodies = new PublishRequest
                {
                    Balls = WeaponRegistry.All.Select(w => new BallProgress { Id = w.Id }).ToList(),
                    Roster = new List<string> { "blade", "fang", "pike" },
                    BanBall = "serpent", BanArena = "spike-walls"
                };
                foreach (var id in ids) store.Publish(id, bodies);
                var offers = Json(store.Offers(ids[0]));
                Assert.That(offers.GetProperty("opponents").GetArrayLength(), Is.EqualTo(3));
                var offerId = offers.GetProperty("offerId").GetString();
                var opponent = offers.GetProperty("opponents")[0].GetProperty("id").GetString();
                var challenge = Json(store.Choose(ids[0], new ChooseRequest { OfferId = offerId, OpponentId = opponent }));
                Assert.That(challenge.GetProperty("opponent").TryGetProperty("roster", out _), Is.False);
                Assert.That(challenge.GetProperty("opponent").TryGetProperty("banBall", out _), Is.False);
                var challengeId = challenge.GetProperty("challengeId").GetString();
                Assert.That(Json(store.Me(ids[0])).GetProperty("activeChallengeId").GetString(), Is.EqualTo(challengeId));
                var parallelOffers = Json(store.Offers(ids[0]));
                Assert.Throws<ArgumentException>(() => store.Choose(ids[0], new ChooseRequest
                {
                    OfferId = parallelOffers.GetProperty("offerId").GetString(), OpponentId = parallelOffers.GetProperty("opponents")[1].GetProperty("id").GetString()
                }));
                var hidden = Json(store.GetChallenge(ids[0], challengeId));
                Assert.That(hidden.TryGetProperty("defenderBanBall", out _), Is.False);
                var revealed = Json(store.Ban(ids[0], challengeId, new BanRequest { BallId = "aegis", ArenaId = "low-gravity" }));
                Assert.That(revealed.GetProperty("defenderBanBall").GetString(), Is.EqualTo("serpent"));
                var first = PickAndFight(store, ids[0], challengeId, "blade");
                var mmr = first.GetProperty("mmrAfter").GetInt32();
                Assert.That(mmr, Is.EqualTo(1000 + first.GetProperty("delta").GetInt32()));
                Assert.That(first.GetProperty("roundHashes").GetArrayLength(), Is.GreaterThanOrEqualTo(2));
                store = new PvpStore(path);
                Assert.That(Json(store.Offers(ids[1])).GetProperty("opponents").GetArrayLength(), Is.EqualTo(3));
                var retry = Json(store.Pick(ids[0], challengeId, new PickRequest { BallIds = Team("fang") }));
                Assert.That(retry.GetProperty("mmrAfter").GetInt32(), Is.EqualTo(mmr));
                Assert.That(Json(store.Me(ids[0])).GetProperty("mmr").GetInt32(), Is.EqualTo(mmr));
                Assert.That(Json(store.Me(ids[0])).GetProperty("activeChallengeId").ValueKind, Is.EqualTo(JsonValueKind.Null));
                Assert.That(Json(store.History(ids[0])).GetArrayLength(), Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => store.Choose(ids[0], new ChooseRequest { OfferId = offerId, OpponentId = opponent }));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void RejectsForgedTreeAndUnownedChallenge()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var store = new PvpStore(Path.Combine(folder, "data.json"));
                var id = Json(store.Register()).GetProperty("account").GetProperty("id").GetString();
                var invalid = new PublishRequest
                {
                    Balls = new List<BallProgress> { new BallProgress { Id = "blade", Level = 1, Pow = 2 },
                        new BallProgress { Id = "fang" }, new BallProgress { Id = "pike" } },
                    Roster = new List<string> { "blade", "fang", "pike" }, BanBall = "serpent", BanArena = "classic"
                };
                Assert.Throws<ArgumentException>(() => store.Publish(id, invalid));
                Assert.Throws<ArgumentException>(() => store.GetChallenge(id, "unknown"));
            }
            finally { Directory.Delete(folder, true); }
        }

        static (PvpStore store, string[] ids, string path) ReadyStore(string folder, int count)
        {
            var path = Path.Combine(folder, "data.json");
            var store = new PvpStore(path);
            var ids = Enumerable.Range(0, count).Select(_ => Json(store.Register()).GetProperty("account").GetProperty("id").GetString()).ToArray();
            var body = new PublishRequest
            {
                Balls = WeaponRegistry.All.Select(w => new BallProgress { Id = w.Id }).ToList(),
                Roster = new List<string> { "blade", "fang", "pike" }, BanBall = "serpent", BanArena = "spike-walls"
            };
            foreach (var id in ids) store.Publish(id, body);
            return (store, ids, path);
        }

        [Test]
        public void OffersStayTheSameUntilTheyRunOut()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var (store, ids, _) = ReadyStore(folder, 6);
                var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
                store.Clock = () => now;
                var first = Json(store.Offers(ids[0]));
                var again = Json(store.Offers(ids[0]));
                Assert.That(again.GetProperty("offerId").GetString(), Is.EqualTo(first.GetProperty("offerId").GetString()), "no free reroll");
                Assert.That(again.GetProperty("refreshSeconds").GetInt32(), Is.EqualTo((int)PvpStore.OfferLife.TotalSeconds));
                now += PvpStore.OfferLife + TimeSpan.FromSeconds(1);
                Assert.That(Json(store.Offers(ids[0])).GetProperty("offerId").GetString(), Is.Not.EqualTo(first.GetProperty("offerId").GetString()));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void LeavingAfterTheRevealIsSettledButLeavingBeforeItIsDropped()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var (store, ids, path) = ReadyStore(folder, 5);
                var now = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
                store.Clock = () => now;

                // Before the reveal: dropped, no MMR, a new challenge may start.
                var offers = Json(store.Offers(ids[0]));
                var c1 = Json(store.Choose(ids[0], new ChooseRequest { OfferId = offers.GetProperty("offerId").GetString(),
                    OpponentId = offers.GetProperty("opponents")[0].GetProperty("id").GetString() })).GetProperty("challengeId").GetString();
                now += PvpStore.ChallengeLife + TimeSpan.FromSeconds(1);
                Assert.That(Json(store.GetChallenge(ids[0], c1)).GetProperty("state").GetString(), Is.EqualTo("expired"));
                Assert.Throws<ArgumentException>(() => store.Ban(ids[0], c1, new BanRequest { BallId = "aegis", ArenaId = "classic" }));
                Assert.That(Json(store.Me(ids[0])).GetProperty("mmr").GetInt32(), Is.EqualTo(PvpRating.Initial));

                // After the reveal: the server picks and settles exactly once.
                offers = Json(store.Offers(ids[0]));
                var c2 = Json(store.Choose(ids[0], new ChooseRequest { OfferId = offers.GetProperty("offerId").GetString(),
                    OpponentId = offers.GetProperty("opponents")[1].GetProperty("id").GetString() })).GetProperty("challengeId").GetString();
                store.Ban(ids[0], c2, new BanRequest { BallId = "blade", ArenaId = "classic" });
                now += PvpStore.ChallengeLife + TimeSpan.FromSeconds(1);
                var me = Json(store.Me(ids[0]));
                Assert.That(me.GetProperty("activeChallengeId").ValueKind, Is.EqualTo(JsonValueKind.Null));
                var result = Json(store.GetChallenge(ids[0], c2));
                Assert.That(result.GetProperty("state").GetString(), Is.EqualTo("done"));
                Assert.That(result.GetProperty("autoPicked").GetBoolean(), Is.True);
                Assert.That(result.GetProperty("challengerBall").GetString(), Is.Not.EqualTo("blade").And.Not.EqualTo("serpent"));
                Assert.That(result.GetProperty("opponentName").GetString(), Does.StartWith("Player-"));
                var mmr = me.GetProperty("mmr").GetInt32();
                store = new PvpStore(path) { Clock = () => now };
                Assert.That(Json(store.Pick(ids[0], c2, new PickRequest { BallIds = Team("fang") })).GetProperty("mmrAfter").GetInt32(), Is.EqualTo(mmr));
                Assert.That(Json(store.History(ids[0])).GetArrayLength(), Is.EqualTo(1));
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void HouseBotsFillAnEmptyServerAndStayFixed()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "data.json");
            try
            {
                var store = new PvpStore(path, bots: true);
                var me = Json(store.Register());
                var id = me.GetProperty("account").GetProperty("id").GetString();
                store.Publish(id, new PublishRequest
                {
                    Balls = WeaponRegistry.All.Select(w => new BallProgress { Id = w.Id }).ToList(),
                    Roster = new List<string> { "blade", "fang", "pike" }, BanBall = "serpent", BanArena = "spike-walls"
                });
                var offers = Json(store.Offers(id));
                Assert.That(offers.GetProperty("opponents").GetArrayLength(), Is.EqualTo(3), "the only real player still gets three opponents");
                var bot = offers.GetProperty("opponents")[0];
                Assert.That(bot.GetProperty("bot").GetBoolean(), Is.True);
                Assert.That(bot.GetProperty("name").GetString(), Does.StartWith(PvpBots.NamePrefix));
                var botId = bot.GetProperty("id").GetString();
                var botMmr = bot.GetProperty("mmr").GetInt32();

                var c = Json(store.Choose(id, new ChooseRequest { OfferId = offers.GetProperty("offerId").GetString(), OpponentId = botId }))
                    .GetProperty("challengeId").GetString();
                var reveal = Json(store.Ban(id, c, new BanRequest { BallId = "aegis", ArenaId = "classic" }));
                var pick = reveal.GetProperty("legalBalls")[0].GetString();
                var result = PickAndFight(store, id, c, pick);
                Assert.That(result.GetProperty("state").GetString(), Is.EqualTo("done"));
                Assert.That(result.GetProperty("opponentName").GetString(), Does.StartWith(PvpBots.NamePrefix));

                Assert.Throws<ArgumentException>(() => store.Rename(id, new NameRequest { Name = "bot rusty" }), "players cannot pose as bots");
                Assert.That(store.Authenticate("Bearer " + new string('0', 64)), Is.Null);

                store = new PvpStore(path, bots: true);   // restart: same bots, once each, MMR unchanged, history kept
                var again = Json(store.Offers(id));
                Assert.That(again.GetProperty("opponents").GetArrayLength(), Is.EqualTo(3));
                foreach (var o in again.GetProperty("opponents").EnumerateArray())
                    Assert.That(o.GetProperty("id").GetString(), Is.Not.EqualTo(botId), "24 h rematch cooldown applies to bots too");
                Assert.That(Json(store.History(id)).GetArrayLength(), Is.EqualTo(1));
                Assert.That(PvpBots.Make(int.Parse(botId.Substring(PvpBots.IdPrefix.Length)) - 1).Mmr, Is.EqualTo(botMmr));

                var off = new PvpStore(path);   // PVP_BOTS=0: bots are never offered
                Assert.That(Json(off.Offers(id)).GetProperty("opponents").GetArrayLength(), Is.Zero);
            }
            finally { Directory.Delete(folder, true); }
        }

        [Test]
        public void BrowserOriginsAllowItchAndLocalOnly()
        {
            var none = Array.Empty<string>();
            Assert.That(CorsRules.Allowed("https://html-classic.itch.zone", none), Is.True);
            Assert.That(CorsRules.Allowed("https://v6p9d9t4.ssl.hwcdn.net", none), Is.True);
            Assert.That(CorsRules.Allowed("http://localhost:8080", none), Is.True);
            Assert.That(CorsRules.Allowed("http://html-classic.itch.zone", none), Is.False, "https only");
            Assert.That(CorsRules.Allowed("https://evil-itch.zone", none), Is.False);
            Assert.That(CorsRules.Allowed("https://itch.zone.evil.com", none), Is.False);
            Assert.That(CorsRules.Allowed("https://mygame.example", new[] { "https://mygame.example/" }), Is.True);
        }

        [Test]
        public void LiveFightSettlesFromThePressesAndOnlyOnce()
        {
            var folder = Path.Combine(Path.GetTempPath(), "bb-pvp-test-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                var now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);
                var store = new PvpStore(Path.Combine(folder, "data.json"), bots: true) { Clock = () => now };
                var id = Json(store.Register()).GetProperty("account").GetProperty("id").GetString();
                store.Publish(id, new PublishRequest
                {
                    Balls = WeaponRegistry.All.Select(w => new BallProgress { Id = w.Id }).ToList(),
                    Roster = new List<string> { "blade", "fang", "pike" }, BanBall = "serpent", BanArena = "spike-walls"
                });
                string Start()
                {
                    var offers = Json(store.Offers(id));
                    var c = Json(store.Choose(id, new ChooseRequest { OfferId = offers.GetProperty("offerId").GetString(),
                        OpponentId = offers.GetProperty("opponents")[0].GetProperty("id").GetString() })).GetProperty("challengeId").GetString();
                    store.Ban(id, c, new BanRequest { BallId = "aegis", ArenaId = "classic" });
                    return c;
                }

                var c1 = Start();
                var fight = Json(store.Pick(id, c1, new PickRequest { BallIds = Team("blade") }));
                Assert.That(fight.GetProperty("state").GetString(), Is.EqualTo("fight"));
                Assert.That(fight.GetProperty("challengerTeam")[0].GetString(), Is.EqualTo("blade"));
                Assert.That(fight.GetProperty("challengerTeam").GetArrayLength(), Is.EqualTo(3));
                Assert.That(fight.GetProperty("defenderTeam").GetArrayLength(), Is.EqualTo(3));
                Assert.That(fight.TryGetProperty("challengerProgress", out _) && fight.TryGetProperty("seed", out _), Is.True);
                Assert.That(fight.TryGetProperty("winner", out _), Is.False, "no result before the presses are in");
                Assert.That(Json(store.GetChallenge(id, c1)).GetProperty("state").GetString(), Is.EqualTo("fight"), "a restart can see the fight");
                Assert.Throws<ArgumentException>(() => store.Finish(id, c1, new FightRequest { Taps = new[] { 0 } }), "odd tap list");
                Assert.Throws<ArgumentException>(() => store.Finish(id, c1, new FightRequest { Taps = new[] { 99, 5 } }), "round out of range");
                Assert.Throws<ArgumentException>(() => store.Finish(id, c1, new FightRequest { RulesVersion = 1 }), "old client rules");
                var taps = new List<int>();
                for (var step = 30; step < 3000; step += 30) { taps.Add(0); taps.Add(step); taps.Add(1); taps.Add(step); }
                var done = Json(store.Finish(id, c1, new FightRequest { RulesVersion = BallBattle.Sim.SimVersion.Rules, Taps = taps.ToArray() }));
                Assert.That(done.GetProperty("state").GetString(), Is.EqualTo("done"));
                var mmr = done.GetProperty("mmrAfter").GetInt32();
                var again = Json(store.Finish(id, c1, new FightRequest { RulesVersion = BallBattle.Sim.SimVersion.Rules }));
                Assert.That(again.GetProperty("mmrAfter").GetInt32(), Is.EqualTo(mmr), "a second send changes nothing");

                // Left in the middle: after FightLife the series settles as a fight without presses.
                var c2 = Start();
                store.Pick(id, c2, new PickRequest { BallIds = Team("fang") });
                now += PvpStore.FightLife + TimeSpan.FromSeconds(1);
                var settled = Json(store.GetChallenge(id, c2));
                Assert.That(settled.GetProperty("state").GetString(), Is.EqualTo("done"));
                Assert.That(settled.GetProperty("challengerBall").GetString(), Is.EqualTo("fang"));
            }
            finally { Directory.Delete(folder, true); }
        }
    }
}
