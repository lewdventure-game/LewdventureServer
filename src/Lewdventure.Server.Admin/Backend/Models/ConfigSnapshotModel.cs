namespace Server.Admin.Backend.Models
{
    public sealed class ConfigSnapshotModel
    {
        public string Version { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        public string CreatedBy { get; set; } = string.Empty;

        public string SourceKind { get; set; } = string.Empty;
    }
}
