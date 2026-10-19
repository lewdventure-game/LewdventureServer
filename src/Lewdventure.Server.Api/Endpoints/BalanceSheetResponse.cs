namespace Server.Api.Endpoints
{
    internal sealed class BalanceSheetResponse
    {
        public string Name { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public int Rows { get; set; }

        public List<string> Columns { get; set; } = new();
    }
}
