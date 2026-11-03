namespace Server.Infrastructure.Players
{
    internal sealed class DialogueCompletion
    {
        public DialogueCompletion(string launchKey, IReadOnlyList<int> picks, string requestId)
        {
            LaunchKey = launchKey;
            Picks = picks;
            RequestId = requestId;
        }

        public string LaunchKey { get; }

        public IReadOnlyList<int> Picks { get; }

        public string RequestId { get; }
    }
}
