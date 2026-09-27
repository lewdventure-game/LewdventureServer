namespace Server.Api.Endpoints
{
    internal sealed class PlayerGrantRequest
    {
        public string Rewards { get; set; } = string.Empty;

        public string Reason { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;
    }
}
