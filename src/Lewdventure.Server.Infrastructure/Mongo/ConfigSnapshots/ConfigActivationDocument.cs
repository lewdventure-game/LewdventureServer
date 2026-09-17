namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigActivationDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Version { get; set; } = string.Empty;

        public string PreviousVersion { get; set; } = string.Empty;

        public string ActivatedBy { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}
