namespace Server.Api.Endpoints
{
    internal sealed class BalanceScenarioResponse
    {
        public string Id { get; set; } = string.Empty;

        public string Name { get; set; } = string.Empty;

        public string CreatedBy { get; set; } = string.Empty;

        public string UpdatedBy { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public DateTime UpdatedAt { get; set; }

        public string Payload { get; set; } = string.Empty;
    }
}
