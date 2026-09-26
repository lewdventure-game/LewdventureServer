namespace Server.GameConfigs
{
    internal sealed class UploadedSheet
    {
        public UploadedSheet(string domain, string spreadsheetId, string range, IReadOnlyList<IReadOnlyList<object?>> values)
        {
            Domain = domain;
            SpreadsheetId = spreadsheetId;
            Range = range;
            Values = values;
        }

        public string Domain { get; }

        public string SpreadsheetId { get; }

        public string Range { get; }

        public IReadOnlyList<IReadOnlyList<object?>> Values { get; }
    }
}
