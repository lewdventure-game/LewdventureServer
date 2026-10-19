namespace Server.Admin.Backend.Models
{
    public sealed class BalanceFormState
    {
        public string Version { get; set; } = string.Empty;

        public int CharacterId { get; set; }

        public int PromoteLevel { get; set; } = 1;

        public float CurrentHealth { get; set; }

        public List<BalanceSummonModel> Summons { get; set; } = new();

        public List<BalanceEquipmentModel> Equipment { get; set; } = new();

        public string AccountBonusesText { get; set; } = string.Empty;

        public string RunPerksText { get; set; } = string.Empty;

        public string RunBonusesText { get; set; } = string.Empty;

        public string RunStatusesText { get; set; } = string.Empty;

        public string EditsText { get; set; } = string.Empty;

        public string Fight { get; set; } = string.Empty;

        public string EnemiesText { get; set; } = string.Empty;

        public ulong Seed { get; set; } = 1;

        public int Battles { get; set; } = 200;

        public int LevelId { get; set; }

        public int LevelRuns { get; set; } = 100;

        public string PerkPolicy { get; set; } = "first";

        public string PerkPriorityText { get; set; } = string.Empty;

        public string ForkPolicy { get; set; } = "firstButton";

        public string VersionB { get; set; } = string.Empty;

        public string EditsBText { get; set; } = string.Empty;

        public string CompareKind { get; set; } = "series";

        public string SweepKind { get; set; } = "series";

        public string SweepSheet { get; set; } = string.Empty;

        public string SweepRow { get; set; } = string.Empty;

        public string SweepColumn { get; set; } = string.Empty;

        public string SweepFrom { get; set; } = string.Empty;

        public string SweepTo { get; set; } = string.Empty;

        public string SweepStep { get; set; } = string.Empty;
    }
}
