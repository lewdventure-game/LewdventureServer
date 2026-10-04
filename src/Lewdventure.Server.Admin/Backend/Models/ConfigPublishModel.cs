namespace Server.Admin.Backend.Models
{
    public sealed class ConfigPublishModel
    {
        public bool Succeeded { get; set; }

        public string Version { get; set; } = string.Empty;

        public string ShortVersion { get; set; } = string.Empty;

        public string PreviousVersion { get; set; } = string.Empty;

        public bool Activated { get; set; }
    }
}
