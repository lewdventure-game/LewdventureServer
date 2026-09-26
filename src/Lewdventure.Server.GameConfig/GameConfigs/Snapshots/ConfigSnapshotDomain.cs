namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotDomain
    {
        public ConfigSnapshotDomain(string domain, string spreadsheetId, string range, string rowsJson)
            : this(domain, spreadsheetId, range, rowsJson, Array.Empty<int>())
        {
        }

        public ConfigSnapshotDomain(string domain, string spreadsheetId, string range, string rowsJson, IReadOnlyList<int> sourceRows)
        {
            Domain = domain;
            SpreadsheetId = spreadsheetId;
            Range = range;
            RowsJson = rowsJson;
            SourceRows = sourceRows;
        }

        public string Domain { get; }

        public string SpreadsheetId { get; }

        public string Range { get; }

        public string RowsJson { get; }

        public IReadOnlyList<int> SourceRows { get; }
    }
}
