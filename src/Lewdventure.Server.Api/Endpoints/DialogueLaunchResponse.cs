namespace Server.Api.Endpoints
{
    internal sealed class DialogueLaunchResponse
    {
        public string LaunchKey { get; set; } = string.Empty;

        public int DialogueId { get; set; }

        public string Trigger { get; set; } = string.Empty;
    }
}
