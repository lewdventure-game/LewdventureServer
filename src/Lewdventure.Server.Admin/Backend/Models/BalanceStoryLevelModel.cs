namespace Server.Admin.Backend.Models
{
    public sealed class BalanceStoryLevelModel
    {
        public int Id { get; set; }

        public int MaxBattleTurns { get; set; }

        public float EnemiesHealthMultiplier { get; set; }

        public float EnemiesAttackMultiplier { get; set; }

        public List<BalanceStageModel> Stages { get; set; } = new();
    }
}
