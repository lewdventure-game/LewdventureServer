namespace Server.Infrastructure.Alerts
{
    internal sealed class AlertMessage
    {
        public AlertMessage(AlertSeverity severity, string title, string description, string dedupKey)
        {
            Severity = severity;
            Title = title;
            Description = description;
            DedupKey = dedupKey;
        }

        public AlertSeverity Severity { get; }

        public string Title { get; }

        public string Description { get; }

        public string DedupKey { get; }

        public List<KeyValuePair<string, string>> Fields { get; } = new();
    }
}
