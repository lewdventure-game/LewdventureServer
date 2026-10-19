namespace Server.Balance
{
    internal sealed class BalancePerkStats
    {
        public int PerkId { get; set; }

        public int Offered { get; set; }

        public int Picked { get; set; }

        public float PickRate { get; set; }
    }
}
