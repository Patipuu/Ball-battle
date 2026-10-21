using System;
using System.Collections.Generic;
using BallBattle.Sim.Meta;

namespace BallBattle.PvpServer
{
    public sealed class Account
    {
        public string Id { get; set; }
        public string TokenHash { get; set; }
        public string Name { get; set; }
        public int Mmr { get; set; } = 1000;
        public int Matches { get; set; }
        public int Wins { get; set; }
        public int Losses { get; set; }
        public int DefenseWins { get; set; }
        public int DefenseLosses { get; set; }
        public List<BallProgress> Balls { get; set; } = new List<BallProgress>();
        public List<string> Roster { get; set; } = new List<string>();
        public string BanBall { get; set; }
        public string BanArena { get; set; }
        public DateTime PublishedUtc { get; set; }
        /// <summary>House bot (PvpBots): a defense only, no token, MMR fixed.</summary>
        public bool IsBot { get; set; }
    }

    public sealed class OfferSet
    {
        public string Id { get; set; }
        public string OwnerId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ExpiresUtc { get; set; }
        public List<string> OpponentIds { get; set; } = new List<string>();
    }

    public sealed class Challenge
    {
        public string Id { get; set; }
        public string ChallengerId { get; set; }
        public string DefenderId { get; set; }
        public DateTime CreatedUtc { get; set; }
        public string State { get; set; } = "ban";
        public string ChallengerBanBall { get; set; }
        public string ChallengerBanArena { get; set; }
        public string DefenderBanBall { get; set; }
        public string DefenderBanArena { get; set; }
        public List<BallProgress> ChallengerBalls { get; set; } = new List<BallProgress>();
        public List<BallProgress> DefenderBalls { get; set; } = new List<BallProgress>();
        public List<string> DefenderRoster { get; set; } = new List<string>();
        public string PickBall { get; set; }
        /// <summary>True when the server picked for a challenger who left after the bans were revealed.</summary>
        public bool AutoPicked { get; set; }
        public string ArenaId { get; set; }
        public uint Seed { get; set; }
        public int ChallengerMmrBefore { get; set; }
        public int DefenderMmrAtStart { get; set; }
        public int Delta { get; set; }
        public int Winner { get; set; } = -1;
        public int ChallengerWins { get; set; }
        public int DefenderWins { get; set; }
        public int Draws { get; set; }
        public int RulesVersion { get; set; }
        public uint[] RoundSeeds { get; set; }
        public string[] RoundHashes { get; set; }
    }

    public sealed class ServerData
    {
        public List<Account> Accounts { get; set; } = new List<Account>();
        public List<OfferSet> Offers { get; set; } = new List<OfferSet>();
        public List<Challenge> Challenges { get; set; } = new List<Challenge>();
    }

    public sealed class PublishRequest
    {
        public List<BallProgress> Balls { get; set; }
        public List<string> Roster { get; set; }
        public string BanBall { get; set; }
        public string BanArena { get; set; }
    }

    public sealed class ChooseRequest { public string OfferId { get; set; } public string OpponentId { get; set; } }
    public sealed class BanRequest { public string BallId { get; set; } public string ArenaId { get; set; } }
    public sealed class PickRequest { public string BallId { get; set; } }
    public sealed class NameRequest { public string Name { get; set; } }
}
