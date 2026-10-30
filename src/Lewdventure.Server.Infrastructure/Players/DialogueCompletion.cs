namespace Server.Infrastructure.Players
{
    internal sealed class DialogueCompletion
    {
        public DialogueCompletion(string launchKey, IReadOnlyList<int> picks)
        {
            LaunchKey = launchKey;
            Picks = picks;
        }

        public string LaunchKey { get; }

        public IReadOnlyList<int> Picks { get; }
    }
}
