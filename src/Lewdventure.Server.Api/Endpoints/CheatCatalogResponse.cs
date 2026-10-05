namespace Server.Api.Endpoints
{
    internal sealed class CheatCatalogResponse
    {
        public string ConfigVersion { get; set; } = string.Empty;

        public List<CheatCatalogItem> Characters { get; set; } = new();

        public List<CheatCatalogItem> Summons { get; set; } = new();

        public List<CheatCatalogItem> Equipment { get; set; } = new();

        public List<CheatCatalogItem> Bonuses { get; set; } = new();

        public List<CheatCatalogItem> RunBonuses { get; set; } = new();

        public List<CheatCatalogItem> Perks { get; set; } = new();

        public List<CheatCatalogItem> Statuses { get; set; } = new();

        public List<CheatCatalogItem> Events { get; set; } = new();

        public List<string> Resources { get; set; } = new();

        public List<int> StoryLevels { get; set; } = new();
    }
}
