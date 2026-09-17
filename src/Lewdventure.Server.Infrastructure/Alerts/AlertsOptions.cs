using System.ComponentModel.DataAnnotations;

namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertsOptions
    {
        public const string SectionName = "Alerts";

        public bool Enabled { get; set; }

        public string DiscordWebhookUrl { get; set; } = string.Empty;

        public string EnvironmentLabel { get; set; } = string.Empty;

        [Range(0, 86400)]
        public int CooldownSeconds { get; set; } = 300;

        [Range(5, 3600)]
        public int HealthCheckPeriodSeconds { get; set; } = 30;

        public bool NotifyOnStartup { get; set; }

        [Range(1, 60)]
        public int HttpTimeoutSeconds { get; set; } = 5;

        [Range(8, 10000)]
        public int QueueCapacity { get; set; } = 256;
    }
}
