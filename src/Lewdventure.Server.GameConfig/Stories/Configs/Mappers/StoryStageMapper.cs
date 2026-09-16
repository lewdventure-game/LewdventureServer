using Newtonsoft.Json;
using Server.Configs;

namespace Server.Stories
{
    internal sealed class StoryStageMapper : IStoryStageMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("is_boss_lvl")]
        public bool IsBossLevel { get; init; }

        [JsonProperty("event_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] EventIds { get; init; } = Array.Empty<int>();

        [JsonProperty("event_chances")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] EventChances { get; init; } = Array.Empty<int>();

        [JsonProperty("enemy_stats_multiplier")]
        public float EnemyStatsMultiplier { get; init; } = 1f;
    }
}
