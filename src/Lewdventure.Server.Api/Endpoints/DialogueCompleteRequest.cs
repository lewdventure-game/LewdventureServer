namespace Server.Api.Endpoints
{
    internal sealed class DialogueCompleteRequest
    {
        public string RequestId { get; set; } = string.Empty;

        public string LaunchKey { get; set; } = string.Empty;

        public List<int> Picks { get; set; } = new();
    }
}
