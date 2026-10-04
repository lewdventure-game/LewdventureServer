namespace Server.Api.Endpoints
{
    internal sealed class ExperimentResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public string Status { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime? StartedAt { get; set; }

        public DateTime? FinishedAt { get; set; }

        public string UpdatedBy { get; set; } = string.Empty;

        public List<ExperimentGroupResponse> Groups { get; set; } = new();
    }
}
