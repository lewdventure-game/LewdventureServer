namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerDialogueLaunchDocument
    {
        public string LaunchKey { get; set; } = string.Empty;

        public int DialogueId { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
