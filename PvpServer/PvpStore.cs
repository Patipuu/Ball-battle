using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BallBattle.Sim.Meta;
using BallBattle.Sim.Pvp;
using BallBattle.Sim.Weapons;

namespace BallBattle.PvpServer
{
    public sealed class PvpStore
    {
        readonly object gate = new object();
        readonly IPvpPersistence persistence;
        readonly JsonSerializerOptions json = new JsonSerializerOptions { WriteIndented = true, IncludeFields = true };
        ServerData data;

        /// <summary>An unfinished challenge is settled or dropped after this long (see <see cref="Sweep"/>).</summary>
        public static readonly TimeSpan ChallengeLife = TimeSpan.FromMinutes(15);
        /// <summary>The same three offers come back until this runs out, so leaving the page cannot reroll easy opponents.</summary>
        public static readonly TimeSpan OfferLife = TimeSpan.FromMinutes(10);
        /// <summary>Server clock (tests move it forward).</summary>
        public Func<DateTime> Clock = () => DateTime.UtcNow;

        readonly bool bots;

        /// <param name="bots">Seed and offer the house bots (PvpBots). The server turns them on unless PVP_BOTS=0.</param>
        public PvpStore(string path, bool bots = false) : this(new FilePersistence(path), bots) { }

        /// <param name="persistence">Where the whole server state lives: a JSON file (VM) or one Postgres row (Render + Neon).</param>
        public PvpStore(IPvpPersistence persistence, bool bots = false)
        {
            this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this.bots = bots;
            var saved = persistence.Load();
            data = string.IsNullOrEmpty(saved) ? new ServerData() : JsonSerializer.Deserialize<ServerData>(saved, json) ?? new ServerData();
            if (bots) { EnsureBots(); Save(); }
        }

