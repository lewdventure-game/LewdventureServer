namespace Server.Balance
{
    internal sealed class BalanceSweepResult : IBalanceRunResult
    {
        public string Kind { get; set; } = string.Empty;

        public BalanceSweepAxis Axis { get; set; } = new();

        public List<BalanceSweepPoint> Points { get; set; } = new();

        public string RunId { get; set; } = string.Empty;
    }
}
