namespace Server.Api.Endpoints
{
    internal sealed class DialogueLaunchResponse
    {
        public string LaunchKey { get; set; } = string.Empty;

        public int DialogueId { get; set; }
    }
}
