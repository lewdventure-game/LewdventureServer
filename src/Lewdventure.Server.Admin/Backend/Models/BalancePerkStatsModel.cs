namespace Server.Admin.Backend.Models
{
    public sealed class BalancePerkStatsModel
    {
        public int PerkId { get; set; }

        public int Offered { get; set; }

        public int Picked { get; set; }

        public float PickRate { get; set; }
    }
}
