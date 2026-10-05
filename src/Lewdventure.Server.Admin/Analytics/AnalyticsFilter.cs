namespace Server.Admin.Analytics
{
    public sealed class AnalyticsFilter
    {
        public string From { get; set; } = string.Empty;

        public string To { get; set; } = string.Empty;

        public string Step { get; set; } = AnalyticsFilterValues.StepDay;

        public string EventType { get; set; } = string.Empty;

        public string Metric { get; set; } = AnalyticsFilterValues.MetricEvents;

        public string MetricKey { get; set; } = string.Empty;

        public string GroupBy { get; set; } = AnalyticsFilterValues.GroupNone;

        public string GroupKey { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string PropertyKey { get; set; } = string.Empty;

        public string PropertyValue { get; set; } = string.Empty;

        public bool IncludeQa { get; set; }
    }
}
