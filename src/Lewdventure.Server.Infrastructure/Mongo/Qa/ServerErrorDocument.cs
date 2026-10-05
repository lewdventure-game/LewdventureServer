namespace Server.Infrastructure.Mongo.Qa
{
    internal sealed class ServerErrorDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Level { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public string Exception { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;
    }
}
