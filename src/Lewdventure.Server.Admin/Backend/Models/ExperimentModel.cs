namespace Server.Admin.Backend.Models
{
    public sealed class ExperimentModel
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;

        public List<ExperimentGroupModel> Groups { get; set; } = new();
    }
}
