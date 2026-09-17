namespace Server.Api.Options
{
    internal sealed class ConfigPublisherOptions
    {
        public const string SectionName = "ConfigPublisher";

        public bool Enabled { get; set; }

        public string ApiKey { get; set; } = string.Empty;

        public string HeaderName { get; set; } = "X-Config-Key";

        public string LegacyHeaderName { get; set; } = "X-Config-Secret";
    }
}
