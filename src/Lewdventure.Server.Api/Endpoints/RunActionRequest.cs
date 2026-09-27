namespace Server.Api.Endpoints
{
    internal sealed class RunActionRequest
    {
        public string RunId { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public List<int> Picks { get; set; } = new();
    }
}
