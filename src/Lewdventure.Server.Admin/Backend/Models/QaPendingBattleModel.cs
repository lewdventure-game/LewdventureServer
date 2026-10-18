namespace Server.Admin.Backend.Models
{
    public sealed class QaPendingBattleModel
    {
        public int StageNumber { get; set; }

        public int EventId { get; set; }

        public bool IsWin { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public float FinalHealth { get; set; }

        public int StepCount { get; set; }

        public List<QaRunBonusModel> BonusGrants { get; set; } = new();

        public List<QaPerkUsageModel> PerkUsages { get; set; } = new();
    }
}
