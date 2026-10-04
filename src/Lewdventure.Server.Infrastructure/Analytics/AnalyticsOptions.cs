namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsOptions
    {
        public const string SectionName = "Analytics";

        public bool Enabled { get; set; }

        public string ClickHouseUrl { get; set; } = string.Empty;

        public string Database { get; set; } = string.Empty;

        public string User { get; set; } = string.Empty;

        public string Password { get; set; } = string.Empty;

        public int QueueCapacity { get; set; } = 20000;

        public int BatchSize { get; set; } = 2000;

        public int FlushIntervalSeconds { get; set; } = 5;

        public string SpoolPath { get; set; } = string.Empty;

        public int SpoolMaxMegabytes { get; set; } = 100;

        public int MaxEventsPerRequest { get; set; } = 500;

        public int MaxPropertiesBytes { get; set; } = 16384;
    }
}
