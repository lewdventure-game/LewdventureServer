namespace Server.Api.Endpoints
{
    internal sealed class BalanceCatalogResponse
    {
        public CheatCatalogResponse Items { get; set; } = new();

        public List<BalanceStoryLevelResponse> StoryLevels { get; set; } = new();

        public List<BalanceEnemyResponse> Enemies { get; set; } = new();

        public List<BalanceSheetResponse> Sheets { get; set; } = new();
    }
}
