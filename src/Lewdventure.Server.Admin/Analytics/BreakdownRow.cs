namespace Server.Admin.Analytics
{
    public sealed class BreakdownRow
    {
        public string Group { get; set; } = string.Empty;

        public long Events { get; set; }

        public long Users { get; set; }

        public double Metric { get; set; }
    }
}
