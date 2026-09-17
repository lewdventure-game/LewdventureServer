namespace Server.Infrastructure.Mongo.ConfigSnapshots
{
    internal sealed class ConfigSnapshotDomainDocument
    {
        public string Domain { get; set; } = string.Empty;

        public string SpreadsheetId { get; set; } = string.Empty;

        public string Range { get; set; } = string.Empty;

        public string RowsJson { get; set; } = string.Empty;
    }
}
