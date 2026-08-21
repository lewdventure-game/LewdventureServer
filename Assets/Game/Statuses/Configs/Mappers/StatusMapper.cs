using Newtonsoft.Json;

namespace Server.Statuses
{
    internal sealed class StatusMapper : IStatusMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("status_type")]
        public StatusType StatusType { get; init; }

        [JsonProperty("proc_order")]
        public int TriggerOrder { get; init; }

        [JsonProperty("icon_art_name")]
        public string IconArtName { get; init; } = string.Empty;

        [JsonProperty("parameters")]
        public string Parameters { get; init; } = string.Empty;

        [JsonProperty("status_target")]
        public StatusTargetType StatusTarget { get; init; }
    }
}
