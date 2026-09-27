namespace Server.Api.Endpoints
{
    internal sealed class RefreshAuthRequest
    {
        public string UserId { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;
    }
}
