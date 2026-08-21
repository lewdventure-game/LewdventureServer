using Newtonsoft.Json;
using Server.Configs;

namespace Server.Aspects
{
    internal sealed class AspectMapper : IAspectMapper
    {
        [JsonProperty("id")]
        public int Id { get; init; }

        [JsonProperty("bonus_ids")]
        [JsonConverter(typeof(DelimitedIntArrayConverter), ';')]
        public int[] BonusIds { get; init; } = Array.Empty<int>();

        [JsonProperty("bonus_values")]
        [JsonConverter(typeof(DelimitedFloatArrayConverter), ';')]
        public float[] BonusValues { get; init; } = Array.Empty<float>();
    }
}
