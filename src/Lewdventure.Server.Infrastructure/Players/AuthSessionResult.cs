namespace Server.Infrastructure.Players
{
    internal sealed class AuthSessionResult
    {
        public AuthSessionResult(bool succeeded, string userId, string refreshToken, DateTime refreshExpiresAt, string error)
        {
            Succeeded = succeeded;
            UserId = userId;
            RefreshToken = refreshToken;
            RefreshExpiresAt = refreshExpiresAt;
            Error = error;
        }

        public bool Succeeded { get; }

        public string UserId { get; }

        public string RefreshToken { get; }

        public DateTime RefreshExpiresAt { get; }

        public string Error { get; }
    }
}
