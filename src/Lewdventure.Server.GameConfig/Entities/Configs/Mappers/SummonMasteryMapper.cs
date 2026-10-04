using Newtonsoft.Json;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class SummonMasteryMapper : ISummonMasteryMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("mastery_level")]
        public int MasteryLevel { get; init; }

        [JsonProperty("copies_to_upgrade")]
        public int CopiesToUpgrade { get; init; }

        [JsonProperty("dmg_multiplier")]
        public float DamageMultiplier { get; init; }

        [JsonProperty("reward_types")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] RewardTypes { get; init; } = Array.Empty<string>();

        [JsonProperty("reward_ids")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] RewardIds { get; init; } = Array.Empty<string>();

        [JsonProperty("reward_values")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] RewardValues { get; init; } = Array.Empty<int>();

        [JsonProperty("frame_id")]
        public int FrameId { get; init; }
    }
}
