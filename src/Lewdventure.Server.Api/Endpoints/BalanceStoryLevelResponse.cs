namespace Server.Api.Endpoints
{
    internal sealed class BalanceStoryLevelResponse
    {
        public int Id { get; set; }

        public int MaxBattleTurns { get; set; }

        public float EnemiesHealthMultiplier { get; set; }

        public float EnemiesAttackMultiplier { get; set; }

        public List<BalanceStageResponse> Stages { get; set; } = new();
    }
}
