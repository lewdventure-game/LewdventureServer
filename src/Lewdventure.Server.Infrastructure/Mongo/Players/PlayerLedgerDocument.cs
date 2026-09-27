namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerLedgerDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public long Rev { get; set; }

        public List<PlayerLedgerEntryDocument> Entries { get; set; } = new();
    }
}
