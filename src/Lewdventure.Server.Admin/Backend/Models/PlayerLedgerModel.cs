namespace Server.Admin.Backend.Models
{
    public sealed class PlayerLedgerModel
    {
        public string Id { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string Action { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string RequestId { get; set; } = string.Empty;

        public List<PlayerLedgerEntryModel> Entries { get; set; } = new();
    }
}
