namespace Server.Infrastructure.Mongo.Players
{
    internal sealed class PlayerLedgerEntryDocument
    {
        public string Type { get; set; } = string.Empty;

        public string Key { get; set; } = string.Empty;

        public long Amount { get; set; }
    }
}
