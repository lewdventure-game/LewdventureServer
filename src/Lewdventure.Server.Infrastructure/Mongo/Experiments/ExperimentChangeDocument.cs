namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentChangeDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string ExperimentId { get; set; } = string.Empty;

        public string Action { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string Actor { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;
    }
}
