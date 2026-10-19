namespace Server.Balance
{
    internal sealed class BalanceLevelResult
    {
        public int StoryLevelId { get; set; }

        public int Runs { get; set; }

        public int Completed { get; set; }

        public float CompletionRate { get; set; }

        public long FirstSeed { get; set; }

        public int Battles { get; set; }

        public int BattlesWon { get; set; }

        public BalanceDistribution StagesCleared { get; set; } = new();

        public BalanceDistribution FinalExperienceLevel { get; set; } = new();

        public BalanceDistribution PerksPerRun { get; set; } = new();

        public List<BalanceLevelStageStats> Stages { get; set; } = new();

        public List<BalancePerkStats> Perks { get; set; } = new();

        public List<long> FailedSeeds { get; set; } = new();
    }
}
