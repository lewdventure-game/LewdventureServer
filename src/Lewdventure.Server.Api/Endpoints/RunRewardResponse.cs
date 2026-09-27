namespace Server.Api.Endpoints
{
    internal sealed class RunRewardResponse
    {
        public string Type { get; set; } = string.Empty;

        public int Id { get; set; }

        public string RewardKey { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}
