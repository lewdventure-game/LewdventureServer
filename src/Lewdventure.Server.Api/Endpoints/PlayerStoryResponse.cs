namespace Server.Api.Endpoints
{
    internal sealed class PlayerStoryResponse
    {
        public List<int> CompletedLevelIds { get; set; } = new();

        public string CurrentRunId { get; set; } = string.Empty;
    }
}
