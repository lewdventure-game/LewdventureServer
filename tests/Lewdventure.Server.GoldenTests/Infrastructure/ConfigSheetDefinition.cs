namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigSheetDefinition
    {
        public ConfigSheetDefinition(string domain, string spreadsheetId, string range)
        {
            Domain = domain;
            SpreadsheetId = spreadsheetId;
            Range = range;
        }

        public string Domain { get; }

        public string SpreadsheetId { get; }

        public string Range { get; }
    }
}
