using Newtonsoft.Json;

namespace Server.Entities
{
    internal sealed class SummonLevelMapper : ISummonLevelMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("pattern_id")]
        public int PatternId { get; init; }

        [JsonProperty("level")]
        public int Level { get; init; }

        [JsonProperty("mastery_requirement")]
        public int MasteryRequirement { get; init; }

        [JsonProperty("resource_types")]
        public string ResourceTypes { get; init; } = string.Empty;

        [JsonProperty("resource_ids")]
        public string ResourceIds { get; init; } = string.Empty;

        [JsonProperty("resource_values")]
        public int ResourceValues { get; init; }
    }
}
