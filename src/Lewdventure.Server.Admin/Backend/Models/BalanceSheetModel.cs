namespace Server.Admin.Backend.Models
{
    public sealed class BalanceSheetModel
    {
        public string Name { get; set; } = string.Empty;

        public string RowKey { get; set; } = string.Empty;

        public int Rows { get; set; }

        public List<string> Columns { get; set; } = new();
    }
}
