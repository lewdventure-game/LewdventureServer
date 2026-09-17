using Newtonsoft.Json;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotFileModel
    {
        public const string FormatName = "lewdventure-config-snapshot";

        [JsonProperty("format")]
        public string Format { get; set; } = FormatName;

        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; } = GameConfigSnapshot.CurrentFormatVersion;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("createdAt")]
        public string CreatedAt { get; set; } = string.Empty;

        [JsonProperty("sourceKind")]
        public string SourceKind { get; set; } = string.Empty;

        [JsonProperty("domains")]
        public List<ConfigSnapshotFileDomain> Domains { get; set; } = new();
    }
}
