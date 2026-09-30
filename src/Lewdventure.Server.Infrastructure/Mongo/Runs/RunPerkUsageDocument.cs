namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunPerkUsageDocument
    {
        public int PerkId { get; set; }

        public int UsedCount { get; set; }
    }
}
