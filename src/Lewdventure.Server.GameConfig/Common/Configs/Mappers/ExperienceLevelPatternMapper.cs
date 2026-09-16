using Newtonsoft.Json;

namespace Server.Configs
{
    internal sealed class ExperienceLevelPatternMapper : IExperienceLevelPatternMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("exp_level")]
        public int ExperienceLevel { get; init; }

        [JsonProperty("exp_for_next_lvl")]
        public int ExperienceForNextLevel { get; init; }

        [JsonProperty("perk_preset_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] PerkPresetIds { get; init; } = Array.Empty<int>();

        [JsonProperty("perk_preset_weights")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] PerkPresetWeights { get; init; } = Array.Empty<int>();
    }
}
