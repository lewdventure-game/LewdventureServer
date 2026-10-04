namespace Server.Admin.Backend.Models
{
    public sealed class PlayerStoryModel
    {
        public List<int> CompletedLevelIds { get; set; } = new();

        public string CurrentRunId { get; set; } = string.Empty;
    }
}
