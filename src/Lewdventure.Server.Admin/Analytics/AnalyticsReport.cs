namespace Server.Admin.Analytics
{
    public sealed class AnalyticsReport
    {
        public long Events { get; set; }

        public long Users { get; set; }

        public double MetricValue { get; set; }

        public List<ChartSeries> Series { get; set; } = new();

        public List<BreakdownRow> Breakdown { get; set; } = new();

        public List<EventRow> Latest { get; set; } = new();

        public List<string> EventTypes { get; set; } = new();

        public List<string> PropertyKeys { get; set; } = new();

        public List<string> Errors { get; set; } = new();
    }
}
