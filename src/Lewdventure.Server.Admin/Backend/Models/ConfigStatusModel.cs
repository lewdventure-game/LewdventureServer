namespace Server.Admin.Backend.Models
{
    public sealed class ConfigStatusModel
    {
        public string LoadedVersion { get; set; } = string.Empty;

        public string LoadedShortVersion { get; set; } = string.Empty;

        public string Source { get; set; } = string.Empty;

        public DateTime LoadedAt { get; set; }

        public string ActiveVersion { get; set; } = string.Empty;

        public bool InSync { get; set; }
    }
}
