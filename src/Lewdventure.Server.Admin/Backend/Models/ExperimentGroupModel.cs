namespace Server.Admin.Backend.Models
{
    public sealed class ExperimentGroupModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string SnapshotVersion { get; set; } = string.Empty;

        public double Percent { get; set; }

        public bool NewPlayersOnly { get; set; }

        public List<string> Countries { get; set; } = new();

        public string Status { get; set; } = string.Empty;

        public DateTime? FrozenAt { get; set; }

        public DateTime? RemovedAt { get; set; }

        public long? Participants { get; set; }
    }
}
