namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerStoryDocument
    {
        public List<int> CompletedLevelIds { get; set; } = new();

        public string CurrentRunId { get; set; } = string.Empty;
    }
}
