namespace Server.Api.Endpoints
{
    internal sealed class RunStartRequest
    {
        public int StoryLevelId { get; set; }

        public string RequestId { get; set; } = string.Empty;
    }
}
