namespace Server.Api.Endpoints
{
    internal sealed class CheatCatalogItem
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public int MaxLevel { get; set; }

        public int MaxMastery { get; set; }
    }
}
