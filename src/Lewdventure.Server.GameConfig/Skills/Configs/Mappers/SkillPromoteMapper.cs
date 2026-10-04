using Newtonsoft.Json;
using Server.Configs;

namespace Server.Skills
{
    internal sealed class SkillPromoteMapper : ISkillPromoteMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("pattern_id")]
        public int PatternId { get; init; }

        [JsonProperty("level")]
        public int Level { get; init; }

        [JsonProperty("resource_types")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] ResourceTypes { get; init; } = Array.Empty<string>();

        [JsonProperty("resource_ids")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] ResourceIds { get; init; } = Array.Empty<string>();

        [JsonProperty("resource_values")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] ResourceValues { get; init; } = Array.Empty<int>();

        [JsonProperty("level_to_unlock")]
        public int LevelToUnlock { get; init; }
    }
}
