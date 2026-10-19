namespace Server.Admin.Backend.Models
{
    public sealed class BalanceBonusModel
    {
        public int Id { get; set; }

        public int Count { get; set; } = 1;

        public int RemainingBattles { get; set; }
    }
}
