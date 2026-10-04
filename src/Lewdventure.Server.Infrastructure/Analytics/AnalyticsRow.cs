using System.Text.Json.Serialization;

namespace Server.Infrastructure.Analytics
{
    internal sealed class AnalyticsRow
    {
        public const string ClientSource = "client";
        public const string ServerSource = "server";

        [JsonPropertyName("insert_id")]
        public string InsertId { get; set; } = string.Empty;

        [JsonPropertyName("event_type")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("event_time")]
        public string EventTime { get; set; } = string.Empty;

        [JsonPropertyName("server_time")]
        public string ServerTime { get; set; } = string.Empty;

        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("device_id")]
        public string DeviceId { get; set; } = string.Empty;

        [JsonPropertyName("session_id")]
        public long SessionId { get; set; }

        [JsonPropertyName("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonPropertyName("app_version")]
        public string AppVersion { get; set; } = string.Empty;

        [JsonPropertyName("country")]
        public string Country { get; set; } = string.Empty;

        [JsonPropertyName("experiment_id")]
        public string ExperimentId { get; set; } = string.Empty;

        [JsonPropertyName("group_id")]
        public string GroupId { get; set; } = string.Empty;

        [JsonPropertyName("config_version")]
        public string ConfigVersion { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string Source { get; set; } = ClientSource;

        [JsonPropertyName("event_properties")]
        public string EventProperties { get; set; } = "{}";

        [JsonPropertyName("user_properties")]
        public string UserProperties { get; set; } = "{}";
    }
}
