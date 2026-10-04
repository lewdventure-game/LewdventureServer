namespace Server.Infrastructure.Mongo.Experiments
{
    internal sealed class ExperimentGroupDocument
    {
        public const string RecruitingStatus = "recruiting";
        public const string FrozenStatus = "frozen";
        public const string RemovedStatus = "removed";

        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string SnapshotVersion { get; set; } = string.Empty;

        public double Percent { get; set; }

        public ExperimentFilterDocument Filter { get; set; } = new();

        public string Status { get; set; } = RecruitingStatus;

        public DateTime? FrozenAt { get; set; }

        public DateTime? RemovedAt { get; set; }
    }
}
