namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public int FormatVersion { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public string SourceKind { get; set; } = string.Empty;

        public DateTime SnapshotCreatedAt { get; set; }

        public List<ConfigSnapshotDomainDocument> Domains { get; set; } = new();
    }
}
