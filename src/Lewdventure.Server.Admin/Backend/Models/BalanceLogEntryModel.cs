namespace Server.Admin.Backend.Models
{
    public sealed class BalanceLogEntryModel
    {
        public int Turn { get; set; }

        public string Phase { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}
