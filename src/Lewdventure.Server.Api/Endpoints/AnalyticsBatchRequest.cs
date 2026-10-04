using System.Text.Json.Serialization;

namespace Server.Api.Endpoints
{
    internal sealed class AnalyticsBatchRequest
    {
        [JsonPropertyName("events")]
        public List<AnalyticsEventRequest> Events { get; set; } = new();
    }
}
