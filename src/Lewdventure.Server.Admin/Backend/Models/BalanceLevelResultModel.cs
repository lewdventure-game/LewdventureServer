namespace Server.Admin.Backend.Models
{
    public sealed class BalanceLevelResultModel
    {
        public int StoryLevelId { get; set; }

        public int Runs { get; set; }

        public int Completed { get; set; }

        public float CompletionRate { get; set; }

        public long FirstSeed { get; set; }

        public int Battles { get; set; }

        public int BattlesWon { get; set; }

        public BalanceDistributionModel StagesCleared { get; set; } = new();

        public BalanceDistributionModel FinalExperienceLevel { get; set; } = new();

        public BalanceDistributionModel PerksPerRun { get; set; } = new();

        public List<BalanceLevelStageStatsModel> Stages { get; set; } = new();

        public List<BalancePerkStatsModel> Perks { get; set; } = new();

        public List<long> FailedSeeds { get; set; } = new();

        public string RunId { get; set; } = string.Empty;
    }
}
