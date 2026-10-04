namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsPlayerContext
    {
        public string ExperimentId { get; set; } = string.Empty;

        public string GroupId { get; set; } = string.Empty;

        public string ConfigVersion { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;
    }
}
