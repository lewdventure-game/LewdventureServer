namespace Server.Admin.Backend.Models
{
    public sealed class BalanceProgressModel
    {
        public int StoryLevelId { get; set; }

        public int NextStoryLevelId { get; set; }

        public int Players { get; set; }

        public int Sampled { get; set; }

        public BalanceDistributionModel Power { get; set; } = new();

        public BalanceDistributionModel PromoteLevel { get; set; } = new();

        public BalanceDistributionModel SummonLevel { get; set; } = new();

        public BalanceDistributionModel EquipmentLevel { get; set; } = new();

        public BalanceDistributionModel AccountBonuses { get; set; } = new();

        public string UserId { get; set; } = string.Empty;

        public float PickedPower { get; set; }

        public BalanceSetupModel Setup { get; set; } = new();
    }
}
