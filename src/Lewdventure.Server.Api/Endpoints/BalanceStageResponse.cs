namespace Server.Api.Endpoints
{
    internal sealed class BalanceStageResponse
    {
        public int Id { get; set; }

        public bool IsBoss { get; set; }

        public float EnemyStatsMultiplier { get; set; }

        public List<BalanceEventResponse> Events { get; set; } = new();
    }
}
