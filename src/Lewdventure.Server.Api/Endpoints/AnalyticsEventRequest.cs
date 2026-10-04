using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Api.Endpoints
{
    internal sealed class AnalyticsEventRequest
    {
        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("time")]
        public long Time { get; set; }

        [JsonPropertyName("event_properties")]
        public JsonElement? EventProperties { get; set; }

        [JsonPropertyName("user_properties")]
        public JsonElement? UserProperties { get; set; }

        [JsonPropertyName("session_id")]
        public long SessionId { get; set; }

        [JsonPropertyName("insert_id")]
        public string InsertId { get; set; } = string.Empty;

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;
    }
}
