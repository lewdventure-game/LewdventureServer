namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunPendingPerkUsageDocument
    {
        public int PerkId { get; set; }

        public int UsedCount { get; set; }

        public int RemainingUses { get; set; }
    }
}
