namespace Server.Balance
{
    internal sealed class BalanceLevelTally
    {
        private const int MaxStageIds = 8;

        public int Completed { get; set; }

        public int Battles { get; set; }

        public int BattlesWon { get; set; }

        public List<float> StagesCleared { get; } = new();

        public List<float> FinalExperienceLevel { get; } = new();

        public List<float> PerksPerRun { get; } = new();

        public List<BalanceLevelStageStats> Stages { get; } = new();

        public Dictionary<int, BalancePerkStats> Perks { get; } = new();

        public List<long> FailedSeeds { get; } = new();

        public BalanceLevelStageStats Stage(int stageIndex)
        {
            while (Stages.Count <= stageIndex)
                Stages.Add(new BalanceLevelStageStats { StageIndex = Stages.Count });

            return Stages[stageIndex];
        }

        public void AddStageId(int stageIndex, int stageId)
        {
            var stageIds = Stage(stageIndex).StageIds;

            if (stageIds.Contains(stageId) || MaxStageIds <= stageIds.Count)
                return;

            stageIds.Add(stageId);
        }

        public BalancePerkStats Perk(int perkId)
        {
            if (Perks.TryGetValue(perkId, out var stats) == false)
            {
                stats = new BalancePerkStats { PerkId = perkId };
                Perks[perkId] = stats;
            }

            return stats;
        }
    }
}
