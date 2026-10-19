namespace Server.Admin.Backend.Models
{
    public sealed class BalanceSetupModel
    {
        public int CharacterId { get; set; }

        public int PromoteLevel { get; set; } = 1;

        public float CurrentHealth { get; set; }

        public List<BalanceSummonModel> Summons { get; set; } = new();

        public List<BalanceEquipmentModel> Equipment { get; set; } = new();

        public List<BalanceBonusModel> AccountBonuses { get; set; } = new();

        public List<int> RunPerks { get; set; } = new();

        public List<BalanceBonusModel> RunBonuses { get; set; } = new();

        public List<int> RunStatuses { get; set; } = new();
    }
}
