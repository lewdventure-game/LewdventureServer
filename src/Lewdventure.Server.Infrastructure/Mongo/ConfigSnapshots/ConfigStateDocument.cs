namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigStateDocument : IMongoDocument
    {
        public const string ActiveId = "active";
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = ActiveId;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Version { get; set; } = string.Empty;

        public string ActivationId { get; set; } = string.Empty;
    }
}
