namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerDialoguesDocument
    {
        public List<PlayerDialogueLaunchDocument> Pending { get; set; } = new();

        public List<PlayerDialogueCompletionDocument> Completed { get; set; } = new();
    }
}
