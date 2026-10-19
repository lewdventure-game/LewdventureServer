namespace Server.Admin.Backend.Models
{
    public sealed class BalanceCatalogModel
    {
        public CheatCatalogModel Items { get; set; } = new();

        public List<BalanceStoryLevelModel> StoryLevels { get; set; } = new();

        public List<BalanceEnemyModel> Enemies { get; set; } = new();
    }
}
