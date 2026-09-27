namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserDeviceDocument
    {
        public string DeviceIdHash { get; set; } = string.Empty;

        public string RefreshTokenHash { get; set; } = string.Empty;

        public DateTime RefreshExpiresAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime LastSeenAt { get; set; }

        public string ClientVersion { get; set; } = string.Empty;
    }
}