        /// <summary>
        /// Creates the house bots, or refreshes them to the current spec (rules may have changed since the data was saved).
        /// Their defense records and the challenges against them are kept.
        /// </summary>
        void EnsureBots()
        {
            foreach (var spec in PvpBots.All())
            {
                var a = data.Accounts.FirstOrDefault(x => x.Id == spec.Id);
                if (a == null) data.Accounts.Add(a = new Account { Id = spec.Id });
                a.IsBot = true;
                a.TokenHash = "BOT";   // never a SHA-256 hex: no token signs in as a bot
                a.Name = spec.Name;
                a.Mmr = spec.Mmr;
                a.Balls = Copy(spec.Balls);
                a.Roster = new List<string>(spec.Roster);
                a.BanBall = spec.BanBall;
                a.BanArena = spec.BanArena;
                a.PublishedUtc = new DateTime(2026, 10, 21, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        public object Register()
        {
            lock (gate)
            {
                var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var id = Guid.NewGuid().ToString("N");
                var account = new Account { Id = id, TokenHash = Hash(token), Name = "Player-" + id.Substring(0, 6) };
                data.Accounts.Add(account);
                Save();
                return new { token, account = Self(account) };
            }
        }

        public string Authenticate(string header)
        {
            if (string.IsNullOrEmpty(header) || !header.StartsWith("Bearer ", StringComparison.Ordinal)) return null;
            var token = header.Substring(7);
            if (token.Length != 64) return null;
            var hash = Hash(token);
            lock (gate) return data.Accounts.FirstOrDefault(a => a.TokenHash == hash)?.Id;
        }

        public object Me(string id)
        {
            lock (gate) { Sweep(id); return Self(Get(id)); }
        }

        public object Rename(string id, NameRequest request)
        {
            lock (gate)
            {
                var name = request?.Name?.Trim();
                if (string.IsNullOrEmpty(name) || name.Length > 18 || name.Any(char.IsControl)) throw new ArgumentException("Name must be 1–18 printable characters.");
                if (name.StartsWith(PvpBots.NamePrefix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("That name is kept for bots.");
                var account = Get(id);
                account.Name = name;
                Save();
                return Self(account);
            }
        }

        public object Publish(string id, PublishRequest request)
        {
            lock (gate)
            {
                if (request?.Balls == null || request.Roster == null || request.Roster.Count != 3)
                    throw new ArgumentException("Publish three defense balls and your owned ball progress.");
                var unique = new HashSet<string>();
                foreach (var ball in request.Balls)
                {
                    if (ball == null || !unique.Add(ball.Id)) throw new ArgumentException("Duplicate or missing ball.");
                    new PvpBallSnapshot(ball);
                }
                if (request.Balls.Count > WeaponRegistry.All.Count) throw new ArgumentException("Too many balls.");
                var rosterIds = new HashSet<string>();
                foreach (var ballId in request.Roster)
                    if (!unique.Contains(ballId) || !rosterIds.Add(ballId)) throw new ArgumentException("Roster needs three distinct owned balls.");
                    else if (WeaponRegistry.IsPlayerOnly(ballId)) throw new ArgumentException("That ball is for Run mode only.");
                new PvpBan(request.BanBall, request.BanArena);
                var account = Get(id);
                account.Balls = Copy(request.Balls);
                account.Roster = new List<string>(request.Roster);
                account.BanBall = request.BanBall;
                account.BanArena = request.BanArena;
                account.PublishedUtc = Clock();
                Save();
                return Self(account);
            }
        }

        public object Offers(string id)
        {
            lock (gate)
            {
                var self = Get(id);
                if (!Ready(self)) throw new ArgumentException("Publish a defense roster first.");
                var now = Clock();
                Sweep(id);
                var live = data.Offers.FirstOrDefault(o => o.OwnerId == id && o.ExpiresUtc > now &&
                    o.OpponentIds.All(x => Offerable(data.Accounts.FirstOrDefault(a => a.Id == x))));
                if (live == null)
                {
                    var candidates = data.Accounts.Where(Offerable).Select(a => new PvpCandidate
                    {
                        AccountId = a.Id, Mmr = a.Mmr, AverageDefenseLevel = Average(a), Verified = true, IsBot = a.IsBot,
                        LastChallengedUtc = data.Challenges.Where(c => c.ChallengerId == id && c.DefenderId == a.Id)
                            .Select(c => c.CreatedUtc).DefaultIfEmpty(DateTime.MinValue).Max()
                    });
                    var selected = PvpMatchmaking.ThreeOffers(id, self.Mmr, Average(self), now, candidates);
                    if (selected.Count != 3) return new { offerId = "", refreshSeconds = 0, opponents = Array.Empty<object>() };
                    data.Offers.RemoveAll(o => o.OwnerId == id || o.ExpiresUtc <= now);
                    live = new OfferSet { Id = Guid.NewGuid().ToString("N"), OwnerId = id, CreatedUtc = now,
                        ExpiresUtc = now + OfferLife, OpponentIds = selected.Select(c => c.AccountId).ToList() };
                    data.Offers.Add(live);
                    Save();
                }
                return new
                {
                    offerId = live.Id,
                    refreshSeconds = (int)Math.Ceiling((live.ExpiresUtc - now).TotalSeconds),
                    opponents = live.OpponentIds.Select(x =>
                    {
                        var a = Get(x);
                        return new { id = a.Id, name = a.Name, bot = a.IsBot, mmr = a.Mmr, wins = a.Wins, losses = a.Losses,
                            averageLevel = Average(a), winDelta = PvpRating.Delta(self.Mmr, a.Mmr, self.Matches, 1),
                            lossDelta = PvpRating.Delta(self.Mmr, a.Mmr, self.Matches, 0) };
                    }).ToArray()
                };
            }
        }

        public object Choose(string id, ChooseRequest request)
        {
            lock (gate)
            {
                var now = Clock();
                Sweep(id);
                var offer = data.Offers.FirstOrDefault(o => o.Id == request?.OfferId && o.OwnerId == id && o.ExpiresUtc > now);
                if (offer == null || !offer.OpponentIds.Contains(request.OpponentId)) throw new ArgumentException("Offer expired or opponent was not offered.");
                if (data.Challenges.Any(c => c.ChallengerId == id && Open(c)))
                    throw new ArgumentException("Finish your active challenge first.");
                var challenger = Get(id);
                var defender = Get(request.OpponentId);
                if (!Ready(challenger) || !Ready(defender)) throw new ArgumentException("Both profiles must be ready.");
                if (data.Challenges.Any(c => c.ChallengerId == id && c.DefenderId == defender.Id && c.CreatedUtc > now.AddHours(-24)))
                    throw new ArgumentException("Opponent challenge cooldown is active.");
                var challenge = new Challenge
                {
                    Id = Guid.NewGuid().ToString("N"), ChallengerId = id, DefenderId = defender.Id,
                    CreatedUtc = now, ChallengerBalls = Copy(challenger.Balls), DefenderBalls = Copy(defender.Balls),
                    DefenderRoster = new List<string>(defender.Roster), DefenderBanBall = defender.BanBall,
                    DefenderBanArena = defender.BanArena, ChallengerMmrBefore = challenger.Mmr,
                    DefenderMmrAtStart = defender.Mmr, Seed = BitConverter.ToUInt32(RandomNumberGenerator.GetBytes(4))
                };
                data.Offers.Remove(offer);
                data.Challenges.Add(challenge);
                Save();
                return new { challengeId = challenge.Id, state = challenge.State, opponent = Public(defender) };
            }
        }

        public object Ban(string id, string challengeId, BanRequest request)
        {
            lock (gate)
            {
                Sweep(id);
                var c = OwnedChallenge(id, challengeId);
                if (c.State == "expired") throw new ArgumentException("Challenge expired. Start a new offer.");
                if (c.State == "pick" || c.State == "done") return Reveal(c);
                if (c.State != "ban") throw new ArgumentException("Challenge state is invalid.");
                var ban = new PvpBan(request?.BallId, request?.ArenaId);
                c.ChallengerBanBall = ban.BallId;
                c.ChallengerBanArena = ban.ArenaId;
                c.State = "pick";
                Save();
                return Reveal(c);
            }
        }

        public object Pick(string id, string challengeId, PickRequest request)
        {
            lock (gate)
            {
                Sweep(id);
                var c = OwnedChallenge(id, challengeId);
                if (c.State == "done") return Result(c);
                if (c.State == "expired") throw new ArgumentException("Challenge expired. Start a new offer.");
                if (c.State != "pick") throw new ArgumentException("Submit your blind bans first.");
                Settle(c, request?.BallId, false);
                Save();
                return Result(c);
            }
        }

        /// <summary>Runs the series once and commits the challenger's MMR. The only place a challenge becomes "done".</summary>
        void Settle(Challenge c, string ballId, bool auto)
        {
            var draft = PvpDraft.Resolve(new PvpBan(c.ChallengerBanBall, c.ChallengerBanArena),
                new PvpBan(c.DefenderBanBall, c.DefenderBanArena), ballId,
                c.ChallengerBalls.Select(b => new PvpBallSnapshot(b)).ToList(),
                c.DefenderRoster.Select(r => new PvpBallSnapshot(c.DefenderBalls.First(b => b.Id == r))).ToList(), c.Seed);
            var series = PvpSeries.Run(draft, c.Seed);
            var challenger = Get(c.ChallengerId);
            var defender = data.Accounts.FirstOrDefault(a => a.Id == c.DefenderId);
            c.PickBall = ballId;
            c.AutoPicked = auto;
            c.ArenaId = draft.ArenaId;
            c.Winner = series.Winner;
            c.ChallengerWins = series.ChallengerWins;
            c.DefenderWins = series.DefenderWins;
            c.Draws = series.Draws;
            c.RulesVersion = series.RulesVersion;
            c.RoundSeeds = series.RoundSeeds;
            c.RoundHashes = series.RoundHashes.Select(h => h.ToString("X16")).ToArray();
            c.Delta = PvpRating.Delta(c.ChallengerMmrBefore, c.DefenderMmrAtStart, challenger.Matches,
                c.Winner == 0 ? 1 : c.Winner == 1 ? 0 : -1);
            challenger.Mmr += c.Delta;
            if (c.Winner >= 0)
            {
                challenger.Matches++;
                if (c.Winner == 0) { challenger.Wins++; if (defender != null) defender.DefenseLosses++; }
                else { challenger.Losses++; if (defender != null) defender.DefenseWins++; }
            }
            c.State = "done";
        }

        /// <summary>
        /// Timeout fallback for the challenger's unfinished challenges. Before the bans are revealed nothing is known, so the
        /// challenge is simply dropped ("expired", no MMR). After the reveal, walking away would dodge a bad matchup, so the
        /// server picks for the player (their highest-level legal ball) and settles the series as usual.
        /// </summary>
        void Sweep(string challengerId)
        {
            var now = Clock();
            var changed = false;
            foreach (var c in data.Challenges)
            {
                if (c.ChallengerId != challengerId || !Open(c) || now <= c.CreatedUtc + ChallengeLife) continue;
                if (c.State == "ban") c.State = "expired";
                else Settle(c, AutoPick(c), true);
                changed = true;
            }
            if (changed) Save();
        }

        static string AutoPick(Challenge c)
        {
            BallProgress best = null;
            foreach (var b in c.ChallengerBalls)
                if (b.Id != c.ChallengerBanBall && b.Id != c.DefenderBanBall && (best == null || b.Level > best.Level)) best = b;
            return best?.Id;
        }

        static bool Open(Challenge c) => c.State == "ban" || c.State == "pick";

        public object GetChallenge(string id, string challengeId)
        {
            lock (gate)
            {
                Sweep(id);
                var c = OwnedChallenge(id, challengeId);
                return c.State == "ban" || c.State == "expired" ? new { challengeId = c.Id, state = c.State, opponentName = NameOf(c.DefenderId) }
                    : c.State == "pick" ? Reveal(c) : Result(c);
            }
        }

        public object History(string id)
        {
            lock (gate)
            {
                Sweep(id);
                return data.Challenges.Where(c => c.ChallengerId == id && c.State == "done")
                    .OrderByDescending(c => c.CreatedUtc).Take(20).Select(Result).ToArray();
            }
        }

        static object Public(Account a) => new { id = a.Id, name = a.Name, mmr = a.Mmr, matches = a.Matches,
            wins = a.Wins, losses = a.Losses, defenseWins = a.DefenseWins, defenseLosses = a.DefenseLosses,
            ready = Ready(a) };

        object Self(Account a) => new { id = a.Id, name = a.Name, mmr = a.Mmr, matches = a.Matches,
            wins = a.Wins, losses = a.Losses, defenseWins = a.DefenseWins, defenseLosses = a.DefenseLosses,
            ready = Ready(a), roster = a.Roster, banBall = a.BanBall, banArena = a.BanArena,
            activeChallengeId = data.Challenges.Where(c => c.ChallengerId == a.Id && Open(c)).OrderByDescending(c => c.CreatedUtc).Select(c => c.Id).FirstOrDefault() };

        string NameOf(string accountId) => data.Accounts.FirstOrDefault(a => a.Id == accountId)?.Name ?? "?";

        object Reveal(Challenge c) => new { challengeId = c.Id, state = c.State, opponentName = NameOf(c.DefenderId),
            challengerBanBall = c.ChallengerBanBall, challengerBanArena = c.ChallengerBanArena,
            defenderBanBall = c.DefenderBanBall, defenderBanArena = c.DefenderBanArena,
            legalBalls = c.ChallengerBalls.Select(b => b.Id).Where(b => b != c.ChallengerBanBall && b != c.DefenderBanBall && !WeaponRegistry.IsPlayerOnly(b)).ToArray() };

        object Result(Challenge c) => new { challengeId = c.Id, state = c.State,
            opponentId = c.DefenderId, opponentName = NameOf(c.DefenderId),
            autoPicked = c.AutoPicked, createdUtc = c.CreatedUtc.ToString("o"), winner = c.Winner, challengerWins = c.ChallengerWins,
            defenderWins = c.DefenderWins, draws = c.Draws, arenaId = c.ArenaId,
            challengerBall = c.PickBall,
            defenderBall = c.DefenderRoster.FirstOrDefault(b => b != c.ChallengerBanBall && b != c.DefenderBanBall),
            challengerProgress = c.ChallengerBalls.FirstOrDefault(b => b.Id == c.PickBall),
            defenderProgress = c.DefenderBalls.FirstOrDefault(b => b.Id == c.DefenderRoster.FirstOrDefault(r => r != c.ChallengerBanBall && r != c.DefenderBanBall)),
            challengerBanBall = c.ChallengerBanBall, challengerBanArena = c.ChallengerBanArena,
            defenderBanBall = c.DefenderBanBall, defenderBanArena = c.DefenderBanArena,
            mmrBefore = c.ChallengerMmrBefore, mmrAfter = c.ChallengerMmrBefore + c.Delta,
            delta = c.Delta, seed = c.Seed, rulesVersion = c.RulesVersion,
            roundSeeds = c.RoundSeeds, roundHashes = c.RoundHashes };

        /// <summary>Ready, and a bot only while bots are on.</summary>
        bool Offerable(Account a) => Ready(a) && (bots || !a.IsBot);
        static bool Ready(Account a) => a != null && a.PublishedUtc != default && a.Roster?.Count == 3 && a.Balls?.Count >= 3;
        static int Average(Account a) => a.Roster.Sum(r => a.Balls.First(b => b.Id == r).Level) / 3;
        static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        static List<BallProgress> Copy(IEnumerable<BallProgress> balls) => balls.Select(b => new BallProgress
        {
            Id = b.Id, Level = b.Level, Shards = 0, Vit = b.Vit, Pow = b.Pow, Growth = b.Growth,
            Start = b.Start, SkillUps = b.SkillUps, Branch = b.Branch, Finale = b.Finale
        }).ToList();

        Account Get(string id) => data.Accounts.First(a => a.Id == id);
        Challenge OwnedChallenge(string id, string challengeId) =>
            data.Challenges.FirstOrDefault(c => c.Id == challengeId && c.ChallengerId == id)
            ?? throw new ArgumentException("Challenge not found.");

        void Save() => persistence.Save(JsonSerializer.Serialize(data, json));
    }

    /// <summary>Stores the whole server state as one JSON document. Called under the store's lock, one process only.</summary>
    public interface IPvpPersistence
    {
        /// <summary>The saved JSON, or null / empty when nothing was saved yet.</summary>
        string Load();
        void Save(string json);
    }

    /// <summary>JSON file with an atomic temp-file rename (the Oracle VM / local setup).</summary>
    public sealed class FilePersistence : IPvpPersistence
    {
        readonly string path;
        public FilePersistence(string path) { this.path = Path.GetFullPath(path); }
        public string Load() => File.Exists(path) ? File.ReadAllText(path) : null;
        public void Save(string json)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var temp = path + ".tmp";
            File.WriteAllText(temp, json);
            File.Move(temp, path, true);
        }
    }
}
