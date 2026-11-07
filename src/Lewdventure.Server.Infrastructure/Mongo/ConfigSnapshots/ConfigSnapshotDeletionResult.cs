namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotDeletionResult
    {
        public ConfigSnapshotDeletionStatus Status { get; set; } = ConfigSnapshotDeletionStatus.Unknown;

        public bool Succeeded => Status == ConfigSnapshotDeletionStatus.Ok;

        public List<string> Errors { get; } = new();
    }
}
