using Server.Admin.Backend.Models;

namespace Server.Admin.Pages.Balance
{
    internal sealed class BalanceEditsEditor
    {
        public string Prefix { get; set; } = string.Empty;

        public List<BalanceConfigEditModel> Rows { get; set; } = new();

        public List<BalanceSheetModel> Sheets { get; set; } = new();
    }
}
