namespace Server.Admin.Backend.Models
{
    public sealed class CheatCatalogItemModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string Details { get; set; } = string.Empty;

        public int MaxLevel { get; set; }

        public int MaxMastery { get; set; }
    }
}
