namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal enum ConfigSnapshotDeletionStatus
    {
        Unknown = 0,
        Ok = 1,
        NotFound = 2,
        Conflict = 3,
    }
}
