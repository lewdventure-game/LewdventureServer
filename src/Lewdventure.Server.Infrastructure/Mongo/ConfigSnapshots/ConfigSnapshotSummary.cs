namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotSummary
    {
        public ConfigSnapshotSummary(string version, DateTime createdAt, string createdBy, string sourceKind)
        {
            Version = version;
            CreatedAt = createdAt;
            CreatedBy = createdBy;
            SourceKind = sourceKind;
        }

        public string Version { get; }

        public DateTime CreatedAt { get; }

        public string CreatedBy { get; }

        public string SourceKind { get; }
    }
}
