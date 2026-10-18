namespace Server.Api.Endpoints
{
    internal sealed class QaPerkUsageResponse
    {
        public int PerkId { get; set; }

        public int UsedCount { get; set; }

        public int RemainingUses { get; set; }
    }
}
