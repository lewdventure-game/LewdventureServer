namespace Server.Api.Endpoints
{
    internal sealed class QaPendingBattleResponse
    {
        public int StageNumber { get; set; }

        public int EventId { get; set; }

        public bool IsWin { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public float FinalHealth { get; set; }

        public int StepCount { get; set; }

        public List<RunBonusResponse> BonusGrants { get; set; } = new();

        public List<QaPerkUsageResponse> PerkUsages { get; set; } = new();
    }
}
