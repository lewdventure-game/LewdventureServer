namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;
        public const string DraftStatus = "draft";
        public const string RunningStatus = "running";
        public const string FinishedStatus = "finished";

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = DraftStatus;

        public List<ExperimentGroupDocument> Groups { get; set; } = new();

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;

        public long Rev { get; set; }
    }
}
