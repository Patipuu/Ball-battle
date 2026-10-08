namespace TeamNet.Multiplayer.Core
{
    public enum PostMatchPartyDecision { Wait, Keep, Leave }

    /// <summary>Empty EOS reads are unknown, not evidence that the party left.</summary>
    public sealed class PostMatchPartyPolicy
    {
        int? loneMemberPoll;
        public PostMatchPartyDecision Observe(bool inLobby, bool localPresent, int members, int pollVersion)
        {
            if (!inLobby) return PostMatchPartyDecision.Leave;
            if (!localPresent || members < 1)
            {
                loneMemberPoll = null;
                return PostMatchPartyDecision.Wait;
            }
            if (members > 1)
            {
                loneMemberPoll = null;
                return PostMatchPartyDecision.Keep;
            }
            if (loneMemberPoll.HasValue && loneMemberPoll.Value != pollVersion)
                return PostMatchPartyDecision.Leave;
            loneMemberPoll = pollVersion;
            return PostMatchPartyDecision.Wait;
        }
    }
}
