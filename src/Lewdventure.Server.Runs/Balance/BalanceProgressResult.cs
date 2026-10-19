namespace Server.Balance
{
    internal sealed class BalanceProgressResult
    {
        public int StoryLevelId { get; set; }

        public int NextStoryLevelId { get; set; }

        public int Players { get; set; }

        public int Sampled { get; set; }

        public BalanceDistribution Power { get; set; } = new();

        public BalanceDistribution PromoteLevel { get; set; } = new();

        public BalanceDistribution SummonLevel { get; set; } = new();

        public BalanceDistribution EquipmentLevel { get; set; } = new();

        public BalanceDistribution AccountBonuses { get; set; } = new();

        public string UserId { get; set; } = string.Empty;

        public float PickedPower { get; set; }

        public BalanceSetup Setup { get; set; } = new();
    }
}
