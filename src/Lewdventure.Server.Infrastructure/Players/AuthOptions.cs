using System.ComponentModel.DataAnnotations;

namespace Server.Infrastructure.Players
{
    internal sealed class AuthOptions
    {
        public const string SectionName = "Auth";

        public bool Enabled { get; set; }

        public string SigningKey { get; set; } = string.Empty;

        public string Issuer { get; set; } = "lewdventure-server";

        public string Audience { get; set; } = "lewdventure-client";

        [Range(5, 1440)]
        public int AccessTokenMinutes { get; set; } = 60;

        [Range(1, 365)]
        public int RefreshTokenDays { get; set; } = 90;
    }
}
