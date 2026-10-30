namespace Server.Runs
{
    internal sealed class RunDialogueLaunch
    {
        public RunDialogueLaunch(string launchKey, int dialogueId)
        {
            LaunchKey = launchKey;
            DialogueId = dialogueId;
        }

        public string LaunchKey { get; }

        public int DialogueId { get; }
    }
}
