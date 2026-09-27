namespace Server.Api.Security
{
    internal sealed class SecurityNames
    {
        public const string AdminScheme = "AdminApiKey";
        public const string ConfigPublisherScheme = "ConfigPublisherKey";
        public const string PlayerScheme = "PlayerBearer";
        public const string AdminPolicy = "AdminOnly";
        public const string ConfigPublisherPolicy = "ConfigPublisher";
        public const string PlayerPolicy = "Player";
        public const string BattleRateLimitPolicy = "battle";
        public const string ConfigRateLimitPolicy = "config";
        public const string AdminRateLimitPolicy = "admin";
        public const string AuthRateLimitPolicy = "auth";
        public const string PlayerRateLimitPolicy = "player";
    }
}
