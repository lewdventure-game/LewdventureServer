namespace Server.Admin.Backend.Models
{
    public sealed class CheatCatalogModel
    {
        public string ConfigVersion { get; set; } = string.Empty;

        public List<CheatCatalogItemModel> Characters { get; set; } = new();

        public List<CheatCatalogItemModel> Summons { get; set; } = new();

        public List<CheatCatalogItemModel> Equipment { get; set; } = new();

        public List<CheatCatalogItemModel> Bonuses { get; set; } = new();

        public List<CheatCatalogItemModel> RunBonuses { get; set; } = new();

        public List<CheatCatalogItemModel> Perks { get; set; } = new();

        public List<CheatCatalogItemModel> Statuses { get; set; } = new();

        public List<CheatCatalogItemModel> Events { get; set; } = new();

        public List<string> Resources { get; set; } = new();

        public List<int> StoryLevels { get; set; } = new();
    }
}
