namespace Server.Infrastructure.Mongo.Balance
{
    internal sealed class BalanceJobDocument : IMongoDocument
    {
        public const int CurrentSchemaVersion = 1;
        public const string QueuedStatus = "queued";
        public const string RunningStatus = "running";
        public const string DoneStatus = "done";
        public const string FailedStatus = "failed";

        public string Id { get; set; } = string.Empty;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Actor { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string Status { get; set; } = QueuedStatus;

        public string Request { get; set; } = string.Empty;

        public string Result { get; set; } = string.Empty;

        public string Error { get; set; } = string.Empty;

        public int StatusCode { get; set; }

        public int Done { get; set; }

        public int Total { get; set; }

        public string RunId { get; set; } = string.Empty;

        public string WorkerId { get; set; } = string.Empty;

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }
    }
}
