namespace Server.Api.Endpoints
{
    internal sealed class BalanceRunResponse
    {
        public string Id { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string Actor { get; set; } = string.Empty;

        public string Kind { get; set; } = string.Empty;

        public string ConfigVersion { get; set; } = string.Empty;

        public int Edits { get; set; }

        public string Summary { get; set; } = string.Empty;

        public string Payload { get; set; } = string.Empty;
    }
}
