namespace Server.Admin.Backend.Models
{
    public sealed class QaPerkUsageModel
    {
        public int PerkId { get; set; }

        public int UsedCount { get; set; }

        public int RemainingUses { get; set; }
    }
}
