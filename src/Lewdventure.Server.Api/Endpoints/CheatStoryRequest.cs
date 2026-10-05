namespace Server.Api.Endpoints
{
    internal sealed class CheatStoryRequest
    {
        public List<int> CompletedLevelIds { get; set; } = new();
    }
}
