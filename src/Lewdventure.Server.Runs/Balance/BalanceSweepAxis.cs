namespace Server.Balance
{
    internal sealed class BalanceSweepAxis
    {
        public string Sheet { get; set; } = string.Empty;

        public string Row { get; set; } = string.Empty;

        public string Column { get; set; } = string.Empty;

        public double From { get; set; }

        public double To { get; set; }

        public double Step { get; set; } = 1;
    }
}
