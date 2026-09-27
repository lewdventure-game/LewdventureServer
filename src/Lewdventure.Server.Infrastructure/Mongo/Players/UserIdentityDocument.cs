namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class UserIdentityDocument
    {
        public string Provider { get; set; } = string.Empty;

        public string Subject { get; set; } = string.Empty;

        public DateTime LinkedAt { get; set; }
    }
}
