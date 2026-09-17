using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Server.GameConfigs
{
    internal sealed class ConfigSnapshotFileDomain
    {
        [JsonProperty("domain")]
        public string Domain { get; set; } = string.Empty;

        [JsonProperty("spreadsheetId")]
        public string SpreadsheetId { get; set; } = string.Empty;

        [JsonProperty("range")]
        public string Range { get; set; } = string.Empty;

        [JsonProperty("rows")]
        public JArray Rows { get; set; } = new();
    }
}
