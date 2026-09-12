using Newtonsoft.Json;
using Server.Configs;

namespace Server.Stories
{
    internal sealed class StoryLevelMapper : IStoryLevelMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("background")]
        public string Background { get; init; } = string.Empty;

        [JsonProperty("start_phrase_loc_key")]
        public string StartPhraseLocKey { get; init; } = string.Empty;

        [JsonProperty("stage_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] StageIds { get; init; } = Array.Empty<int>();

        [JsonProperty("exp_level_id")]
        public int ExperienceLevelId { get; init; }

        [JsonProperty("max_battle_turns")]
        public int MaxBattleTurns { get; init; }

        [JsonProperty("trigger_types")]
        [JsonConverter(typeof(DelimitedEnumArrayConverter<StoryLevelTriggerType>), ';')]
        public StoryLevelTriggerType[] TriggerTypes { get; init; } = Array.Empty<StoryLevelTriggerType>();

        [JsonProperty("trigger_values")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] TriggerValues { get; init; } = Array.Empty<int>();
    }
}
