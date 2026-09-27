namespace Server.Api.Endpoints
{
    internal sealed class AuthTokensResponse
    {
        public string UserId { get; set; } = string.Empty;

        public string AccessToken { get; set; } = string.Empty;

        public DateTime AccessExpiresAt { get; set; }

        public string RefreshToken { get; set; } = string.Empty;

        public DateTime RefreshExpiresAt { get; set; }
    }
}
