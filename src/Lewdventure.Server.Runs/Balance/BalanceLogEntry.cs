namespace Server.Balance
{
    internal sealed class BalanceLogEntry
    {
        public int Turn { get; set; }

        public string Phase { get; set; } = string.Empty;

        public string Text { get; set; } = string.Empty;
    }
}
