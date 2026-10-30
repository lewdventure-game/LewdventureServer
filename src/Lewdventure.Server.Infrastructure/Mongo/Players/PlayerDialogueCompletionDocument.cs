namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerDialogueCompletionDocument
    {
        public string LaunchKey { get; set; } = string.Empty;

        public int DialogueId { get; set; }

        public List<int> Picks { get; set; } = new();

        public DateTime CompletedAt { get; set; }
    }
}
