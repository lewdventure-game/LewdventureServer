namespace Server.Api.Payments
{
    internal sealed class NutakuLoginCode
    {
        public string Id { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
