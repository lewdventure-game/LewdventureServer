namespace Server.Infrastructure.Qa
{
    internal sealed class QaPlayerMatch
    {
        public const string UserIdMatch = "userId";
        public const string AliasMatch = "alias";
        public const string DeviceMatch = "deviceId";

        public QaPlayerMatch(string userId, string alias, string matchedBy)
        {
            UserId = userId;
            Alias = alias;
            MatchedBy = matchedBy;
        }

        public string UserId { get; }

        public string Alias { get; }

        public string MatchedBy { get; }
    }
}
