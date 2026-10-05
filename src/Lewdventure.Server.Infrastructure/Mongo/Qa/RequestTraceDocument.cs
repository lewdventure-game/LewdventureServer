namespace Server.Infrastructure.Mongo.Qa
{
    internal sealed class RequestTraceDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string CorrelationId { get; set; } = string.Empty;

        public string Method { get; set; } = string.Empty;

        public string Path { get; set; } = string.Empty;

        public string ClientVersion { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public long DurationMs { get; set; }

        public string RequestBody { get; set; } = string.Empty;

        public string ResponseBody { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;
    }
}
