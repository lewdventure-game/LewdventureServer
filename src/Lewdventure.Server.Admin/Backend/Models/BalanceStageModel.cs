namespace Server.Admin.Backend.Models
{
    public sealed class BalanceStageModel
    {
        public int Id { get; set; }

        public bool IsBoss { get; set; }

        public float EnemyStatsMultiplier { get; set; }

        public List<BalanceEventModel> Events { get; set; } = new();
    }
}
