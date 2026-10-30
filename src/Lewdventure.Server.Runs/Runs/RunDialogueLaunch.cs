namespace Server.Runs
{
    internal sealed class RunDialogueLaunch
    {
        public RunDialogueLaunch(string launchKey, int dialogueId, string trigger)
        {
            LaunchKey = launchKey;
            DialogueId = dialogueId;
            Trigger = trigger;
        }

        public string LaunchKey { get; }

        public int DialogueId { get; }

        public string Trigger { get; }
    }
}
