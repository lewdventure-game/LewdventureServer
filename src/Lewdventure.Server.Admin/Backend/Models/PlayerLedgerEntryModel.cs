namespace Server.Admin.Backend.Models
{
    public sealed class PlayerLedgerEntryModel
    {
        public string Type { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public long Amount { get; set; }
    }
}
