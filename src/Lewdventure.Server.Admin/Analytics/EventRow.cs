namespace Server.Admin.Analytics
{
    public sealed class EventRow
    {
        public DateTime Time { get; set; }

        public string EventType { get; set; } = string.Empty;

        public string UserId { get; set; } = string.Empty;

        public string Country { get; set; } = string.Empty;

        public string Experiment { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public string AppVersion { get; set; } = string.Empty;

        public string Properties { get; set; } = string.Empty;
    }
}
