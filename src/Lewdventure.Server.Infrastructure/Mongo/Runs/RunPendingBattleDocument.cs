namespace Server.Infrastructure.Mongo.Runs
{
    internal sealed class RunPendingBattleDocument
    {
        public int StageIndex { get; set; }

        public int EventId { get; set; }

        public bool IsWin { get; set; }

        public string Outcome { get; set; } = string.Empty;

        public float FinalHealth { get; set; }

        public List<RunBonusDocument> BonusGrants { get; set; } = new();

        public List<RunPendingPerkUsageDocument> PerkUsages { get; set; } = new();

        public string BattleDigest { get; set; } = string.Empty;

        public int BattleStepCount { get; set; }
    }
}
