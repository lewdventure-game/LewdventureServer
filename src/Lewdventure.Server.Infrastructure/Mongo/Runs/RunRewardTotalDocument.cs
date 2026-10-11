namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunRewardTotalDocument
    {
        public string Type { get; set; } = string.Empty;

        public int Id { get; set; }

        public string RewardKey { get; set; } = string.Empty;

        public int Count { get; set; }
    }
}
