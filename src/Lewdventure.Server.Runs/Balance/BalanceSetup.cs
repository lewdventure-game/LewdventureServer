namespace Server.Balance
{
    internal sealed class BalanceSetup
    {
        public int CharacterId { get; set; }

        public int PromoteLevel { get; set; } = 1;

        public float CurrentHealth { get; set; }

        public List<BalanceSummonSetup> Summons { get; set; } = new();

        public List<BalanceEquipmentSetup> Equipment { get; set; } = new();

        public List<BalanceBonusSetup> AccountBonuses { get; set; } = new();

        public List<int> RunPerks { get; set; } = new();

        public List<BalanceBonusSetup> RunBonuses { get; set; } = new();

        public List<int> RunStatuses { get; set; } = new();
    }
}
