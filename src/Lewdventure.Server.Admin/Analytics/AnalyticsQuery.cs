namespace Server.Admin.Analytics
{
    internal sealed class AnalyticsQuery
    {
        public string Table { get; set; } = string.Empty;

        public string Where { get; set; } = string.Empty;

        public string MetricExpression { get; set; } = string.Empty;

        public string GroupExpression { get; set; } = string.Empty;

        public string BucketExpression { get; set; } = string.Empty;

        public Dictionary<string, string> Parameters { get; } = new(StringComparer.Ordinal);

        public DateTime From { get; set; }

        public DateTime To { get; set; }

        public bool IsHourly { get; set; }
    }
}
