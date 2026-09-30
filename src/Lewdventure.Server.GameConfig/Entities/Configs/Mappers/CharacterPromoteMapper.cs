using Newtonsoft.Json;
using Server.Configs;

namespace Server.Entities
{
    internal sealed class CharacterPromoteMapper : ICharacterPromoteMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("promote_level")]
        public int PromoteLevel { get; init; }

        [JsonProperty("frame_id")]
        public int FrameId { get; init; }

        [JsonProperty("copies_to_upgrade")]
        public int CopiesToUpgrade { get; init; }

        [JsonProperty("reward_types")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] RewardTypes { get; init; } = Array.Empty<string>();

        [JsonProperty("reward_ids")]
        [JsonConverter(typeof(DelimitedStringArrayConverter), ';')]
        public string[] RewardIds { get; init; } = Array.Empty<string>();

        [JsonProperty("reward_values")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] RewardValues { get; init; } = Array.Empty<int>();
    }
}
