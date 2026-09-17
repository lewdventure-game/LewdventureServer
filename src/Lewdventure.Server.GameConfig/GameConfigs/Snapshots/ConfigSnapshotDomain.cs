namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotDomain
    {
        public ConfigSnapshotDomain(string domain, string spreadsheetId, string range, string rowsJson)
        {
            Domain = domain;
            SpreadsheetId = spreadsheetId;
            Range = range;
            RowsJson = rowsJson;
        }

        public string Domain { get; }

        public string SpreadsheetId { get; }

        public string Range { get; }

        public string RowsJson { get; }
    }
}
