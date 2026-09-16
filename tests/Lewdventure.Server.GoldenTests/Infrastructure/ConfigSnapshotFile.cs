using Newtonsoft.Json;

namespace Tests.Golden.Infrastructure
{
    internal sealed class ConfigSnapshotFile
    {
        [JsonProperty("format")]
        public string Format { get; set; } = string.Empty;

        [JsonProperty("formatVersion")]
        public int FormatVersion { get; set; }

        [JsonProperty("capturedAt")]
        public DateTime CapturedAt { get; set; }

        [JsonProperty("domains")]
        public List<ConfigSnapshotDomain> Domains { get; set; } = new();
    }
}
